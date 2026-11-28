using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using WildLens.Core;
using WildLens.UI;

namespace WildLens.Environment
{
    /// <summary>
    /// The trailhead information board, designed like a national-park trailhead sign (1.2): read in seconds, from a few
    /// steps back. Header: the reserve's name, the time of day and conditions. Left: a large map drawn from the real
    /// terrain, trails, tracks, creek, trees and destinations, with numbered markers and "you are here". Right: the
    /// destinations, each with its number, distance and best times as pictograms. Footer: one line of etiquette and the
    /// version (kept small; full release notes live in CHANGELOG.md). Everything is read from the running game when the
    /// scene starts, so it never goes out of date; a stale scene is flagged in the footer.
    ///
    /// The transform is the centre of the board face; +Z faces the reader.
    /// </summary>
    public class ReserveInfoBoard : MonoBehaviour
    {
        [Tooltip("Board face size in metres.")]
        public Vector2 size = new Vector2(2.6f, 1.6f);
        [Tooltip("Best-time pictograms (dawn, day, golden hour, dusk, rain), set by the builder; the same as on the signs.")]
        public Texture2D[] timeIcons = new Texture2D[0];

        private const float UnitsPerMetre = 500f;
        private static readonly Color Paper = new Color(0.9f, 0.87f, 0.76f);
        private static readonly Color Green = new Color(0.2f, 0.32f, 0.19f);
        private static readonly Color Cream = new Color(0.95f, 0.91f, 0.8f);
        private static readonly Color Ink = new Color(0.14f, 0.12f, 0.09f);
        private static readonly Color Muted = new Color(0.36f, 0.32f, 0.25f);
        private static readonly Color Brown = new Color(0.45f, 0.29f, 0.14f);

        private RectTransform _root;
        private Text _nowBig, _nowSmall;
        private Texture2D _map, _disc;

        private void Start()
        {
            BuildCanvas();
            var game = WildLensGame.Instance;
            if (game != null && game.Journal != null) game.Journal.Changed += RefreshStatus;
        }

        private void OnDestroy()
        {
            var game = WildLensGame.Instance;
            if (game != null && game.Journal != null) game.Journal.Changed -= RefreshStatus;
            if (_map != null) Destroy(_map);
            if (_disc != null) Destroy(_disc);
        }

        // ------------------------------------------------------------------ layout

