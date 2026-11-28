using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WildLens.Core;
using WildLens.Data;
using WildLens.Environment;

namespace WildLens.EditorTools
{
    /// <summary>
    /// Willow Falls (1.5; part of <see cref="VegetationBuilder"/>), the reserve's fifth destination. Why a photographer
    /// comes here: falling water and spray catching the morning light, a mossy ravine, a phoebe on the ledge, and after
    /// the afternoon shower the falls roar (the weather changes the place). It uses what the reserve already has:
    /// Willow Creek's own fall off the upland (landforms v2), the weather, and the photography loop.
    ///
    /// Plan (inside <see cref="PlanCreek"/>, before carving): find the creek reach that naturally loses the most height
    /// over ~50 m, away from the pond and the meadow loop; move that loss into one lip (2–7 m); below it the bed runs
    /// nearly level through a plunge pool, cut deeper, and the carve widens into a ravine with steep walls. The bed is only
    /// ever lowered, never raised, so it stays a cut into real ground. Then a destination site with a viewing deck on the
    /// ravine rim (the track and bike pull-off follow automatically).
    /// Build: a falling-water curtain over a rock face, the pool, spray and roar driven by recent rain
    /// (<see cref="Waterfall"/>), the deck, a lookout at the top, sign and panel, ferns, the phoebe.
    /// </summary>
    public partial class VegetationBuilder
    {
        private int _fallsIndex = -1;
        private float _fallsDrop, _fallsFoot;
        private float[] _carveExtra;
        private const int FallsWindow = 20; // creek samples (2.5 m each) below the lip that give up their fall to it

        /// <summary>Reshape the creek bed so its steepest reach becomes a waterfall. Call after ComputeBed, before CarveCreek.</summary>
        private void PlanFalls()
        {
            int n = _creek.Count;
            _carveExtra = new float[n];
            if (n < FallsWindow * 2 + 10) return;
            Site pond = _sites.Find(s => s.kind == DestinationKind.Wetland);
            int best = -1;
            float bestDrop = 0f;
            for (int i = n / 6; i < n - FallsWindow - 6; i++)
            {
                if (pond != null && TerrainUtil.FlatDistance(_creek[i], pond.centre) < pond.water * 1.4f + 40f) continue;
                if (LoopRadius(_creek[i]) < 1.15f) continue; // not on the meadow loop
                float drop = _creekBed[i + 1] - _creekBed[i + FallsWindow];
                if (drop > bestDrop) { bestDrop = drop; best = i; }
            }
            if (best < 0 || bestDrop < 2.2f)
            {
                Debug.Log($"[WildLens] No waterfall: Willow Creek doesn't fall far enough anywhere (best {bestDrop:0.0} m over 50 m).");
                return;
            }
            int L = best;
            float D = Mathf.Clamp(bestDrop - 0.4f, 2f, 7f);
            float foot = _creekBed[L] - D;
            for (int j = 1; j <= FallsWindow; j++)
            {
                float t = j / (float)FallsWindow;
                float target = Mathf.Lerp(foot, _creekBed[L + FallsWindow], t);
                _creekBed[L + j] = Mathf.Min(_creekBed[L + j], target);
                // The ravine: widest at the foot of the falls, closing over the run below.
                _carveExtra[L + j] = 6f * Mathf.Pow(1f - t, 1.5f);
            }
            _carveExtra[L] = 3f;
            if (L > 0) _carveExtra[L - 1] = 1.5f;
            for (int j = L; j <= L + FallsWindow; j++) _creekWater[j] = _creekBed[j] + 0.32f;
            // Plunge pool: a deeper bowl at the foot with level water.
            for (int j = 1; j <= 3; j++)
            {
                _creekWater[L + j] = foot + 0.32f;
                _creekBed[L + j] = foot - 0.8f;
            }
            _fallsIndex = L;
            _fallsDrop = D;
            _fallsFoot = foot;
        }

