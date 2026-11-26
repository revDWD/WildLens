using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using WildLens.Core;
using WildLens.UI;

namespace WildLens.Environment
{
    /// <summary>
    /// The trailhead information board: a park notice board maintained by the reserve staff, and quietly the
    /// game's version dashboard. Everything on it is read from the running game when the scene starts (package
    /// version, the build that made the scene, features, recent changes, species and journal progress, trails,
    /// viewpoints, destinations), so it can never go out of date. Its map is drawn from the real terrain, trails,
    /// tracks, trees and destinations, with a "you are here" marker.
    ///
    /// The transform is the centre of the board face; +Z faces the reader.
    /// </summary>
    public class ReserveInfoBoard : MonoBehaviour
    {
        [Tooltip("Board face size in metres.")]
        public Vector2 size = new Vector2(2.6f, 1.6f);

        private const float UnitsPerMetre = 500f;
        private static readonly Color Paper = new Color(0.86f, 0.83f, 0.72f);
        private static readonly Color Green = new Color(0.2f, 0.3f, 0.18f);
        private static readonly Color Cream = new Color(0.93f, 0.9f, 0.8f);
        private static readonly Color Ink = new Color(0.17f, 0.15f, 0.11f);
        private static readonly Color Brown = new Color(0.45f, 0.29f, 0.14f);
        private const string HeadingHex = "#33492a";

        private RectTransform _root;
        private Text _status;
        private Texture2D _map;

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

            var paper = UIFactory.CreateImage("Paper", _root, Paper);
            UIFactory.Stretch(paper.rectTransform);

            // Header band
            var header = UIFactory.CreateImage("Header", _root, Green);
            Top(header.rectTransform, 0f, 0f, w, 92f);
            var title = UIFactory.CreateText("Title", header.rectTransform, "WILLOW CREEK NATURE RESERVE", 44, Cream, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.Stretch(title.rectTransform, 30f, 0f, 300f, 0f);
            var sub = UIFactory.CreateText("Subtitle", header.rectTransform, "Visitor Information\n<size=19>Willow Creek Conservation Trust · est. 1962</size>", 24, Cream, TextAnchor.MiddleRight);
            UIFactory.Stretch(sub.rectTransform, 0f, 0f, 30f, 0f);

            // Left: map, legend
            float mapSize = 500f;
            var mapFrame = UIFactory.CreateImage("Map Frame", _root, Brown);
            Top(mapFrame.rectTransform, 26f, 112f, mapSize + 8f, mapSize + 8f);
            var map = UIFactory.CreateRawImage("Map", mapFrame.rectTransform);
            UIFactory.Stretch(map.rectTransform, 4f, 4f, 4f, 4f);
            BuildMap(map, mapSize);
            var legend = UIFactory.CreateText("Legend", _root,
                "<color=#b8996b><b>——</b></color> Reserve Road   <color=#8a5a2b><b>——</b></color> Meadow Loop   <color=#a0784a><b>– –</b></color> Footpaths   " +
                "<color=#5b8fb3><b>——</b></color> Creek   <color=#c0392b><b>●</b></color> You are here   (map faces the way you do)", 19, Ink);
            Top(legend.rectTransform, 30f, 126f + mapSize, mapSize, 60f);

            // Right column
            float x = 566f, cw = w - x - 30f, y = 110f;
            var welcome = UIFactory.CreateText("Welcome", _root, WelcomeText(), 22, Ink);
            Top(welcome.rectTransform, x, y, cw, 100f);
            y += 104f;

            Heading("WHAT YOU CAN DO HERE", x, y, cw);
            y += 34f;
            var features = WildLensVersion.Features;
            int rows = Mathf.CeilToInt(features.Length / 2f);
            for (int i = 0; i < features.Length; i++)
            {
                int col = i / rows, row = i % rows;
                float fx = x + col * (cw * 0.5f), fy = y + row * 27f;
                var box = UIFactory.CreateImage("Tick", _root, Green);
                Top(box.rectTransform, fx + 2f, fy + 6f, 16f, 16f);
                var tick = UIFactory.CreateImage("Tick Mark", box.rectTransform, Cream);
                UIFactory.Stretch(tick.rectTransform, 4f, 4f, 4f, 4f);
                var t = UIFactory.CreateText("Feature", _root, features[i], 21, Ink);
                Top(t.rectTransform, fx + 26f, fy, cw * 0.5f - 30f, 28f);
            }
            y += rows * 27f + 10f;

            Heading("NEW THIS SEASON", x, y, cw);
            y += 34f;
            var sb = new StringBuilder();
            for (int i = 0; i < Mathf.Min(3, WildLensVersion.RecentChanges.Length); i++)
                sb.Append(i == 0 ? "<b>" : "").Append(WildLensVersion.RecentChanges[i]).Append(i == 0 ? "</b>" : "").Append('\n');
            var changes = UIFactory.CreateText("Changes", _root, sb.ToString().TrimEnd('\n'), 20, Ink);
            Top(changes.rectTransform, x, y, cw, 78f);
            y += 82f;

            Heading("RESERVE STATUS", x, y, cw);
            y += 34f;
            _status = UIFactory.CreateText("Status", _root, "", 20, Ink);
            Top(_status.rectTransform, x, y, cw, h - y - 70f);
            RefreshStatus();

            // Footer
            var footer = UIFactory.CreateImage("Footer", _root, new Color(0.76f, 0.72f, 0.58f));
            Top(footer.rectTransform, 0f, h - 52f, w, 52f);
            var tip = UIFactory.CreateText("Ranger Tip", footer.rectTransform,
                "<b>Ranger's tip:</b> ride the Reserve Road, park at the pull-offs and walk the last stretch. " +
                "Wildlife keeps hours: come back at a different time of day.", 20, Ink, TextAnchor.MiddleCenter);
            UIFactory.Stretch(tip.rectTransform, 20f, 0f, 20f, 0f);
        }

