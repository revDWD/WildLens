using System.Collections.Generic;
using UnityEngine;
using WildLens.Core;
using WildLens.Environment;

namespace WildLens.EditorTools
{
    /// <summary>Generates the closed trail loop on the terrain and its visible path mesh.</summary>
    public static class TrailBuilder
    {
        public struct Loop
        {
            public Vector3 center;
            public float radiusX, radiusZ;
            public List<Vector3> points;
        }

        public static Loop Generate(Terrain terrain, SliceBuildSettings s)
        {
            Vector3 tPos = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            float margin = 12f;
            float maxRz = (Mathf.Min(size.x / s.trailAspect, size.z) * 0.5f) - margin - s.scatterMargin * 0.25f;
            float rz = Mathf.Clamp(s.trailRadius, 25f, Mathf.Max(25f, maxRz));
            float rx = rz * s.trailAspect;

            Vector3 terrainCenter = tPos + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
            Vector3 center = terrainCenter;

            if (s.autoPlaceOnGentleGround)
            {
                // Try a grid of centres that keep the loop on the terrain; keep the gentlest.
                float bestCost = float.MaxValue;
                float freeX = Mathf.Max(0f, size.x * 0.5f - rx - margin);
                float freeZ = Mathf.Max(0f, size.z * 0.5f - rz - margin);
                for (int gx = -2; gx <= 2; gx++)
                for (int gz = -2; gz <= 2; gz++)
                {
                    Vector3 c = terrainCenter + new Vector3(freeX * gx / 2f, 0f, freeZ * gz / 2f);
                    float cost = LoopCost(c, rx, rz) + (Mathf.Abs(gx) + Mathf.Abs(gz)) * 0.5f; // slight preference for centre
                    if (cost < bestCost) { bestCost = cost; center = c; }
                }
            }

            var rng = new System.Random(s.seed);
            float o1 = (float)rng.NextDouble() * 100f, o2 = (float)rng.NextDouble() * 100f;
            const int count = 72;
            var pts = new List<Vector3>(count);
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                float wobble = 1f + (Mathf.PerlinNoise(o1 + ca * 1.1f, o1 + sa * 1.1f) - 0.5f) * 0.34f
                                  + (Mathf.PerlinNoise(o2 + ca * 3f, o2 + sa * 3f) - 0.5f) * 0.1f;
                pts.Add(center + new Vector3(ca * rx * wobble, 0f, sa * rz * wobble));
            }
            for (int pass = 0; pass < 2; pass++) pts = Smooth(pts);
            for (int i = 0; i < pts.Count; i++) pts[i] = TerrainUtil.Ground(pts[i]);

            return new Loop { center = TerrainUtil.Ground(center), radiusX = rx, radiusZ = rz, points = pts };
        }

        private static float LoopCost(Vector3 c, float rx, float rz)
        {
            float slope = 0f, minH = float.MaxValue, maxH = float.MinValue;
            const int n = 32;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                Vector3 p = c + new Vector3(Mathf.Cos(a) * rx, 0f, Mathf.Sin(a) * rz);
                slope += TerrainUtil.SlopeDegrees(p);
                float h = TerrainUtil.SampleHeight(p);
                minH = Mathf.Min(minH, h); maxH = Mathf.Max(maxH, h);
            }
            return slope / n + (maxH - minH) * 0.3f;
        }

        private static List<Vector3> Smooth(List<Vector3> p)
        {
            var r = new List<Vector3>(p.Count);
            for (int i = 0; i < p.Count; i++)
                r.Add((p[(i - 1 + p.Count) % p.Count] + p[i] * 2f + p[(i + 1) % p.Count]) * 0.25f);
            return r;
        }

        public static TrailPath CreateTrailObject(Loop loop, SliceBuildSettings s, Transform parent)
        {
            var go = new GameObject("Trail");
            go.transform.SetParent(parent, false);
            var path = go.AddComponent<TrailPath>();
            path.SetPath(loop.points, s.trailWidth, s.markerSpacing);

            var dirtTex = GeneratedAssets.LoadOrCreate("T_TrailDirt", () => GeneratedAssets.NoiseTexture(128,
                new Color(0.42f, 0.34f, 0.24f), new Color(0.55f, 0.46f, 0.33f), 6f, 3, 0.04f, new Color(0.7f, 0.66f, 0.58f)));
            var mat = GeneratedAssets.Material("TrailDirt", Color.white, 0.05f, dirtTex);

            var mesh = GeneratedAssets.Save(ProceduralMeshes.TrailRibbon(loop.points, s.trailWidth, 0.6f), "Mesh_TrailRibbon");
            var surface = new GameObject("Trail Surface");
            surface.transform.SetParent(go.transform, false);
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = surface.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return path;
        }
    }
}
