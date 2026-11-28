using System.Collections.Generic;
using UnityEngine;
using WildLens.Core;
using WildLens.Data;
using WildLens.Environment;

namespace WildLens.EditorTools
{
    /// <summary>
    /// Hidden discoveries (1.5; part of <see cref="VegetationBuilder"/>). A few unmarked places found by reading the
    /// ground, each with a reason to stop. They are not on the map and have no signs or counters: the reward is the
    /// place itself, and a single field note the first time you walk in.
    ///   Hidden Overlook: the most prominent upland shelf away from the Ridge Overlook and every path; a log seat, a small
    ///                    cairn, the view cleared. The new landforms make these exist.
    ///   Fern Glade:      a hollow inside dense woods, far from paths; trees cleared to a small opening, a fern carpet and a
    ///                    mossy log (and, since 1.5, the first macro subjects).
    ///   Creek Alcove:    a wooded bend of Willow Creek out of sight of the trails; flat rocks to sit on, a raccoon at dusk.
    /// Uses its own random source, after all other scenery, so nothing else moves.
    /// </summary>
    public partial class VegetationBuilder
    {
        public int DiscoveriesPlaced { get; private set; }
        private Vector3? _fernGlade, _creekAlcove;
        private readonly List<(Vector3 p, float r)> _discoveryPoints = new List<(Vector3, float)>();

        public void BuildDiscoveries(Transform parent)
        {
            var rng = new System.Random(_s.seed * 389 + 3);
            float Rf(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var group = new GameObject("Hidden Places").transform;
            group.SetParent(parent, false);
            var post = GeneratedAssets.Material("WoodDark", new Color(0.33f, 0.24f, 0.16f));
            var stone = GeneratedAssets.Material("CairnStone", new Color(0.5f, 0.49f, 0.46f), 0.2f);
            Site ridge = _sites.Find(x => x.kind == DestinationKind.RidgeOverlook);

            // ---- Hidden Overlook: prominence on the upland, away from the ridge and the paths.
            Vector3? overlook = null;
            float best = float.MinValue;
            ForReserveGrid(p =>
            {
                p = TerrainUtil.Ground(p);
                if (ridge != null && TerrainUtil.FlatDistance(p, ridge.centre) < 160f) return;
                if (PathDistance(p) < 35f || InKeepOut(p) || TerrainUtil.SlopeDegrees(p) > 12f || CreekDistance(p) < 20f) return;
                float prominence = p.y - RingMean(p, 40f);
                if (prominence < 2.5f) return;
                float score = prominence * 2f + p.y * 0.1f;
                if (score > best) { best = score; overlook = p; }
            });
            if (overlook.HasValue)
            {
                Vector3 p = overlook.Value, view = BestView(p);
                Clear(p, 4f);
                ClearCone(p, view, 32f, 130f);
                var seat = PropBuilder.Group("Overlook Seat", group, TerrainUtil.Ground(p - view * 1.2f), Quaternion.LookRotation(view));
                if (_art && _artLogs.Count > 0)
                {
                    var log = PlaceArt(_artLogs, seat, seat.position, 0.75f, 0.2f, 2f);
                    log.transform.rotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, view));
                }
                else PropBuilder.Box("Seat Log", seat, new Vector3(0f, 0.24f, 0f), new Vector3(1.8f, 0.36f, 0.4f), post);
                Vector3 cairn = TerrainUtil.Ground(p + Vector3.Cross(Vector3.up, view) * 1.8f);
                float cy = 0f;
                for (int i = 0; i < 4; i++)
                {
                    float sz = 0.5f - i * 0.1f;
                    MeshPart("Cairn Stone", group, _rocks[rng.Next(_rocks.Length)], stone, cairn + Vector3.up * (cy + sz * 0.3f),
                        new Vector3(sz, sz * 0.6f, sz * 0.9f), Quaternion.Euler(0f, Rf(0f, 360f), 0f));
                    cy += sz * 0.45f;
                }
                AddViewpoint(group, "Hidden Overlook", p + Vector3.up * 1.6f, TerrainUtil.Ground(p + view * 140f) + Vector3.up * 4f);
                Discovery(group, p, "Hidden Overlook", "A quiet shelf above the reserve. Few visitors find it; the hawks know it well.", 9f);
                AddHotspot(group, "Overlook Perch", HotspotType.Perch, cairn, 4f);
            }