        private void Heading(string text, float x, float y, float width)
        {
            var t = UIFactory.CreateText("Heading", _root, $"<color={HeadingHex}>{text}</color>", 24, Ink, TextAnchor.UpperLeft, FontStyle.Bold);
            Top(t.rectTransform, x, y, width, 30f);
            var rule = UIFactory.CreateImage("Rule", _root, new Color(Green.r, Green.g, Green.b, 0.5f));
            Top(rule.rectTransform, x, y + 29f, width, 2f);
        }

        private static void Top(RectTransform rt, float x, float y, float width, float height) =>
            UIFactory.Place(rt, new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(width, height));

        // ------------------------------------------------------------------ content

        private static string WelcomeText()
        {
            var info = SceneQuery.FindOne<SliceInfo>();
            string built = info != null
                ? (info.IsCurrent ? $"Scene built with {info.packageVersion}" : $"<color=#a0401a>Scene built with {info.packageVersion}: please rebuild</color>")
                : "Scene build version unknown";
            return $"<b>Welcome to the reserve.</b> Walk the Meadow Loop, or take the trail bike out to the far corners.\n" +
                   $"<size=19><color=#5b5444>WildLens {WildLensVersion.Package} \"{WildLensVersion.ReleaseName}\" · {WildLensVersion.ReleaseDate} · " +
                   $"{built} · environment {WildLensVersion.Environment}</color></size>";
        }

