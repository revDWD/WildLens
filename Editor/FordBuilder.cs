using System.Collections.Generic;
using UnityEngine;
using WildLens.Core;
using WildLens.Data;
using WildLens.Environment;

namespace WildLens.EditorTools
{
    /// <summary>
    /// The Creek Ford (1.4; part of <see cref="VegetationBuilder"/>). Why a photographer comes here: moving water and
    /// the creek's own wildlife. A belted kingfisher fishes from a snag over the riffle, a spotted sandpiper teeters on the
    /// gravel bar, a heron stalks the shallows, and you can cross on stepping stones. The creek had bridges, but no place
    /// to stop and watch the water itself.
    ///
    /// Planned with the reserve (before scenery, so trees respect it): the calmest open reach of Willow Creek 18–70 m from
    /// any path and away from crossings, with a short footpath spur to the bank. Built with the ecosystem: stones, a
    /// painted gravel bar, the perch snag, a log seat facing upstream, a sign and a short panel, a residence and hotspots.
    /// Uses its own random source, so the rest of the reserve is laid out exactly as before.
    /// </summary>
    public partial class VegetationBuilder
    {
        private bool _hasFord;
        private int _fordIndex;
        private Vector3 _fordPoint, _fordDir, _fordSide, _fordBar, _fordApproach;
        private const float FordBarRadius = 3.2f;

        /// <summary>Choose the ford and lay its footpath spur. Call after creek, tracks and roads are planned.</summary>
        public void PlanFord(Transform parent, bool meshes)
        {
            int n = _creek.Count;
            if (n < 24 || _creekWater == null) return;
            var rng = new System.Random(_s.seed * 977 + 13);
            float best = float.MaxValue;
            for (int i = n / 5; i < n * 4 / 5; i += 2)
            {
                Vector3 p = _creek[i];
                float d = PathDistance(p);
                if (d < 18f || d > 70f || InKeepOut(p)) continue;
                int a = Mathf.Max(0, i - 4), b = Mathf.Min(n - 1, i + 4);
                float drop = Mathf.Abs(_creekWater[a] - _creekWater[b]);
                if (drop > 0.8f) continue;                                         // a calm riffle, not a chute
                float score = Mathf.Abs(d - 32f) * 0.6f + drop * 20f + Forest(p) * 25f + (float)rng.NextDouble() * 4f;
                if (score < best) { best = score; _fordIndex = i; }
            }
            if (best == float.MaxValue) return;

            _hasFord = true;
            _fordPoint = new Vector3(_creek[_fordIndex].x, _creekWater[_fordIndex], _creek[_fordIndex].z);
            _fordDir = TerrainUtil.Flat(_creek[Mathf.Min(n - 1, _fordIndex + 2)] - _creek[Mathf.Max(0, _fordIndex - 2)]).normalized;
            _fordSide = Vector3.Cross(Vector3.up, _fordDir);

            // Nearest point on any path: the spur leaves from there and arrives on the near bank.
            Vector3 from = _trail.GetPoint(0f);
            float fromD = float.MaxValue;
            void Consider(IReadOnlyList<Vector3> pts) { foreach (var q in pts) { float dq = TerrainUtil.FlatDistance(q, _fordPoint); if (dq < fromD) { fromD = dq; from = q; } } }
            Consider(_trail.Points);
            foreach (var t in _tracks) Consider(t.Points);
            if (Vector3.Dot(from - _fordPoint, _fordSide) < 0f) _fordSide = -_fordSide; // +side faces the visitor
            _fordApproach = TerrainUtil.Ground(_fordPoint + _fordSide * 4.6f);
            _fordBar = TerrainUtil.Ground(_fordPoint - _fordSide * 3.6f + _fordDir * 2.5f); // far bank, a little downstream

            var path = new List<Vector3>();
            float len = TerrainUtil.FlatDistance(from, _fordApproach);
            for (float s = 0f; s <= len; s += 3f) path.Add(TerrainUtil.Ground(Vector3.Lerp(from, _fordApproach, s / Mathf.Max(0.01f, len))));
            path.Add(_fordApproach);
            var go = new GameObject("Track: Creek Ford");
            go.transform.SetParent(parent, false);
            var track = go.AddComponent<ReserveTrack>();
            track.title = "Creek Ford";
            track.SetPath(path, 1.3f);
            track.kind = TrackKind.Footpath;
            _tracks.Add(track);
            if (meshes)
            {
                var mesh = GeneratedAssets.Save(ProceduralMeshes.TrailRibbon(path, 1.3f, 0.6f, closed: false), "Mesh_Track_CreekFord");
                var surface = new GameObject("Track Surface");
                surface.transform.SetParent(go.transform, false);
                surface.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = surface.AddComponent<MeshRenderer>();
                mr.sharedMaterial = GeneratedAssets.Material("TrackDirt", Color.white, 0.05f);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            AddKeepOut(_fordPoint, 6.5f);
            AddKeepOut(_fordBar, FordBarRadius + 0.8f);
        }

        /// <summary>Gravel-bar weight 0..1 for the terrain painter (stony ground, no grass).</summary>
        private float FordBar(Vector3 p)
        {
            if (!_hasFord) return 0f;
            float d = TerrainUtil.FlatDistance(p, _fordBar);
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FordBarRadius * 0.6f, FordBarRadius, d));
        }