        /// <summary>The falls as a destination site: centre between the lip and a viewing deck on the ravine rim.</summary>
        private void PlanFallsSite()
        {
            if (_fallsIndex < 0) return;
            int n = _creek.Count, L = _fallsIndex;
            Vector3 lip = new Vector3(_creek[L].x, _creekWater[L], _creek[L].z);
            Vector3 down = TerrainUtil.Flat(_creek[L + 1] - _creek[L]).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, down);
            int at = Mathf.Min(n - 1, L + 6);
            float rim = 4.6f + _carveExtra[at] + 3.5f;
            // The deck goes on whichever rim is gentler (and away from structures).
            Vector3 a = TerrainUtil.Ground(_creek[at] + side * rim), b = TerrainUtil.Ground(_creek[at] - side * rim);
            float score(Vector3 p) => TerrainUtil.SlopeDegrees(p) + (InKeepOut(p) ? 100f : 0f);
            Vector3 deck = score(a) <= score(b) ? a : b;
            var site = new Site
            {
                kind = DestinationKind.Waterfall,
                title = "Willow Falls",
                note = "Willow Creek drops off the upland into a mossy ravine. A phoebe nests by the ledge; after rain the falls roar.",
                residents = new[] { "eastern_phoebe", "eastern_gray_squirrel" },
                centre = TerrainUtil.Ground((lip + deck) * 0.5f),
                radius = 22f,
                arrival = deck,
                facing = TerrainUtil.Flat(lip - deck).normalized,
            };
            _sites.Add(site);
            AddKeepOut(deck, 4f);
            AddKeepOut(lip, 7f);
            Debug.Log($"[WildLens] Willow Falls: a {_fallsDrop:0.0} m drop {TerrainUtil.FlatDistance(lip, _creek[0]):0} m down Willow Creek.");
        }

        // ------------------------------------------------------------------ build