        private void RefreshStatus()
        {
            if (_status == null) return;
            var sb = new StringBuilder();
            var game = WildLensGame.Instance;
            var clock = DayCycle.Instance;
            if (clock != null) sb.Append($"<b>Now: {DayCycle.PeriodName(clock.Period)}, {clock.ClockText}.</b> {RightNow(clock.Period)}\n");
            int species = game != null && game.Species != null ? game.Species.Count : 0;
            int seen = game != null && game.Journal != null ? game.Journal.DiscoveredCount : 0;
            sb.Append($"Species living here: <b>{species}</b>   ·   in your journal: <b>{seen}</b>\n");

            var trail = SceneQuery.FindOne<TrailPath>();
            float tracks = 0f;
            foreach (var t in ReserveTrack.All) tracks += t.Length;
            sb.Append("Trails: <b>Meadow Loop</b>");
            if (trail != null) sb.Append($" {trail.Length / 1000f:0.0} km");
            float road = 0f; foreach (var t in ReserveTrack.All) if (t.kind == TrackKind.Road) road += t.Length;
            if (road > 0f) sb.Append($"   ·   <b>Reserve Road</b> {road / 1000f:0.0} km");
            if (ReserveTrack.All.Count > 0) sb.Append($"   ·   footpaths {(tracks - road) / 1000f:0.0} km");
            sb.Append('\n');
            int views = SceneQuery.FindAll<ScenicViewpoint>().Length;
            sb.Append($"Scenic viewpoints: <b>{views}</b>   ·   Destinations: <b>{ReserveDestination.All.Count}</b>");
            foreach (var c in ReserveCreek.All) sb.Append($"   ·   <b>{c.title}</b> {c.Length / 1000f:0.0} km");
            if (ReserveParking.All.Count > 0) sb.Append($"   ·   <b>{ReserveParking.All.Count}</b> bike pull-offs");
            sb.Append('\n');

            var dests = new List<ReserveDestination>(ReserveDestination.All);
            dests.Sort((a, b) => a.number.CompareTo(b.number));
            foreach (var d in dests)
            {
                float km = TerrainUtil.FlatDistance(d.transform.position, transform.position) / 1000f;
                string when = string.IsNullOrEmpty(d.bestTimes) ? Short(d.fieldNote) : "best: " + d.bestTimes;
                sb.Append($"<b>{d.number}</b>  {d.title}  <color=#5b5444>{km:0.0} km · {when}</color>\n");
            }
            _status.text = sb.ToString().TrimEnd('\n');
        }

        /// <summary>What the reserve is doing at this time of day: the patterns, said plainly.</summary>
        private static string RightNow(DayPeriod p)
        {
            switch (p)
            {
                case DayPeriod.Dawn: return "Mist on the water; cottontails on the meadow edges.";
                case DayPeriod.Morning: return "Deer in the meadows; hawks rising over the ridge.";
                case DayPeriod.Midday: return "Deer resting in the woods; the grove is cool and quiet.";
                case DayPeriod.GoldenHour: return "Best light on the ridge; deer back in the meadows.";
                case DayPeriod.Dusk: return "Deer at the pond and creek; foxes on the meadow edges.";
                default: return "The reserve sleeps.";
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
                Marker(target.rectTransform, ToPx(d.transform.position) * k, new Color(0.2f, 0.3f, 0.18f), d.number.ToString(), d.title);
            Marker(target.rectTransform, ToPx(transform.position) * k, new Color(0.75f, 0.22f, 0.15f), "", "You are here");
        }

        private static void Marker(RectTransform map, Vector2 pos, Color colour, string number, string label)
        {
            var dot = UIFactory.CreateImage("Marker", map, colour);
            UIFactory.Place(dot.rectTransform, Vector2.zero, pos - new Vector2(13f, 13f), new Vector2(26f, 26f));
            if (!string.IsNullOrEmpty(number))
            {
                var n = UIFactory.CreateText("Number", dot.rectTransform, number, 20, Cream, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIFactory.Stretch(n.rectTransform);
            }
            bool right = pos.x < 330f;
            var t = UIFactory.CreateText("Label", map, $"<b>{label}</b>", 18, Ink, right ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
            UIFactory.Place(t.rectTransform, Vector2.zero, right ? pos + new Vector2(18f, -14f) : pos + new Vector2(-218f, -14f), new Vector2(200f, 28f));
            var halo = t.gameObject.AddComponent<Outline>();
            halo.effectColor = new Color(Paper.r, Paper.g, Paper.b, 0.9f);
            halo.effectDistance = new Vector2(1.5f, -1.5f);
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