        private void BuildFord(Transform parent, Material wood, Material post)
        {
            if (!_hasFord) return;
            var rng = new System.Random(_s.seed * 977 + 29);
            float Rf(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var group = new GameObject("Creek Ford").transform;
            group.SetParent(parent, false);

            // Stepping stones straight across, tops just above the water, each with a flat collider you can stand on.
            var stoneMat = GeneratedAssets.Material("CreekStone", new Color(0.42f, 0.41f, 0.38f), 0.35f);
            var stoneMesh = _rocks[0];
            for (float x = -2.4f; x <= 2.41f; x += 0.75f)
            {
                Vector3 c = _fordPoint + _fordSide * x + _fordDir * Rf(-0.12f, 0.12f);
                c.y = _fordPoint.y + 0.06f;
                var stone = new GameObject("Stepping Stone");
                stone.transform.SetParent(group, false);
                stone.transform.SetPositionAndRotation(c, Quaternion.Euler(Rf(-4f, 4f), Rf(0f, 360f), Rf(-4f, 4f)));
                var size = new Vector3(Rf(0.55f, 0.7f), 0.5f, Rf(0.5f, 0.65f));
                stone.transform.localScale = size;
                stone.AddComponent<MeshFilter>().sharedMesh = stoneMesh;
                stone.AddComponent<MeshRenderer>().sharedMaterial = stoneMat;
                var col = stone.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, -0.2f, 0f);
                col.size = new Vector3(0.9f, 0.6f, 0.9f);
                AddFeature(stone, FeatureKind.Rock, size.x * 0.5f, 0.15f);
                MakeStatic(stone);
            }

            // The gravel bar on the far bank: a few rounded stones scattered on the painted gravel (the sandpiper's beat).
            for (int i = 0; i < 9; i++)
            {
                float a = Rf(0f, Mathf.PI * 2f), r = Mathf.Sqrt(Rf(0f, 1f)) * FordBarRadius * 0.85f;
                Vector3 p = TerrainUtil.Ground(_fordBar + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
                if (CreekDistance(p) < 2.4f) continue;
                MeshPart("Bar Stone", group, _rocks[rng.Next(_rocks.Length)], stoneMat, p, Vector3.one * Rf(0.18f, 0.42f),
                    Quaternion.Euler(Rf(0f, 25f), Rf(0f, 360f), Rf(0f, 25f)));
            }
            AddHotspot(group, "Ford Gravel Bar", HotspotType.Water, _fordBar, 6f);

            // A dead snag leaning over the water upstream: the kingfisher's lookout.
            var deadwood = GeneratedAssets.Material("DeadWood", new Color(0.45f, 0.42f, 0.38f));
            Vector3 snag = TerrainUtil.Ground(_fordPoint - _fordSide * 3.2f - _fordDir * 5f);
            Snag(group, snag, deadwood);
            AddHotspot(group, "Kingfisher Perch", HotspotType.Perch, snag, 5f);

            // The visitor's side: a log seat facing upstream across the riffle, a sign, a short panel.
            Vector3 seat = TerrainUtil.Ground(_fordApproach + _fordDir * 2.2f);
            Vector3 look = TerrainUtil.Flat(_fordPoint - _fordDir * 6f - seat).normalized;
            var bench = PropBuilder.Group("Ford Seat", group, seat, Quaternion.LookRotation(look));
            PropBuilder.Box("Seat Log", bench, new Vector3(0f, 0.24f, 0f), new Vector3(1.9f, 0.38f, 0.42f), post);
            PropBuilder.AddCollider(bench, new Vector3(0f, 0.24f, 0f), new Vector3(1.9f, 0.38f, 0.42f));
            SignKit.Sign(group, TerrainUtil.Ground(_fordApproach - _fordDir * 2.4f + _fordSide * 0.8f), Quaternion.LookRotation(_fordSide),
                "CREEK FORD", "watch the riffle", new[] { SignIcon.Dawn, SignIcon.Water }, 0, post);
            InterpretivePanel(group, TerrainUtil.Ground(_fordApproach + _fordSide * 1.2f - _fordDir * 0.6f), _fordSide, SignIcon.Bird,
                "THE RIFFLE", "Kingfishers dive\nfrom the snag.\nSandpipers wade.", wood, post);

            var voice = new GameObject("Ford Brook");
            voice.transform.SetParent(group, false);
            voice.transform.position = _fordPoint + Vector3.up * 0.5f;
            voice.AddComponent<AudioSource>();
            var amb = voice.AddComponent<AmbientSound>();
            amb.kind = AmbientKind.Brook;
            amb.volume = 0.6f;
            amb.audibleRadius = 40f;

            Residence(group, "Creek ford", _fordPoint - _fordSide * 2f, 8f, "belted_kingfisher", "spotted_sandpiper", "great_blue_heron");
            Anchor(group, ScheduleRole.Water, _fordPoint, 18f);
            AddViewpoint(group, "Creek Ford", seat + Vector3.up * 1.2f, _fordPoint - _fordDir * 8f + Vector3.up * 0.4f);
        }
    }
}