        private void BuildWaterfall(Transform group, Site s, Material wood, Material post)
        {
            if (_fallsIndex < 0) return;
            var rng = new System.Random(_s.seed * 613 + 7);
            float Rf(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            int n = _creek.Count, L = _fallsIndex;
            Vector3 lip = new Vector3(_creek[L].x, _creekWater[L], _creek[L].z);
            Vector3 down = TerrainUtil.Flat(_creek[L + 1] - _creek[L]).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, down);
            float poolWater = _fallsFoot + 0.32f;
            Vector3 pool = new Vector3(_creek[L + 2].x, poolWater, _creek[L + 2].z);

            // The rock face the water falls over: a scanned face set into the lip, open side downstream.
            if (_artOutcrops.Count > 0)
            {
                var face = InstantiatePrefab(_artOutcrops[0], group, lip, 1f);
                var f = face.GetComponent<EnvironmentFeature>();
                float h = f != null ? f.height : 5f;
                float k = Mathf.Clamp((_fallsDrop + 1.6f) / Mathf.Max(0.5f, h), 0.45f, 1.6f);
                face.transform.localScale = _artOutcrops[0].transform.localScale * k;
                face.transform.SetPositionAndRotation(new Vector3(lip.x, _fallsFoot - 0.6f, lip.z) - down * 0.6f, Quaternion.LookRotation(down));
                face.name = "Falls Rock Face";
                if (f != null) { f.radius *= k; f.height *= k; }
                MakeStatic(face);
                // Ravine walls: two more faces looking into the gorge just below the falls.
                for (int i = 0; i < 2 && _artOutcrops.Count > 0; i++)
                {
                    float sgn = i == 0 ? 1f : -1f;
                    int at = Mathf.Min(n - 1, L + 4 + i * 2);
                    Vector3 p = TerrainUtil.Ground(_creek[at] + side * sgn * (4.6f + _carveExtra[at] * 0.55f));
                    var wall = InstantiatePrefab(_artOutcrops[_artOutcrops.Count - 1], group, p, Rf(0.55f, 0.75f));
                    wall.transform.rotation = Quaternion.LookRotation(-side * sgn) * Quaternion.Euler(0f, Rf(-15f, 15f), 0f);
                    wall.transform.position = p - Vector3.up * 1.2f;
                    wall.name = "Ravine Wall";
                    MakeStatic(wall);
                }
            }

            // The falling water: a curtain arcing out from the lip to the pool, streaks scrolling down.
            var curtain = new GameObject("Falls Curtain");
            curtain.transform.SetParent(group, false);
            curtain.AddComponent<MeshFilter>().sharedMesh = GeneratedAssets.Save(CurtainMesh(lip, down, side, poolWater), "Mesh_FallsCurtain");
            var cr = curtain.AddComponent<MeshRenderer>();
            cr.sharedMaterial = FallsMaterial();
            cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var flow = curtain.AddComponent<WaterFlow>();
            flow.speed = new Vector2(0f, -1.4f);
            flow.scrollBase = true;
            flow.wobble = 0f;

            // Spray and roar, driven by recent rain.
            var falls = group.gameObject.AddComponent<Waterfall>();
            falls.curtain = flow;
            falls.sprayMaterial = MistMaterial();
            falls.sprayCentre = pool + Vector3.up * 0.4f;
            falls.spraySize = new Vector3(4.5f, 0.6f, 3f);
            falls.sprayRotation = Quaternion.LookRotation(down);
            var roarGo = new GameObject("Falls Roar");
            roarGo.transform.SetParent(group, false);
            roarGo.transform.position = pool + Vector3.up;
            roarGo.AddComponent<AudioSource>();
            var roar = roarGo.AddComponent<AmbientSound>();
            roar.kind = AmbientKind.Falls;
            roar.volume = 0.75f;
            roar.audibleRadius = 90f;
            falls.roar = roarGo.GetComponent<AudioSource>();

            // Moss and ferns on the ravine floor and walls, a few boulders at the pool's edge.
            for (int i = 0; i < 14; i++)
            {
                int at = Mathf.Min(n - 1, L + 1 + rng.Next(0, 10));
                Vector3 p = TerrainUtil.Ground(_creek[at] + side * (rng.NextDouble() < 0.5 ? -1f : 1f) * Rf(2.6f, 4.6f + _carveExtra[at] * 0.6f));
                if (CreekDistance(p) < 2.4f) continue;
                if (i < 4 && _art && _artBoulders.Count > 0) PlaceArt(_artBoulders, group, p, Rf(0.5f, 0.8f), 0.25f, 10f);
                else if (_art && _artFerns.Count > 0) PlaceArt(_artFerns, group, p, Rf(0.8f, 1.3f), 0.02f, 8f);
            }

            // The viewing deck on the rim, facing the falls.
            Vector3 deckPos = s.arrival;
            Vector3 face2 = TerrainUtil.Flat(new Vector3(lip.x, 0f, lip.z) - deckPos).normalized;
            var deck = PropBuilder.Group("Falls Viewing Deck", group, deckPos, Quaternion.LookRotation(face2));
            float y = 0.3f;
            Planks(deck, new Vector3(0f, y, 0f), new Vector3(3.4f, 0.12f, 2.4f), wood, post);
            foreach (var c in new[] { new Vector3(-1.6f, 0f, -1.1f), new Vector3(1.6f, 0f, -1.1f), new Vector3(-1.6f, 0f, 1.1f), new Vector3(1.6f, 0f, 1.1f) })
                PropBuilder.Box("Post", deck, c + new Vector3(0f, 0.55f, 0f), new Vector3(0.12f, 1.4f, 0.12f), post);
            PropBuilder.Box("Rail Front", deck, new Vector3(0f, y + 0.95f, 1.15f), new Vector3(3.4f, 0.09f, 0.09f), post);
            PropBuilder.Box("Rail L", deck, new Vector3(-1.65f, y + 0.95f, 0f), new Vector3(0.09f, 0.09f, 2.4f), post);
            PropBuilder.Box("Rail R", deck, new Vector3(1.65f, y + 0.95f, 0f), new Vector3(0.09f, 0.09f, 2.4f), post);
            PropBuilder.Box("Bench Seat", deck, new Vector3(0f, y + 0.45f, -0.55f), new Vector3(1.8f, 0.08f, 0.42f), wood);
            PropBuilder.Box("LegL", deck, new Vector3(-0.75f, y + 0.22f, -0.55f), new Vector3(0.09f, 0.45f, 0.4f), post);
            PropBuilder.Box("LegR", deck, new Vector3(0.75f, y + 0.22f, -0.55f), new Vector3(0.09f, 0.45f, 0.4f), post);
            PropBuilder.AddCollider(deck, new Vector3(0f, y, 0f), new Vector3(3.4f, 0.12f, 2.4f));
            PropBuilder.AddCollider(deck, new Vector3(0f, y + 0.6f, 1.15f), new Vector3(3.4f, 1.2f, 0.1f));
            foreach (float x in new[] { -1.65f, 1.65f }) PropBuilder.AddCollider(deck, new Vector3(x, y + 0.6f, 0f), new Vector3(0.1f, 1.2f, 2.4f));
            AddViewpoint(group, "Willow Falls", deckPos + Vector3.up * (y + 1.65f), new Vector3(lip.x, (lip.y + poolWater) * 0.5f, lip.z));

            Vector3 back = -face2, across = Vector3.Cross(Vector3.up, face2);
            Plank(group, "WILLOW FALLS", "roars after rain", SignKit.TimeIcons(BestTimes(s.kind)), s.number,
                TerrainUtil.Ground(deckPos + back * 3.6f + across * 2.2f), Quaternion.LookRotation(back), post);
            InterpretivePanel(group, TerrainUtil.Ground(deckPos + back * 3.4f - across * 2.3f), back, SignIcon.Water, "THE FALLS",
                "The creek drops off the upland here.\nAfter rain, it roars.", wood, post);

            // A lookout at the top of the falls: rail on the lip's edge, looking down the ravine.
            Vector3 topSide = Vector3.Dot(deckPos - lip, side) > 0f ? side : -side;
            Vector3 top = TerrainUtil.Ground(lip + topSide * 5.2f - down * 1.5f);
            var rail = PropBuilder.Group("Falls Top Lookout", group, top, Quaternion.LookRotation(down));
            foreach (float x in new[] { -1.1f, 1.1f })
                PropBuilder.Box("Post", rail, new Vector3(x, 0.55f, 0.6f), new Vector3(0.11f, 1.1f, 0.11f), post);
            PropBuilder.Box("Rail", rail, new Vector3(0f, 1.0f, 0.6f), new Vector3(2.4f, 0.09f, 0.09f), post);
            PropBuilder.AddCollider(rail, new Vector3(0f, 0.6f, 0.6f), new Vector3(2.4f, 1.2f, 0.12f));
            AddViewpoint(group, "Top of the Falls", top + Vector3.up * 1.65f, TerrainUtil.Ground(_creek[Mathf.Min(n - 1, L + 12)]) + Vector3.up * 0.5f);

            // The phoebe's ledge and a water hotspot at the pool.
            AddHotspot(group, "Falls Ledge", HotspotType.Perch, lip + topSide * 3f + Vector3.up * 0.5f, 5f);
            AddHotspot(group, "Plunge Pool", HotspotType.Water, pool, 6f);
            Anchor(group, ScheduleRole.Water, pool, 16f);
        }

