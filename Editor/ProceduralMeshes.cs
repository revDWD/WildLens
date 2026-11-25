using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using WildLens.Core;

namespace WildLens.EditorTools
{
    /// <summary>Simple procedural meshes for placeholder scenery.</summary>
    public static class ProceduralMeshes
    {
        /// <summary>Unit cone: base radius 1 at y=0, tip at y=1, with a bottom cap.</summary>
        public static Mesh Cone(int segments = 12)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < segments; i++)
            {
                float a0 = i / (float)segments * Mathf.PI * 2f, a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
                Vector3 p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                Vector3 p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                int b = verts.Count;
                verts.Add(p0); verts.Add(Vector3.up); verts.Add(p1);
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                int c = verts.Count;
                verts.Add(Vector3.zero); verts.Add(p0); verts.Add(p1);
                tris.Add(c); tris.Add(c + 1); tris.Add(c + 2);
            }
            var m = new Mesh { name = "Cone" };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// Noise-displaced sphere (radius ~0.5). Used for rocks (strong, flattened base) and foliage blobs (soft).
        /// Displacement depends only on vertex position so UV seams stay closed.
        /// </summary>
        public static Mesh Blob(int seed, float strength, float frequency, bool flattenBase)
        {
            var src = PrimitiveMeshes.Get(PrimitiveType.Sphere);
            var verts = src.vertices;
            float o = seed * 7.31f;
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 n = verts[i].normalized;
                float noise = Noise3(n * frequency + Vector3.one * o);
                Vector3 v = n * (0.5f * (1f + (noise - 0.5f) * 2f * strength));
                if (flattenBase && v.y < -0.18f) v.y = Mathf.Lerp(v.y, -0.18f, 0.8f);
                verts[i] = v;
            }
            var m = new Mesh { name = "Blob" };
            m.vertices = verts;
            m.triangles = src.triangles;
            m.uv = src.uv;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static float Noise3(Vector3 p)
        {
            float xy = Mathf.PerlinNoise(p.x, p.y);
            float yz = Mathf.PerlinNoise(p.y + 31.4f, p.z);
            float zx = Mathf.PerlinNoise(p.z + 71.9f, p.x);
            return (xy + yz + zx) / 3f;
        }

        /// <summary>Terrain-hugging ribbon along a closed loop.</summary>
        public static Mesh TrailRibbon(IReadOnlyList<Vector3> loop, float width, float sampleSpacing)
        {
            // Resample the loop at even spacing.
            var samples = new List<Vector3>();
            float carry = 0f;
            for (int i = 0; i < loop.Count; i++)
            {
                Vector3 a = loop[i], b = loop[(i + 1) % loop.Count];
                float len = Vector3.Distance(a, b);
                float d = carry;
                while (d < len)
                {
                    samples.Add(Vector3.Lerp(a, b, d / len));
                    d += sampleSpacing;
                }
                carry = d - len;
            }

            float[] offsets = { -0.5f, -0.25f, 0f, 0.25f, 0.5f };
            float[] lift = { 0.015f, 0.05f, 0.06f, 0.05f, 0.015f };
            int cols = offsets.Length;
            int n = samples.Count;
            var verts = new Vector3[(n + 1) * cols];
            var uvs = new Vector2[verts.Length];
            var tris = new List<int>(n * (cols - 1) * 6);
            float distance = 0f;

            for (int i = 0; i <= n; i++)
            {
                Vector3 p = samples[i % n];
                Vector3 prev = samples[(i - 1 + n) % n], next = samples[(i + 1) % n];
                Vector3 tangent = TerrainUtil.Flat(next - prev).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, tangent);
                if (i > 0) distance += Vector3.Distance(samples[(i - 1) % n], p);
                for (int c = 0; c < cols; c++)
                {
                    Vector3 v = p + right * (offsets[c] * width);
                    v.y = TerrainUtil.SampleHeight(v) + lift[c];
                    verts[i * cols + c] = v;
                    uvs[i * cols + c] = new Vector2(offsets[c] + 0.5f, distance / 2f);
                }
            }
            for (int i = 0; i < n; i++)
            for (int c = 0; c < cols - 1; c++)
            {
                int a = i * cols + c, b = a + 1, d = a + cols, e = d + 1;
                tris.Add(a); tris.Add(d); tris.Add(b);
                tris.Add(b); tris.Add(d); tris.Add(e);
            }

            var m = new Mesh { name = "TrailRibbon" };
            if (verts.Length > 65000) m.indexFormat = IndexFormat.UInt32;
            m.vertices = verts;
            m.uv = uvs;
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
