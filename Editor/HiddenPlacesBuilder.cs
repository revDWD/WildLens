using System.Collections.Generic;
using UnityEngine;
using WildLens.Core;
using WildLens.Data;
using WildLens.Environment;

namespace WildLens.EditorTools
{
    /// <summary>
    /// Four more hidden places (1.9; part of <see cref="VegetationBuilder"/>, built in the late geography step). Like the
    /// Hidden Overlook, Fern Glade and Creek Alcove (1.5): unmarked, on no map or sign, never counted, found and named once.
    /// Each is chosen from the land for a reason a photographer would come, and skipped cleanly where the land has no site.
    ///   Cascade Overlook  a ledge on the ravine's rim above the cascade, with a line of sight to it; a log seat, no rail.
    ///   Hidden Pond       a small still pond in a wooded hollow ≥ 45 m from every path: reeds, a half-sunk log, water for
    ///                     a heron, blackbirds and raccoons, a water stop at dusk (and the small life another builder puts at water).
    ///   Rock Shelter      an overhanging crag set into an upland hillside, its alcove dry underfoot (≥ 2.2 m clear), a
    ///                     long-cold fire ring, a view out.
    ///   Old Cabin Ruins   a stone foundation and chimney stub in a small clearing taken back by ferns and young trees, rotted
    ///                     sills, and the last posts of a split-rail fence where a bluebird perches.
    /// Prototyped and measured first (Python): all four were found on every one of eight seeds, 42–130 m from the nearest
    /// path, reachable on foot with no slope steeper than 15° on the gentlest approach.
    /// </summary>
    public partial class VegetationBuilder
    {
        private class HiddenPondPlan
        {
            public Vector3 centre;
            public float radius, level, hollow;
        }

        private class ShelterPlan
        {
            public Vector3 floor, forward, right;
            public const float HalfWidth = 4.4f, Depth = 5.5f, Height = 4.2f, Bottom = -1.6f;
            public const float AlcoveHalfWidth = 2.5f, AlcoveDepth = 2.8f, Ceiling = 2.6f;
            public void Local(Vector3 p, out float x, out float z)
            {
                Vector3 d = p - floor;
                x = d.x * right.x + d.z * right.z;
                z = d.x * forward.x + d.z * forward.z;
            }
            /// <summary>Inside the crag's footprint (the rock and its alcove floor).</summary>
            public bool InCrag(Vector3 p)
            {
                Local(p, out float x, out float z);
                return Mathf.Abs(x) <= HalfWidth + 0.4f && z >= -Depth - 0.4f && z <= 0.6f;
            }
        }

        private HiddenPondPlan _hiddenPond;
        private ShelterPlan _shelter;

        // ================================================================== Cascade Overlook

        private Vector3? PlanCascadeOverlook(List<string> notes)
        {
            var cs = _cascade;
            int n = _run.Count;
            int mid = cs.lips[cs.lips.Length / 2];
            Vector3 target = new Vector3(_run[mid].x, cs.foot + cs.height * 0.5f, _run[mid].z);
            float best = float.MinValue;
            Vector3? chosen = null;
            for (int i = Mathf.Max(0, cs.lips[0] - 4); i <= Mathf.Min(n - 1, cs.pool[cs.pool.Length - 1] + 2); i++)
            {
                Vector3 side = Side(_run, i, out _);
                foreach (float sgn in new[] { 1f, -1f })
                    for (float off = 8f; off < 22f; off += 1.5f)
                    {
                        Vector3 p = TerrainUtil.Ground(_run[i] + side * sgn * off);
                        float slope = TerrainUtil.SlopeDegrees(p);
                        if (slope > 16f || p.y < cs.foot + cs.height + 0.5f) continue;
                        float dist = TerrainUtil.FlatDistance(p, target);
                        if (dist < 8f || dist > 24f) continue;
                        if (PathDistance(p) < 30f || InKeepOut(p) || NearHiddenPlace(p, 30f)) continue;
                        // A line of sight from eye height to the middle of the cascade.
                        float eye = p.y + 1.6f;
                        bool clear = true;
                        for (int s = 1; s <= 9 && clear; s++)
                        {
                            float t = s / 10f;
                            Vector3 q = Vector3.Lerp(p, target, t);
                            if (TerrainUtil.SampleHeight(q) > Mathf.Lerp(eye, target.y, t) - 0.2f) clear = false;
                        }
                        if (!clear) continue;
                        float score = (p.y - cs.foot) - Mathf.Abs(dist - 13f) * 0.3f - slope * 0.1f;
                        if (score > best) { best = score; chosen = p; }
                    }
            }
            if (!chosen.HasValue) { notes.Add("no Cascade Overlook (no ledge with a view of the cascade)"); return null; }
            _newHidden.Add((chosen.Value, 6f));
            return chosen;
        }