            // ---- Fern Glade: a hollow in dense woods.
            Vector3? glade = null;
            best = float.MinValue;
            ForReserveGrid(p =>
            {
                p = TerrainUtil.Ground(p);
                if (PathDistance(p) < 30f || InKeepOut(p) || TerrainUtil.SlopeDegrees(p) > 14f || CreekDistance(p) < 15f) return;
                int near = 0;
                foreach (var t in _treeObjects) if (t != null && TerrainUtil.FlatDistance(t.transform.position, p) < 14f) near++;
                if (near < 7) return;
                float score = near + Forest(p) * 5f;
                if (score > best) { best = score; glade = p; }
            });
            if (glade.HasValue)
            {
                Vector3 p = glade.Value;
                Clear(p, 6f);
                if (_art && _artFerns.Count > 0)
                    for (int i = 0; i < 34; i++)
                    {
                        float a = Rf(0f, Mathf.PI * 2f), r = Mathf.Sqrt(Rf(0.05f, 1f)) * 7f;
                        if (r < 1.4f) continue;
                        PlaceArt(_artFerns, group, TerrainUtil.Ground(p + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r), Rf(0.8f, 1.35f), 0.02f, 8f);
                    }
                if (_art && _artLogs.Count > 0)
                {
                    var log = PlaceArt(_artLogs, group, TerrainUtil.Ground(p + new Vector3(Rf(-1f, 1f), 0f, Rf(-1f, 1f))), 0.85f, 0.25f, 3f);
                    log.name = "Mossy Log";
                }
                if (_art && _artStumps.Count > 0) PlaceArt(_artStumps, group, TerrainUtil.Ground(p + new Vector3(2.4f, 0f, -1.6f)), 1f, 0.05f, 4f);
                AddViewpoint(group, "Fern Glade", p + new Vector3(-2.5f, 1.3f, -2.5f), p + new Vector3(2f, 0.4f, 2f));
                Discovery(group, p, "Fern Glade", "A hollow of ferns under the canopy. Sit still and listen: the woods carry on around you.", 7f);
                Residence(group, "Fern glade", p, 8f, "black_capped_chickadee"); // a small flock of chickadees: the tamest birds here
                AddHotspot(group, "Glade Edge", HotspotType.MatureTree, p, 10f);
                _fernGlade = p;
            }

            // ---- Creek Alcove: a wooded bend out of sight of the trails, away from the ford and the falls.
            int alcove = -1;
            best = float.MinValue;
            for (int i = 8; i < _creek.Count - 8; i++)
            {
                Vector3 c = _creek[i];
                if (PathDistance(c) < 40f || InKeepOut(c)) continue;
                if (_hasFord && TerrainUtil.FlatDistance(c, _fordPoint) < 70f) continue;
                if (_fallsIndex >= 0 && Mathf.Abs(i - _fallsIndex) < 30) continue;
                float bend = Vector3.Angle(TerrainUtil.Flat(_creek[i] - _creek[i - 6]), TerrainUtil.Flat(_creek[i + 6] - _creek[i]));
                float score = Forest(c) * 30f + bend * 0.4f;
                if (score > best) { best = score; alcove = i; }
            }
            if (alcove > 0)
            {
                Vector3 c = _creek[alcove];
                Vector3 dir = TerrainUtil.Flat(_creek[alcove + 1] - _creek[alcove - 1]).normalized, side = Vector3.Cross(Vector3.up, dir);
                Vector3 bank = TerrainUtil.Ground(c + side * 3.6f);
                Clear(bank, 3.5f);
                if (_art && _artBoulders.Count > 0)
                    for (int i = 0; i < 2; i++)
                        PlaceArt(_artBoulders, group, TerrainUtil.Ground(bank + dir * (i == 0 ? -1.2f : 1.1f) + side * Rf(0f, 0.6f)), Rf(0.45f, 0.6f), 0.35f, 6f);
                if (_art && _artFerns.Count > 0)
                    for (int i = 0; i < 10; i++)
                        PlaceArt(_artFerns, group, TerrainUtil.Ground(bank + dir * Rf(-5f, 5f) + side * Rf(1f, 4f)), Rf(0.7f, 1.2f), 0.02f, 8f);
                AddViewpoint(group, "Creek Alcove", bank + Vector3.up * 1.2f + side * 0.6f, new Vector3(c.x, _creekWater[alcove] + 0.4f, c.z) - dir * 6f);
                Discovery(group, bank, "Creek Alcove", "A bend in Willow Creek, out of sight of the trails. Raccoons come to wash their food at dusk.", 8f);
                Residence(group, "Creek alcove", c + side * 1.5f, 8f, "raccoon", "song_sparrow");
                AddHotspot(group, "Alcove Water", HotspotType.Water, c, 6f);
                _creekAlcove = bank;
            }
        }

        private void Discovery(Transform group, Vector3 p, string title, string note, float radius)
        {
            var go = new GameObject("Discovery: " + title);
            go.transform.SetParent(group, false);
            go.transform.position = p;
            var d = go.AddComponent<ReserveDiscovery>();
            d.title = title;
            d.note = note;
            d.radius = radius;
            DiscoveriesPlaced++;
            _discoveryPoints.Add((p, radius)); // 1.9: new places keep their distance from the ones already found
        }
    }
}