        private void BuildCanvas()
        {
            var go = new GameObject("Board Canvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 0.012f);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // canvases face -Z; the board faces +Z
            go.transform.localScale = Vector3.one / UnitsPerMetre;
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 3f; // crisp text up close
            _root = (RectTransform)go.transform;
            _root.sizeDelta = size * UnitsPerMetre;
            float w = _root.sizeDelta.x, h = _root.sizeDelta.y;
            _disc = DiscTexture(64);

            var paper = UIFactory.CreateImage("Paper", _root, Paper);
            UIFactory.Stretch(paper.rectTransform);

            // Header: the place, then the moment.
            const float headerH = 150f;
            var header = UIFactory.CreateImage("Header", _root, Green);
            Top(header.rectTransform, 0f, 0f, w, headerH);
            var title = UIFactory.CreateText("Title", header.rectTransform, "WILLOW CREEK", 72, Cream, TextAnchor.UpperLeft, FontStyle.Bold);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            Top(title.rectTransform, 34f, 12f, 640f, 86f);
            var sub = UIFactory.CreateText("Subtitle", header.rectTransform, "NATURE RESERVE", 36, Cream, TextAnchor.UpperLeft);
            Top(sub.rectTransform, 38f, 96f, 640f, 46f);
            _nowBig = UIFactory.CreateText("Now", header.rectTransform, "", 52, Cream, TextAnchor.UpperRight, FontStyle.Bold);
            _nowBig.horizontalOverflow = HorizontalWrapMode.Overflow; // right-aligned: grows leftwards
            Top(_nowBig.rectTransform, w - 640f, 16f, 606f, 64f);
            // 1.6: larger (34), shrinking only when a long forecast line needs it, never spilling off the header.
            _nowSmall = UIFactory.CreateText("Conditions", header.rectTransform, "", 34, Cream, TextAnchor.UpperRight);
            _nowSmall.resizeTextForBestFit = true;
            _nowSmall.resizeTextMinSize = 26;
            _nowSmall.resizeTextMaxSize = 34;
            Top(_nowSmall.rectTransform, w - 780f, 86f, 746f, 50f);

            // Map: the biggest thing on the board.
            const float footerH = 68f;
            float mapSize = Mathf.Min(640f, h - headerH - footerH - 70f);
            var mapFrame = UIFactory.CreateImage("Map Frame", _root, Brown);
            Top(mapFrame.rectTransform, 30f, headerH + 18f, mapSize + 8f, mapSize + 8f);
            var map = UIFactory.CreateRawImage("Map", mapFrame.rectTransform);
            UIFactory.Stretch(map.rectTransform, 4f, 4f, 4f, 4f);
            BuildMap(map, mapSize);
            var legend = UIFactory.CreateText("Legend", _root,
                "<color=#b8996b><b>——</b></color> Road    <color=#8a5a2b><b>——</b></color> Loop    <color=#a0784a><b>– –</b></color> Footpath    " +
                "<color=#5b8fb3><b>——</b></color> Creek", 24, Ink);
            Top(legend.rectTransform, 34f, headerH + mapSize + 32f, mapSize, 34f);

            // Destinations: number, name, distance, best times.
            float x = mapSize + 80f, cw = w - x - 34f, y = headerH + 20f;
            var heading = UIFactory.CreateText("Heading", _root, "DESTINATIONS", 34, Green, TextAnchor.UpperLeft, FontStyle.Bold);
            Top(heading.rectTransform, x, y, cw, 42f);
            var rule = UIFactory.CreateImage("Rule", _root, Green);
            Top(rule.rectTransform, x, y + 44f, cw, 3f);
            y += 64f;
            var dests = new List<ReserveDestination>(ReserveDestination.All);
            dests.Sort((a, b) => a.number.CompareTo(b.number));
            float rowH = dests.Count > 0 ? Mathf.Min(132f, (h - footerH - 10f - y) / dests.Count) : 132f;
            foreach (var d in dests)
            {
                var disc = UIFactory.CreateRawImage("Disc", _root);
                disc.texture = _disc;
                disc.color = Green;
                Top(disc.rectTransform, x, y + 4f, 72f, 72f);
                var n = UIFactory.CreateText("Number", disc.rectTransform, d.number.ToString(), 44, Cream, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIFactory.Stretch(n.rectTransform);
                var name = UIFactory.CreateText("Destination", _root, d.title, 40, Ink, TextAnchor.UpperLeft, FontStyle.Bold);
                Top(name.rectTransform, x + 90f, y - 2f, cw - 90f, 52f);
                float ix = x + 92f;
                foreach (int k in TimeIndices(d.bestTimes))
                {
                    if (timeIcons == null || k >= timeIcons.Length || timeIcons[k] == null) continue;
                    var icon = UIFactory.CreateRawImage("Best Time", _root);
                    icon.texture = timeIcons[k];
                    icon.color = k == 1 || k == 2 ? new Color(0.78f, 0.5f, 0.12f) : Muted;
                    Top(icon.rectTransform, ix, y + 52f, 44f, 44f);
                    ix += 50f;
                }
                float km = TerrainUtil.FlatDistance(d.transform.position, transform.position) / 1000f;
                var dist = UIFactory.CreateText("Distance", _root, $"{km:0.0} km", 34, Muted, TextAnchor.UpperLeft);
                Top(dist.rectTransform, ix + 8f, y + 56f, cw - (ix - x), 40f);
                y += rowH;
            }

            // Footer: one line of etiquette; the version small at the right (a stale scene is flagged here).
            var footer = UIFactory.CreateImage("Footer", _root, new Color(0.78f, 0.74f, 0.6f));
            Top(footer.rectTransform, 0f, h - footerH, w, footerH);
            var tip = UIFactory.CreateText("Etiquette", footer.rectTransform,
                "Walk quietly  ·  wildlife keeps hours", 32, Ink, TextAnchor.MiddleLeft); // 1.6: riding advice is on the road face
            UIFactory.Stretch(tip.rectTransform, 34f, 0f, 380f, 0f);
            var version = UIFactory.CreateText("Version", footer.rectTransform, VersionText(), 22, Muted, TextAnchor.MiddleRight);
            UIFactory.Stretch(version.rectTransform, 0f, 0f, 30f, 0f);
            RefreshStatus();
        }

        private static void Top(RectTransform rt, float x, float y, float width, float height) =>
            UIFactory.Place(rt, new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(width, height));

        // ------------------------------------------------------------------ content

        private static string VersionText()
        {
            var info = SceneQuery.FindOne<SliceInfo>();
            if (info != null && !info.IsCurrent) return $"<color=#a0401a><b>Scene built with {info.packageVersion}: please rebuild</b></color>";
            return $"WildLens {WildLensVersion.Package} · {WildLensVersion.ReleaseName}";
        }

        /// <summary>Best-time pictogram indices (0 dawn, 1 day, 2 golden hour, 3 dusk) from a destination's best-times words.</summary>
        private static IEnumerable<int> TimeIndices(string bestTimes)
        {
            string t = (bestTimes ?? "").ToLowerInvariant();
            if (t.Contains("dawn") || t.Contains("early morning")) yield return 0;
            if (t.Contains("midday") || (t.Contains("morning") && !t.Contains("early morning"))) yield return 1;
            if (t.Contains("golden") || t.Contains("late afternoon") || t.Contains("evening")) yield return 2;
            if (t.Contains("dusk")) yield return 3;
            if (t.Contains("rain")) yield return 4; // 1.5: "after rain
        }

        /// <summary>
        /// Conditions for the header. There is no weather yet, so this follows the light; when weather arrives, its state
        /// belongs here ("Overcast", "Light rain").
        /// </summary>
        private static string Conditions(DayPeriod p)
        {
            switch (p)
            {
                case DayPeriod.Dawn: return "Misty";
                case DayPeriod.Dusk: return "Calm";
                case DayPeriod.Night: return "Clear night";
                default: return "Fair";
            }
        }

        private void RefreshStatus()
        {
            if (_nowBig == null) return;
            var clock = DayCycle.Instance;
            if (clock == null) { _nowBig.text = ""; _nowSmall.text = "Welcome"; return; }
            _nowBig.text = $"{DayCycle.PeriodName(clock.Period).ToUpperInvariant()}  {clock.ClockText}";
            var weather = WeatherCycle.Instance;
            string conditions = weather != null ? weather.ConditionText(clock.Period) : Conditions(clock.Period);
            string doing = weather != null && weather.Condition == WeatherCondition.LightRain ? "wildlife sheltering in the woods" : RightNow(clock.Period);
            _nowSmall.text = $"{conditions}  ·  {doing}";
        }

        /// <summary>What the reserve is doing at this time of day: the patterns, said plainly.</summary>
        private static string RightNow(DayPeriod p)
        {
            switch (p)
            {
                case DayPeriod.Dawn: return "mist on the pond";
                case DayPeriod.Morning: return "deer in the meadows";
                case DayPeriod.Midday: return "deer resting in the woods";
                case DayPeriod.GoldenHour: return "best light on the ridge";
                case DayPeriod.Dusk: return "deer at the water";
                default: return "the reserve sleeps";
            }
        }

        private float _refresh;
        private DayPeriod _lastPeriod = (DayPeriod)(-1);

        private void Update()
        {
            _refresh -= Time.deltaTime;
            if (_refresh > 0f) return;
            _refresh = 2f;
            if (DayCycle.Instance != null && DayCycle.Instance.Period != _lastPeriod) _lastPeriod = DayCycle.Instance.Period;
            RefreshStatus(); // keeps the clock line current
        }

        private static string Short(string note)
        {
            if (string.IsNullOrEmpty(note)) return "";
            int cut = note.IndexOf('.');
            return cut > 0 ? note.Substring(0, cut) : note;
        }

        // ------------------------------------------------------------------ map

        private void BuildMap(RawImage target, float displaySize)
        {
            var trail = SceneQuery.FindOne<TrailPath>();
            var pts = new List<Vector3>();
            if (trail != null) pts.AddRange(trail.Points);
            foreach (var t in ReserveTrack.All) pts.AddRange(t.Points);
            foreach (var c in ReserveCreek.All) pts.AddRange(c.Points);
            foreach (var d in ReserveDestination.All) pts.Add(d.transform.position);
            pts.Add(transform.position);
            // Heads-up map, like a real park board: up on the map is the direction the reader faces.
            Vector3 look = -transform.forward; look.y = 0f;
            look = look.sqrMagnitude > 0.001f ? look.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, look);
            Vector2 M(Vector3 p) => new Vector2(Vector3.Dot(p, side), Vector3.Dot(p, look));
            Vector3 W(Vector2 m) => side * m.x + look * m.y;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (var p in pts)
            {
                min = Vector2.Min(min, M(p));
                max = Vector2.Max(max, M(p));
            }
            float span = Mathf.Max(max.x - min.x, max.y - min.y) + 80f;
            Vector2 centre = (min + max) * 0.5f;
            Vector2 origin = centre - Vector2.one * span * 0.5f;

            const int res = 256;
            float cell = span / res;
            var heights = new float[res + 1, res + 1];
            for (int z = 0; z <= res; z++)
            for (int x = 0; x <= res; x++)
                heights[x, z] = TerrainUtil.SampleHeight(W(origin + new Vector2(x, z) * cell));

            var trees = new float[res, res];
            foreach (var f in SceneQuery.FindAll<EnvironmentFeature>())
            {
                if (f.kind != FeatureKind.Tree) continue;
                Vector2 m = M(f.transform.position);
                int tx = Mathf.FloorToInt((m.x - origin.x) / cell), tz = Mathf.FloorToInt((m.y - origin.y) / cell);
                for (int dz = -2; dz <= 2; dz++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    int x = tx + dx, z = tz + dz;
                    if (x < 0 || z < 0 || x >= res || z >= res) continue;
                    trees[x, z] += 1f / (1f + dx * dx + dz * dz);
                }
            }

            var px = new Color[res * res];
            Color land = new Color(0.86f, 0.84f, 0.68f), forest = new Color(0.5f, 0.62f, 0.4f), water = new Color(0.45f, 0.62f, 0.7f);
            Vector3 light = new Vector3(-1f, 1.6f, 1f).normalized;
            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                float h = heights[x, z];
                Vector3 n = new Vector3(heights[x, z] - heights[x + 1, z], cell, heights[x, z] - heights[x, z + 1]).normalized;
                float shade = Mathf.Lerp(0.82f, 1.08f, Mathf.Clamp01(Vector3.Dot(n, light)));
                Color c = Color.Lerp(land, forest, Mathf.Clamp01(trees[x, z] * 0.9f)) * shade;
                // Contours every 5 m.
                float c0 = Mathf.Floor(h / 5f), c1 = Mathf.Floor(heights[x + 1, z] / 5f), c2 = Mathf.Floor(heights[x, z + 1] / 5f);
                if (c0 != c1 || c0 != c2) c = Color.Lerp(c, new Color(0.55f, 0.45f, 0.3f), 0.35f);
                c.a = 1f;
                px[z * res + x] = c;
            }

            Vector2 ToPx(Vector3 p) => (M(p) - origin) / cell;
            foreach (var d in ReserveDestination.All)
                if (d.waterRadius > 0f) Disc(px, res, ToPx(d.transform.position), d.waterRadius / cell, water);
            foreach (var c in ReserveCreek.All) Polyline(px, res, c.Points, ToPx, 1.4f, new Color(0.36f, 0.56f, 0.7f), dashed: false);
            foreach (var t in ReserveTrack.All)
                if (t.kind == TrackKind.Road) Polyline(px, res, t.Points, ToPx, 2f, new Color(0.72f, 0.6f, 0.42f), dashed: false);
                else Polyline(px, res, t.Points, ToPx, 1.1f, new Color(0.6f, 0.44f, 0.27f), dashed: true);
            if (trail != null)
            {
                var loop = new List<Vector3>(trail.Points);
                if (loop.Count > 0) loop.Add(loop[0]);
                Polyline(px, res, loop, ToPx, 1.6f, new Color(0.54f, 0.35f, 0.17f), dashed: false);
            }

            _map = new Texture2D(res, res, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            _map.SetPixels(px);
            _map.Apply();
            target.texture = _map;

            // Markers as UI so the numbers stay sharp.
            float k = displaySize / res;
            foreach (var d in ReserveDestination.All)
                Marker(target.rectTransform, ToPx(d.transform.position) * k, Green, d.number.ToString(), d.title, displaySize);
            Marker(target.rectTransform, ToPx(transform.position) * k, new Color(0.75f, 0.22f, 0.15f), "", "You are here", displaySize);
        }

        private void Marker(RectTransform map, Vector2 pos, Color colour, string number, string label, float displaySize)
        {
            var dot = UIFactory.CreateRawImage("Marker", map);
            dot.texture = _disc;
            dot.color = colour;
            UIFactory.Place(dot.rectTransform, Vector2.zero, pos - new Vector2(20f, 20f), new Vector2(40f, 40f));
            if (!string.IsNullOrEmpty(number))
            {
                var n = UIFactory.CreateText("Number", dot.rectTransform, number, 28, Cream, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIFactory.Stretch(n.rectTransform);
            }
            bool right = pos.x < displaySize * 0.55f;
            var t = UIFactory.CreateText("Label", map, $"<b>{label}</b>", 26, Ink, right ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
            UIFactory.Place(t.rectTransform, Vector2.zero, right ? pos + new Vector2(26f, -18f) : pos + new Vector2(-286f, -18f), new Vector2(260f, 36f));
            var halo = t.gameObject.AddComponent<Outline>();
            halo.effectColor = new Color(Paper.r, Paper.g, Paper.b, 0.95f);
            halo.effectDistance = new Vector2(2f, -2f);
        }

        /// <summary>A soft-edged white disc for round markers (tinted by the image colour).</summary>
        private static Texture2D DiscTexture(int n)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[n * n];
            float r = n * 0.5f - 1f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f));
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 0.5f));
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private static void Disc(Color[] px, int res, Vector2 c, float r, Color colour)
        {
            int x0 = Mathf.Max(0, (int)(c.x - r - 1)), x1 = Mathf.Min(res - 1, (int)(c.x + r + 1));
            int z0 = Mathf.Max(0, (int)(c.y - r - 1)), z1 = Mathf.Min(res - 1, (int)(c.y + r + 1));
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                float d = Vector2.Distance(new Vector2(x, z), c);
                if (d <= r) px[z * res + x] = Color.Lerp(colour, px[z * res + x], Mathf.Clamp01(d - r + 1f));
            }
        }

        private static void Polyline(Color[] px, int res, IReadOnlyList<Vector3> pts, System.Func<Vector3, Vector2> toPx,
            float radius, Color colour, bool dashed)
        {
            float run = 0f;
            for (int i = 1; i < pts.Count; i++)
            {
                Vector2 a = toPx(pts[i - 1]), b = toPx(pts[i]);
                float len = Vector2.Distance(a, b);
                for (float s = 0f; s <= len; s += 0.5f)
                {
                    run += 0.5f;
                    if (dashed && run % 8f > 5f) continue;
                    Disc(px, res, Vector2.Lerp(a, b, len > 0f ? s / len : 0f), radius, colour);
                }
            }
        }
    }
}