        private void BuildCascadeOverlook(Transform hidden, Vector3 p)
        {
            var cs = _cascade;
            int mid = cs.lips[cs.lips.Length / 2];
            Vector3 target = new Vector3(_run[mid].x, cs.foot + cs.height * 0.5f, _run[mid].z);
            Vector3 view = TerrainUtil.Flat(target - p).normalized;
            Clear(p, 3.5f);
            ClearCone(p, view, 28f, TerrainUtil.FlatDistance(p, target) + 6f);
            var seat = PropBuilder.Group("Cascade Overlook Seat", hidden, TerrainUtil.Ground(p - view * 1.1f), Quaternion.LookRotation(view));
            if (_art && _artLogs.Count > 0)
            {
                var log = PlaceArt(_artLogs, seat, seat.position, 0.75f, 0.2f, 2f);
                log.transform.rotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, view));
            }
            else PropBuilder.Box("Seat Log", seat, new Vector3(0f, 0.24f, 0f), new Vector3(1.8f, 0.36f, 0.4f), GeneratedAssets.Material("WoodDark", new Color(0.33f, 0.24f, 0.16f)));
            if (_art && _artBoulders.Count > 0)
                PlaceArt(_artBoulders, hidden, TerrainUtil.Ground(p + Vector3.Cross(Vector3.up, view) * 1.9f), R(0.55f, 0.7f), 0.35f, 6f);
            AddViewpoint(hidden, "Cascade Overlook", p + Vector3.up * 1.6f, target);
            Discovery(hidden, p, "Cascade Overlook", "A ledge above the cascade. The run steps down through the hemlocks below your feet.", 6f);
        }

        // ================================================================== Hidden Pond

        private float HiddenPondRadius(HiddenPondPlan s, Vector3 p)
        {
            float a = Mathf.Atan2(p.z - s.centre.z, p.x - s.centre.x);
            float c = Mathf.Cos(a), sn = Mathf.Sin(a), o = _noiseOffset * 0.61f + 17f;
            float k = (Mathf.PerlinNoise(o + c * 1.1f, o + sn * 1.1f) - 0.5f) * 0.5f + (Mathf.PerlinNoise(o + 9f + c * 2.6f, o + sn * 2.6f) - 0.5f) * 0.2f;
            return s.radius * (1f + k);
        }

        private HiddenPondPlan PlanHiddenPond(List<string> notes)
        {
            Site cattail = _sites.Find(x => x.kind == DestinationKind.Wetland);
            float best = float.MinValue;
            Vector3 chosen = Vector3.zero;
            float chosenHollow = 0f;
            ForReserveGrid(q =>
            {
                Vector3 p = TerrainUtil.Ground(q);
                if (TerrainUtil.SlopeDegrees(p) > 6f || LoopRadius(p) < 1.3f) return;
                if (PathDistance(p) < 45f || WaterDistance(p) < 30f || RunDistance(p) < 45f || InKeepOut(p)) return;
                if (NearSite(p, 40f) || NearHiddenPlace(p, 60f)) return;
                if (cattail != null && TerrainUtil.FlatDistance(p, cattail.centre) < 140f) return;
                float f = Forest(p);
                if (f < 0.45f || MaxSlope(p, 12f) > 9f) return;
                float hollow = RingMean(p, 22f) - p.y;
                if (hollow < 0.2f) return;
                float score = hollow * 3f + f * 4f - TerrainUtil.SlopeDegrees(p) * 0.2f;
                if (score > best) { best = score; chosen = p; chosenHollow = hollow; }
            });
            if (best == float.MinValue) { notes.Add("no Hidden Pond (no wooded hollow away from the paths)"); return null; }
            const float R = 8.5f;
            var plan = new HiddenPondPlan { centre = chosen, radius = R, hollow = chosenHollow };
            // Water stays below the land all round: the lower of a wide ring and the ring at the water's edge, less 0.3 m.
            plan.level = Mathf.Min(RingMin(chosen, R * 1.35f + 14f), RingMin(chosen, R * 1.35f + 3f, 32)) - 0.3f;
            float level = plan.level;
            ShapeAt(chosen, R * 1.35f + 9f, (p, d, h) =>
            {
                float r = HiddenPondRadius(plan, p);
                if (d < r) return Mathf.Min(h, level - 0.15f - 1.1f * (1f - (d / r) * (d / r)));
                float bank = level + 0.12f + (d - r) * 0.09f;
                float w = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(r + 3f, r + 9f, d));
                return Mathf.Lerp(bank, h, w);
            });
            _hiddenPond = plan;
            _newHidden.Add((chosen, R + 8f));
            return plan;
        }

        private void BuildHiddenPond(Transform hidden, HiddenPondPlan s)
        {
            var group = new GameObject("Hidden Pond").transform;
            group.SetParent(hidden, false);
            group.position = s.centre;
            float maxR = s.radius * 1.35f, level = s.level;
            Clear(s.centre, maxR * 0.6f);
            // The same still, dark, rippled water as Cattail Pond (never a mirror).
            var water = new GameObject("Water");
            water.transform.SetParent(group, false);
            water.transform.position = new Vector3(s.centre.x, level, s.centre.z);
            water.transform.localScale = new Vector3(maxR + 3f, 1f, maxR + 3f);
            water.AddComponent<MeshFilter>().sharedMesh = GeneratedAssets.LoadOrCreate("Mesh_PondDisc", () => ProceduralMeshes.Disc(48));
            var mr = water.AddComponent<MeshRenderer>();
            mr.sharedMaterial = WaterMaterial("PondWater", new Color(0.06f, 0.085f, 0.07f), 0.88f, new Vector2(18f, 18f), 0.25f);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var drift = water.AddComponent<WaterFlow>();
            drift.speed = new Vector2(0.003f, 0.005f);
            drift.wobble = 1f;
            // Reflections of the trees around it, re-rendered as the light and weather change (not baked once).
            var probeGo = new GameObject("Hidden Pond Reflections");
            probeGo.transform.SetParent(group, false);
            probeGo.transform.position = new Vector3(s.centre.x, level + 2f, s.centre.z);
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = new Vector3(maxR * 2f + 24f, 20f, maxR * 2f + 24f);
            probe.boxProjection = true;
            probe.resolution = 128;
            probe.importance = 1;
            probeGo.AddComponent<ReflectionRefresh>();

            // Reeds and cattails where the shore is shallow; a few lily pads.
            var stems = new List<CombineInstance>();
            var heads = new List<CombineInstance>();
            Mesh capsule = PrimitiveMeshes.Get(PrimitiveType.Capsule);
            for (int i = 0; i < 80; i++)
            {
                float a = R(0f, Mathf.PI * 2f);
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float r = HiddenPondRadius(s, s.centre + dir * s.radius);
                Vector3 c = s.centre + dir * (r + R(-1.3f, 0.8f));
                float ground = TerrainUtil.SampleHeight(c);
                if (ground > level + 0.45f) continue;
                int k = _rng.Next(5, 10);
                for (int j = 0; j < k; j++)
                {
                    Vector3 p = new Vector3(c.x + R(-0.5f, 0.5f), Mathf.Max(level - 0.3f, TerrainUtil.SampleHeight(c)), c.z + R(-0.5f, 0.5f));
                    float h = R(1f, 1.8f);
                    var rot = Quaternion.Euler(R(-8f, 8f), R(0f, 360f), R(-8f, 8f));
                    stems.Add(new CombineInstance { mesh = _cylinder, transform = Matrix4x4.TRS(p + rot * Vector3.up * (h * 0.5f), rot, new Vector3(0.022f, h * 0.5f, 0.022f)) });
                    if (R() < 0.35f)
                        heads.Add(new CombineInstance { mesh = capsule, transform = Matrix4x4.TRS(p + rot * Vector3.up * (h * 0.82f), rot, new Vector3(0.05f, 0.11f, 0.05f)) });
                }
            }
            CombinedStatic(group, "Hidden Pond Reeds", stems, new Color(0.38f, 0.44f, 0.2f));
            CombinedStatic(group, "Hidden Pond Cattails", heads, new Color(0.3f, 0.19f, 0.1f));
            var pad = GeneratedAssets.Material("LilyPad", new Color(0.24f, 0.4f, 0.16f), 0.5f);
            var disc = GeneratedAssets.LoadOrCreate("Mesh_PondDisc", () => ProceduralMeshes.Disc(48));
            for (int i = 0; i < 12; i++)
            {
                float a = R(0f, Mathf.PI * 2f);
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 c = s.centre + dir * HiddenPondRadius(s, s.centre + dir * s.radius) * R(0.4f, 0.8f);
                float size = R(0.16f, 0.3f);
                // MeshPart places in the group's space; the group stands at the pond's centre.
                MeshPart("Lily Pad", group, disc, pad, group.InverseTransformPoint(new Vector3(c.x, level + 0.012f, c.z)), new Vector3(size, 1f, size), Quaternion.Euler(0f, R(0f, 360f), 0f));
            }

            // A half-sunk log reaching in from the bank: a perch for turtles one day, a heron's stand now.
            float la = R(0f, Mathf.PI * 2f);
            Vector3 ldir = new Vector3(Mathf.Cos(la), 0f, Mathf.Sin(la));
            Vector3 lp = s.centre + ldir * (HiddenPondRadius(s, s.centre + ldir * s.radius) - 1.2f);
            GameObject log;
            if (_art && _artLogs.Count > 0) log = PlaceArt(_artLogs, group, lp, 1f, 0f, 0f);
            else
            {
                log = new GameObject("Log");
                log.transform.SetParent(group, false);
                MeshPart("Trunk", log.transform, _cylinder, Pick(_bark), Vector3.zero, new Vector3(0.4f, 2.2f, 0.4f), Quaternion.Euler(0f, 0f, 90f));
            }
            log.name = "Sunken Log";
            log.transform.position = new Vector3(lp.x, level - 0.12f, lp.z);
            log.transform.rotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, ldir)) * Quaternion.Euler(0f, 90f, 7f);
            Mist(group, new Vector3(s.centre.x, level + 0.5f, s.centre.z), new Vector3(maxR * 2f + 4f, 1.2f, maxR * 2f + 4f), 0.9f);

            // Wildlife, as at Cattail Pond: two shallows hotspots on the shore (just outside the waterline, on the two gentlest
            // banks), water at dusk on the far bank, and the residents' home on dry ground by the first shallows (the spawner
            // samples a residence's whole disc). The banks are found without the random stream (nothing after them moves).
            Vector3 gentle1 = Vector3.right, gentle2 = Vector3.left;
            float g1 = float.MaxValue, g2 = float.MaxValue;
            for (int i = 0; i < 24; i++)
            {
                float a = i / 24f * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float sl = TerrainUtil.SlopeDegrees(s.centre + dir * (HiddenPondRadius(s, s.centre + dir * s.radius) + 2f));
                if (sl < g1) { g1 = sl; gentle1 = dir; }
            }
            for (int i = 0; i < 24; i++)
            {
                float a = i / 24f * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                if (Vector3.Dot(dir, gentle1) > -0.05f) continue; // the other side of the pond (more than a right angle round)
                float sl = TerrainUtil.SlopeDegrees(s.centre + dir * (HiddenPondRadius(s, s.centre + dir * s.radius) + 2f));
                if (sl < g2) { g2 = sl; gentle2 = dir; }
            }
            Vector3 Shore(Vector3 dir, float beyond) => TerrainUtil.Ground(s.centre + dir * (HiddenPondRadius(s, s.centre + dir * s.radius) + beyond));
            AddHotspot(group, "Hidden Pond Shallows", HotspotType.Water, Shore(gentle1, 1.2f), 7f);
            AddHotspot(group, "Hidden Pond Shallows", HotspotType.Water, Shore(gentle2, 1.2f), 7f);
            Vector3 far = TerrainUtil.Ground(s.centre - ldir * (HiddenPondRadius(s, s.centre - ldir * s.radius) + 4f));
            Anchor(group, ScheduleRole.Water, far, 12f);
            Residence(group, "Hidden pond", Shore(gentle1, 7f), 5f, "great_blue_heron", "red_winged_blackbird", "raccoon");
            Vector3 bank = TerrainUtil.Ground(s.centre + ldir * (maxR + 4f));
            AddViewpoint(group, "Hidden Pond", bank + Vector3.up * 1.4f, new Vector3(s.centre.x, level + 0.5f, s.centre.z) - ldir * s.radius * 0.5f);
            Discovery(hidden, s.centre, "Hidden Pond", "Still water in a hollow of the woods, far from any path. A heron fishes it; blackbirds call from the reeds.", s.radius + 8f);
        }

        // ================================================================== Rock Shelter

        private ShelterPlan PlanRockShelter(List<string> notes)
        {
            Vector3 tPos = _terrain.transform.position, size = _terrain.terrainData.size;
            float lo = float.MaxValue, hi = float.MinValue;
            ForReserveGrid(q => { float h = TerrainUtil.SampleHeight(q); lo = Mathf.Min(lo, h); hi = Mathf.Max(hi, h); });
            Site ridge = _sites.Find(x => x.kind == DestinationKind.RidgeOverlook);
            float best = float.MinValue;
            ShelterPlan chosen = null;
            for (float x = tPos.x + 45f; x < tPos.x + size.x - 45f; x += 6f)
            for (float z = tPos.z + 45f; z < tPos.z + size.z - 45f; z += 6f)
            {
                Vector3 p = TerrainUtil.Ground(new Vector3(x, 0f, z));
                if (TerrainUtil.FlatDistance(p, _loop.center) > _s.reserveReach + 20f) continue;
                float slope = TerrainUtil.SlopeDegrees(p);
                if (slope < 7f || slope > 24f || LoopRadius(p) < 1.3f) continue;
                float pd = PathDistance(p);
                if (pd < 40f || WaterDistance(p) < 22f || RunDistance(p) < 40f || InKeepOut(p)) continue;
                Vector3 f = TerrainUtil.Flat(TerrainUtil.SampleNormal(p));
                if (f.sqrMagnitude < 1e-4f) continue;
                f.Normalize();
                Vector3 r = Vector3.Cross(Vector3.up, f);
                if (TerrainUtil.SlopeDegrees(p + f * 2f) > 14f) continue;
                float rise = TerrainUtil.SampleHeight(p - f * ShelterPlan.Depth) - p.y;
                float lat = Mathf.Abs(TerrainUtil.SampleHeight(p + r * ShelterPlan.HalfWidth) - TerrainUtil.SampleHeight(p - r * ShelterPlan.HalfWidth));
                if (rise < 0.8f || rise > 3.6f || lat > 1.8f) continue;
                if (NearSite(p, 30f) || NearHiddenPlace(p, 60f)) continue;
                float view = 0f;
                foreach (float d in new[] { 40f, 80f, 120f }) view += (p.y - TerrainUtil.SampleHeight(p + f * d)) / 3f;
                float upland = (p.y - lo) / Mathf.Max(1f, hi - lo);
                float score = upland * 6f + Mathf.Min(view, 12f) * 0.4f + Mathf.Min(rise, 2.5f) * 0.8f - lat - Mathf.Max(0f, pd - 110f) * 0.05f;
                if (ridge != null)
                {
                    float dr = TerrainUtil.FlatDistance(p, ridge.centre);
                    if (dr > 50f && dr < 260f) score += 3f; // under the ridge
                }
                if (score > best) { best = score; chosen = new ShelterPlan { floor = p, forward = f, right = r }; }
            }
            if (chosen == null) { notes.Add("no Rock Shelter (no upland hillside to set a crag into, away from the paths)"); return null; }

            // The alcove floor: levelled inside the rock only (a cut, never a fill), so the roof's clearance holds.
            var data = _terrain.terrainData;
            int res = data.heightmapResolution;
            float cell = size.x / (res - 1);
            float floorH = chosen.floor.y;
            Vector3 c = chosen.floor;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((c.x - 7f - tPos.x) / cell), 0, res - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((c.x + 7f - tPos.x) / cell), 0, res - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((c.z - 7f - tPos.z) / cell), 0, res - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((c.z + 7f - tPos.z) / cell), 0, res - 1);
            int w = x1 - x0 + 1, h = z1 - z0 + 1;
            var heights = data.GetHeights(x0, z0, w, h);
            for (int zz = 0; zz < h; zz++)
            for (int xx = 0; xx < w; xx++)
            {
                var p = new Vector3(tPos.x + (x0 + xx) * cell, 0f, tPos.z + (z0 + zz) * cell);
                chosen.Local(p, out float lx, out float lz);
                if (Mathf.Abs(lx) >= 4.6f || lz <= -5f || lz >= 1.5f) continue;
                float target = floorH + 0.08f * Mathf.Clamp01(-lz / 3f) + Mathf.Max(0f, Mathf.Abs(lx) - 2.7f) * 1.2f
                               + Mathf.Max(0f, -lz - 3.1f) * 1.2f + Mathf.Max(0f, lz - 0.6f) * 0.5f;
                float cur = tPos.y + heights[zz, xx] * size.y;
                if (target < cur) heights[zz, xx] = Mathf.Clamp01((target - tPos.y) / size.y);
            }
            data.SetHeights(x0, z0, heights);
            _shelter = chosen;
            _newHidden.Add((chosen.floor, 7f));
            return chosen;
        }

        private void BuildRockShelter(Transform hidden, ShelterPlan s, List<string> notes)
        {
            Vector3 f = s.forward, r = s.right, p = s.floor;
            var group = PropBuilder.Group("Rock Shelter", hidden, p, Quaternion.LookRotation(f));
            Clear(p - f * 2f, 6.5f);

            var mesh = GeneratedAssets.Save(ProceduralMeshes.Crag(_s.seed, ShelterPlan.HalfWidth, ShelterPlan.Depth, ShelterPlan.Height, ShelterPlan.Bottom,
                ShelterPlan.AlcoveHalfWidth, ShelterPlan.AlcoveDepth, ShelterPlan.Ceiling), "Mesh_RockShelterCrag");
            var crag = new GameObject("Crag");
            crag.transform.SetParent(group, false);
            crag.AddComponent<MeshFilter>().sharedMesh = mesh;
            crag.AddComponent<MeshRenderer>().sharedMaterial = _stone[0]; // the scanned mossy rock surface where the art is in
            var cragCol = crag.AddComponent<MeshCollider>();
            cragCol.sharedMesh = mesh;
            AddFeature(crag, FeatureKind.Rock, ShelterPlan.HalfWidth, ShelterPlan.Height);
            MakeStatic(crag);
            Occupy(p);

            // The same bedrock showing on the hillside either side: scanned faces, set into the slope like the outcrops.
            for (int i = 0; i < 2 && _artOutcrops.Count > 0; i++)
            {
                float sgn = i == 0 ? -1f : 1f;
                Vector3 q = TerrainUtil.Ground(p + r * sgn * R(6.6f, 8.2f) - f * R(1f, 3f));
                if (InKeepOut(q) || PathDistance(q) < 6f || WaterDistance(q) < 5f) continue;
                var prefab = _artOutcrops[_rng.Next(_artOutcrops.Count)];
                var face = InstantiatePrefab(prefab, group, q, R(0.45f, 0.65f));
                var ef = face.GetComponent<EnvironmentFeature>();
                float k = face.transform.localScale.x / Mathf.Max(0.0001f, prefab.transform.localScale.x); // the scale it was given
                float hh = ef != null ? ef.height * k : 2.5f;
                face.transform.position = q - Vector3.up * hh * R(0.2f, 0.28f);
                face.transform.rotation = Quaternion.LookRotation(f) * Quaternion.Euler(0f, R(-20f, 20f), 0f);
                if (ef != null) { ef.radius *= k; ef.height *= k; }
                face.name = "Shelter Outcrop";
                MakeStatic(face);
            }

            // A hint of long-ago use, and nothing more: a ring of stones round a patch of old charcoal.
            Vector3 fire = TerrainUtil.Ground(p - f * 1.2f + r * 0.4f);
            var charcoal = GeneratedAssets.Material("Charcoal", new Color(0.07f, 0.065f, 0.06f), 0.05f);
            MeshPart("Old Fire", group, GeneratedAssets.LoadOrCreate("Mesh_PondDisc", () => ProceduralMeshes.Disc(48)), charcoal,
                group.InverseTransformPoint(fire + Vector3.up * 0.012f), new Vector3(0.42f, 1f, 0.42f), Quaternion.identity);
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2f + R(-0.2f, 0.2f);
                Vector3 sp = TerrainUtil.Ground(fire + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.52f);
                float sz = R(0.13f, 0.2f);
                MeshPart("Fire Stone", group, Pick(_rocks), Pick(_stone), group.InverseTransformPoint(sp - Vector3.up * sz * 0.2f),
                    new Vector3(sz, sz * 0.6f, sz * 0.9f), Quaternion.Euler(R(0f, 20f), R(0f, 360f), R(0f, 20f)));
            }
            // 1.10: the alcove is dry. In the crag's local frame (this group: origin at the floor, +Z out of the hill) the alcove runs x ±AlcoveHalfWidth,
            // z -AlcoveDepth..0, floor to Ceiling (the numbers ProceduralMeshes.Crag cut it from), pulled in 0.1 m for the rock's slight undulation.
            var rain = new GameObject("Rain Shelter");
            rain.transform.SetParent(group, false);
            var rs = rain.AddComponent<RainShelter>();
            rs.placeName = "Rock Shelter";
            rs.centre = new Vector3(0f, ShelterPlan.Ceiling * 0.5f, -ShelterPlan.AlcoveDepth * 0.5f);
            rs.size = new Vector3(ShelterPlan.AlcoveHalfWidth * 2f - 0.2f, ShelterPlan.Ceiling, ShelterPlan.AlcoveDepth - 0.1f);
            rs.feather = 0.6f;
            AddKeepOut(p - f * 1.4f, 2.8f); // a dry floor: no grass, nothing scattered under the roof

            // Prove it: measure the clear height under the roof from the floor, with the real collider.
            Physics.SyncTransforms();
            float clear = float.MaxValue;
            for (float x = -2f; x <= 2.01f; x += 0.5f)
                for (float z = -2.4f; z <= -0.19f; z += 0.4f)
                {
                    Vector3 o = TerrainUtil.Ground(p + r * x + f * z) + Vector3.up * 0.05f;
                    if (cragCol.Raycast(new Ray(o, Vector3.up), out RaycastHit hit, 8f)) clear = Mathf.Min(clear, hit.distance + 0.05f);
                }
            if (clear < float.MaxValue)
            {
                notes.Add($"Rock Shelter clear height {clear:0.00} m (designed ≥ 2.2)");
                if (clear < 1.95f) Debug.LogWarning($"[WildLens] Rock Shelter: only {clear:0.00} m clear under the roof (the player is 1.8 m). Please send a screenshot from inside.");
            }

            AddViewpoint(group, "Rock Shelter", p - f * 1.6f + Vector3.up * 1.6f, TerrainUtil.Ground(p + f * 60f) + Vector3.up * 2f);
            Discovery(hidden, p - f * 1.0f, "Rock Shelter", "An overhang in the rock, dry underfoot. Someone sheltered here long ago; the view hasn't changed.", 7f);
            Residence(hidden, "Rock shelter woods", TerrainUtil.Ground(p + f * 14f), 12f, "tufted_titmouse", "eastern_gray_squirrel");
            // The titmouse keeps to the woods all day (its rhythm sends it to the nearest woodland cover): this is that cover.
            Anchor(hidden, ScheduleRole.Woodland, TerrainUtil.Ground(p + f * 14f), 12f);
        }

        // ================================================================== Old Cabin Ruins

        private Vector3? PlanCabin(List<string> notes)
        {
            Vector3 tPos = _terrain.transform.position, size = _terrain.terrainData.size;
            float best = float.MinValue;
            Vector3? chosen = null;
            for (float x = tPos.x + 45f; x < tPos.x + size.x - 45f; x += 8f)
            for (float z = tPos.z + 45f; z < tPos.z + size.z - 45f; z += 8f)
            {
                Vector3 p = TerrainUtil.Ground(new Vector3(x, 0f, z));
                if (TerrainUtil.FlatDistance(p, _loop.center) > _s.reserveReach + 20f) continue;
                float slope = TerrainUtil.SlopeDegrees(p);
                if (slope > 7f || LoopRadius(p) < 1.3f) continue;
                float pd = PathDistance(p);
                if (pd < 40f || WaterDistance(p) < 25f || RunDistance(p) < 45f || InKeepOut(p)) continue;
                float f = Forest(p);
                if (f < 0.35f || f > 0.75f || MaxSlope(p, 7f) > 9f) continue;
                if (NearSite(p, 40f) || NearHiddenPlace(p, 60f)) continue;
                // At the woods' edge, findable: 45–100 m off a path is ideal.
                float score = -Mathf.Abs(f - 0.55f) * 10f - slope * 0.5f - Mathf.Max(0f, pd - 100f) * 0.05f - Mathf.Abs(pd - 60f) * 0.02f;
                if (score > best) { best = score; chosen = p; }
            }
            if (!chosen.HasValue) { notes.Add("no Old Cabin Ruins (no level ground at a woods' edge away from the paths)"); return null; }
            _newHidden.Add((chosen.Value, 8f));
            return chosen;
        }

        private void BuildCabinRuins(Transform hidden, Vector3 c)
        {
            Vector3 down = TerrainUtil.Flat(TerrainUtil.SampleNormal(c));
            Vector3 along = down.sqrMagnitude > 1e-4f ? Vector3.Cross(Vector3.up, down.normalized) : Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward;
            along = Quaternion.Euler(0f, R(-12f, 12f), 0f) * along.normalized; // the long walls follow the contour
            Vector3 across = Vector3.Cross(Vector3.up, along);
            var group = PropBuilder.Group("Old Cabin Ruins", hidden, c, Quaternion.LookRotation(across));
            Clear(c, 6.5f);
            var stoneMats = _stone;
            const float L = 5f, W = 4f;

            // The foundation: one or two courses of fieldstone, with gaps where stones have gone.
            void Wall(Vector3 a, Vector3 b)
            {
                float len = TerrainUtil.FlatDistance(a, b);
                Vector3 dir = TerrainUtil.Flat(b - a).normalized;
                int count = Mathf.Max(2, Mathf.RoundToInt(len / 0.5f));
                float top = float.MinValue, low = float.MaxValue;
                for (int i = 0; i <= count; i++)
                {
                    if (R() < 0.18f) continue;
                    Vector3 p = TerrainUtil.Ground(Vector3.Lerp(a, b, i / (float)count) + Vector3.Cross(Vector3.up, dir) * R(-0.08f, 0.08f));
                    var sz = new Vector3(R(0.42f, 0.6f), R(0.26f, 0.36f), R(0.34f, 0.48f));
                    var rot = Quaternion.LookRotation(dir) * Quaternion.Euler(R(-6f, 6f), R(-10f, 10f), R(-6f, 6f));
                    var stone = MeshPart("Foundation Stone", group, Pick(_rocks), stoneMats[_rng.Next(stoneMats.Length)], Vector3.zero, sz, rot);
                    stone.transform.SetPositionAndRotation(p + Vector3.up * sz.y * 0.28f, rot); // bedded, not perched
                    low = Mathf.Min(low, p.y); top = Mathf.Max(top, p.y + sz.y * 0.75f);
                    if (R() < 0.4f)
                    {
                        var sz2 = sz * R(0.7f, 0.9f);
                        var upper = MeshPart("Foundation Stone", group, Pick(_rocks), stoneMats[_rng.Next(stoneMats.Length)], Vector3.zero, sz2, rot * Quaternion.Euler(0f, R(-20f, 20f), 0f));
                        upper.transform.SetPositionAndRotation(p + Vector3.up * (sz.y * 0.62f + sz2.y * 0.3f), rot * Quaternion.Euler(0f, R(-20f, 20f), 0f));
                        top = Mathf.Max(top, p.y + sz.y * 0.62f + sz2.y * 0.7f);
                    }
                }
                // Low enough to step over (step ≤ 0.4 m), solid enough not to walk through.
                if (top > float.MinValue)
                {
                    var col = new GameObject("Wall Collider");
                    col.transform.SetParent(group, false);
                    col.transform.SetPositionAndRotation((a + b) * 0.5f, Quaternion.LookRotation(dir));
                    var box = col.AddComponent<BoxCollider>();
                    float hgt = Mathf.Min(0.38f, top - low);
                    col.transform.position = new Vector3(col.transform.position.x, low, col.transform.position.z);
                    box.center = new Vector3(0f, hgt * 0.5f, 0f);
                    box.size = new Vector3(0.45f, hgt, len);
                }
            }
            Vector3 c0 = c - along * (L * 0.5f) - across * (W * 0.5f), c1 = c + along * (L * 0.5f) - across * (W * 0.5f);
            Vector3 c2 = c + along * (L * 0.5f) + across * (W * 0.5f), c3 = c - along * (L * 0.5f) + across * (W * 0.5f);
            Wall(c0, c1);
            Wall(c1, c2);
            Wall(c2, c3);
            Wall(c3, Vector3.Lerp(c3, c0, 0.35f)); // the doorway side: a gap where the door stood
            Wall(Vector3.Lerp(c3, c0, 0.62f), c0);

            // The chimney stub at the far gable: stacked stone, grounded at its lowest corner, falling away at the top.
            Vector3 chim = c + along * (L * 0.5f + 0.55f);
            float baseY = float.MaxValue;
            foreach (var o in new[] { new Vector3(-0.65f, 0f, -0.5f), new Vector3(0.65f, 0f, -0.5f), new Vector3(-0.65f, 0f, 0.5f), new Vector3(0.65f, 0f, 0.5f) })
                baseY = Mathf.Min(baseY, TerrainUtil.SampleHeight(chim + across * o.x + along * o.z));
            var chimney = PropBuilder.Group("Chimney", group, new Vector3(chim.x, baseY - 0.08f, chim.z), Quaternion.LookRotation(along));
            for (int layer = 0; layer < 6; layer++)
            {
                int stones = layer < 4 ? 5 : 3;
                for (int i = 0; i < stones; i++)
                {
                    if (layer >= 4 && R() < 0.4f) continue; // the top has fallen
                    float a = i / (float)stones * Mathf.PI * 2f + layer * 0.6f;
                    var lp = new Vector3(Mathf.Cos(a) * 0.45f, 0.14f + layer * 0.27f, Mathf.Sin(a) * 0.36f);
                    var sz = new Vector3(R(0.4f, 0.55f), R(0.24f, 0.3f), R(0.34f, 0.45f));
                    MeshPart("Chimney Stone", chimney, Pick(_rocks), stoneMats[_rng.Next(stoneMats.Length)], lp, sz, Quaternion.Euler(R(-5f, 5f), R(0f, 360f), R(-5f, 5f)));
                }
            }
            PropBuilder.AddCollider(chimney, new Vector3(0f, 0.8f, 0f), new Vector3(1.3f, 1.6f, 1.0f));
            AddKeepOut(chim, 1.2f);
            Occupy(chim);

            // Rotted sill logs along the long walls, inside: bedded at both ends, tilted with the ground.
            var bark = Pick(_bark);
            foreach (float side in new[] { -1f, 1f })
            {
                float len = side < 0f ? 4.4f : 3.6f, rad = R(0.15f, 0.19f);
                Vector3 mid = c + across * side * (W * 0.5f - 0.45f) + along * R(-0.3f, 0.3f);
                Vector3 e1 = mid - along * len * 0.5f, e2 = mid + along * len * 0.5f;
                float g1 = TerrainUtil.SampleHeight(e1), g2 = TerrainUtil.SampleHeight(e2);
                var log = new GameObject("Fallen Log"); // a sill gone to rot: small life finds it like any fallen log
                log.transform.SetParent(group, false);
                log.transform.position = new Vector3(mid.x, (g1 + g2) * 0.5f + rad * 0.35f, mid.z);
                log.transform.rotation = Quaternion.LookRotation(along) * Quaternion.Euler(-Mathf.Atan2(g2 - g1, len) * Mathf.Rad2Deg, 0f, 0f);
                MeshPart("Trunk", log.transform, _cylinder, bark, Vector3.zero, new Vector3(rad * 2f, len * 0.5f, rad * 2f), Quaternion.Euler(90f, 0f, R(-4f, 4f)));
                var col = log.AddComponent<CapsuleCollider>();
                col.direction = 2; col.height = len; col.radius = rad;
                AddFeature(log, FeatureKind.Log, len * 0.5f, rad * 2f);
                MakeStatic(log);
            }

            // Taken back by the woods: a young tree inside the walls, two at the edge, ferns in the corners.
            for (int i = 0; i < 3; i++)
            {
                Vector3 tp = i == 0 ? TerrainUtil.Ground(c + along * R(-1.2f, 0.4f) + across * R(-0.6f, 0.6f))
                                    : TerrainUtil.Ground(c + Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward * R(4.2f, 6f));
                if (InKeepOut(tp) || PathDistance(tp) < 3f) continue;
                var tree = i == 1 ? Conifer(group, tp) : Deciduous(group, tp);
                float k = R(0.4f, 0.6f);
                tree.transform.localScale *= k;
                tree.name = "Young " + tree.name;
                var fe = tree.GetComponent<EnvironmentFeature>();
                if (fe != null) { fe.radius *= k; fe.height *= k; }
                MakeStaticExceptSway(tree);
                AddDistanceCull(tree, 0.012f);
                _treeObjects.Add(tree);
                Occupy(tp);
            }
            if (_art && _artFerns.Count > 0)
                for (int i = 0; i < 22; i++)
                {
                    Vector3 fp = TerrainUtil.Ground(c + Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward * Mathf.Sqrt(R(0.02f, 1f)) * 6.5f);
                    if (InKeepOut(fp)) continue;
                    PlaceArt(_artFerns, group, fp, R(0.75f, 1.25f), 0.02f, 8f);
                }

            // The last of a split-rail fence: three leaning posts and a fallen rail. The bluebird's perch.
            var postMat = GeneratedAssets.Material("WoodDark", new Color(0.33f, 0.24f, 0.16f));
            var weathered = GeneratedAssets.Material("WeatheredRail", new Color(0.5f, 0.46f, 0.4f));
            Vector3 fenceStart = c + across * (W * 0.5f + 3.6f) - along * 2.4f;
            Vector3 fenceDir = Quaternion.Euler(0f, R(-15f, 15f), 0f) * along;
            Vector3 fenceMid = fenceStart + fenceDir * 2.4f;
            for (int i = 0; i < 3; i++)
            {
                Vector3 fp = TerrainUtil.Ground(fenceStart + fenceDir * (i * 2.4f + R(-0.2f, 0.2f)));
                if (InKeepOut(fp) || PathDistance(fp) < 2f) continue;
                float hgt = R(1.1f, 1.3f);
                var fpost = PropBuilder.Group("Fence Post", group, fp, Quaternion.LookRotation(fenceDir) * Quaternion.Euler(R(-7f, 7f), R(-20f, 20f), R(-7f, 7f)));
                PropBuilder.Box("Post", fpost, new Vector3(0f, hgt * 0.5f - 0.15f, 0f), new Vector3(0.13f, hgt + 0.3f, 0.11f), i == 1 ? weathered : postMat);
                PropBuilder.AddCollider(fpost, new Vector3(0f, hgt * 0.5f, 0f), new Vector3(0.14f, hgt, 0.14f));
                var fe = fpost.gameObject.AddComponent<EnvironmentFeature>();
                fe.kind = FeatureKind.Rock; // a perch for small birds (rocks over 0.8 m are perches), and an obstacle
                fe.radius = 0.1f;
                fe.height = hgt;
            }
            var rail = PropBuilder.Group("Fallen Rail", group, TerrainUtil.Ground(fenceMid + Vector3.Cross(Vector3.up, fenceDir) * 0.6f),
                Quaternion.LookRotation(Quaternion.Euler(0f, R(-12f, 12f), 0f) * fenceDir));
            PropBuilder.Box("Rail", rail, new Vector3(0f, 0.05f, 0f), new Vector3(0.12f, 0.1f, 2.6f), weathered);

            AddHotspot(group, "Fence Perch", HotspotType.Perch, fenceMid, 4f);
            // The clearing itself, a little meadow: by day the bluebird's rhythm sends it to the nearest meadow (its edge nearest the
            // fence is a metre or two from the posts, its middle ~6 m), and the cottontails come out to it at dawn and dusk. Small,
            // so it is a clearing, not a pasture: a deer wandering by may graze it.
            AddHotspot(group, "Cabin Clearing Meadow", HotspotType.Meadow, TerrainUtil.Ground(c), 6f);
            Residence(hidden, "Cabin fence", TerrainUtil.Ground(fenceMid), 6f, "eastern_bluebird");
            Residence(hidden, "Cabin clearing", TerrainUtil.Ground(c - across * 5f), 10f, "eastern_cottontail", "groundhog");
            AddViewpoint(group, "Old Cabin Ruins", TerrainUtil.Ground(c - across * 7.5f - along * 3f) + Vector3.up * 1.6f, c + Vector3.up * 0.8f);
            Discovery(hidden, c, "Old Cabin Ruins", "Fieldstone and a chimney, all that's left of a homestead. The woods are taking it back; a bluebird keeps the fence.", 8f);
        }

        // ================================================================== for the terrain painter

        /// <summary>Bare, dry ground (0..1): the shelter's floor under its roof.</summary>
        private float GeographyBare(Vector3 p)
        {
            if (_shelter == null) return 0f;
            _shelter.Local(p, out float x, out float z);
            if (z > 1f || z < -3.4f || Mathf.Abs(x) > 3.2f) return 0f;
            return (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.2f, 3.2f, Mathf.Abs(x)))) *
                   (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 1f, z)));
        }

        /// <summary>No terrain grass here: the Hidden Pond's water and the shelter's floor.</summary>
        private bool GeographyNoGrass(Vector3 p)
        {
            if (_hiddenPond != null)
            {
                float d = TerrainUtil.FlatDistance(p, _hiddenPond.centre);
                if (d < _hiddenPond.radius * 1.5f && d < HiddenPondRadius(_hiddenPond, p) + 0.4f) return true;
            }
            return GeographyBare(p) > 0.4f;
        }
    }
}