        /// <summary>A curtain of falling water: arcs out from the lip, widens a little as it falls, UVs run downward.</summary>
        private Mesh CurtainMesh(Vector3 lip, Vector3 down, Vector3 side, float poolY)
        {
            const int rows = 10;
            float drop = lip.y - poolY;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int r = 0; r <= rows; r++)
            {
                float t = r / (float)rows;
                float y = lip.y + 0.05f - (drop + 0.15f) * t;
                float outward = 0.35f + Mathf.Sqrt(t) * Mathf.Min(1.1f, drop * 0.22f);
                float half = Mathf.Lerp(1.6f, 2f, t);
                Vector3 c = new Vector3(lip.x, y, lip.z) + down * outward;
                verts.Add(c - side * half);
                verts.Add(c + side * half);
                uvs.Add(new Vector2(0f, t * drop / 1.2f));
                uvs.Add(new Vector2(2f, t * drop / 1.2f));
                if (r == 0) continue;
                int i0 = (r - 1) * 2;
                tris.AddRange(new[] { i0, i0 + 2, i0 + 1, i0 + 1, i0 + 2, i0 + 3 });
            }
            var mesh = new Mesh { name = "FallsCurtain" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Streaky white water, alpha blended, both faces (the pipeline's particle shader, like the rain).</summary>
        private static Material FallsMaterial()
        {
            string texPath = GeneratedAssets.Folder + "/T_FallsStreaks.asset";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null)
            {
                const int w = 64, h = 256;
                tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
                var rng = new System.Random(5);
                var col = new float[w];
                for (int x = 0; x < w; x++) col[x] = 0.45f + (float)rng.NextDouble() * 0.55f;
                var px = new Color[w * h];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float streak = col[x] * (0.75f + 0.25f * Mathf.Sin((y / (float)h * 6f + col[(x + 7) % w] * 3f) * Mathf.PI * 2f));
                    float edge = Mathf.SmoothStep(0f, 1f, Mathf.Min(x, w - 1 - x) / 6f);
                    float a = Mathf.Clamp01(streak * edge * 0.95f);
                    px[y * w + x] = new Color(0.94f, 0.97f, 1f, a);
                }
                tex.SetPixels(px);
                tex.Apply();
                AssetDatabase.CreateAsset(tex, texPath);
            }
            string path = GeneratedAssets.Folder + "/M_FallsWater.mat";
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            bool urp = pipeline != null && pipeline.GetType().Name.Contains("Universal");
            var shader = urp ? Shader.Find("Universal Render Pipeline/Particles/Unlit") : null;
            if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader != null ? shader : MaterialLibrary.LitShader); AssetDatabase.CreateAsset(mat, path); }
            if (shader != null) mat.shader = shader;
            foreach (var prop in new[] { "_BaseMap", "_MainTex" }) if (mat.HasProperty(prop)) mat.SetTexture(prop, tex);
            Color tint = new Color(0.9f, 0.94f, 0.97f, 0.88f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", tint * 0.55f);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            if (urp)
            {
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
                if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
