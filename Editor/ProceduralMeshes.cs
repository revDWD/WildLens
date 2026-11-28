using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using WildLens.Core;

namespace WildLens.EditorTools
{
    /// <summary>Simple procedural meshes for placeholder scenery.</summary>
    public static class ProceduralMeshes
    {
        /// <summary>Unit cone: base radius 1 at y=0, tip at y=1, optionally with a bottom cap.</summary>
        public static Mesh Cone(int segments = 12, bool cap = true)
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
                if (!cap) continue;
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

        /// <summary>
        /// Tapered tube for trunks and limbs (0.9): base radius 1 at y = 0, <paramref name="topRatio"/> at y = 1, no caps,
        /// cylindrical UVs (bark wraps twice around, repeats along the length).
        /// </summary>
        public static Mesh Frustum(int segments, float topRatio)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int ring = 0; ring <= 1; ring++)
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f, r = ring == 0 ? 1f : topRatio;
                verts.Add(new Vector3(Mathf.Cos(a) * r, ring, Mathf.Sin(a) * r));
                uvs.Add(new Vector2(i / (float)segments * 2f, ring * 3f));
            }
            int row = segments + 1;
            for (int i = 0; i < segments; i++)
            {
                int a = i, b = i + 1, c = row + i, d = row + i + 1;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
            var m = new Mesh { name = "Frustum" };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Flat unit disc (radius 1) in the XZ plane, facing up. Used for pond water.</summary>
        public static Mesh Disc(int segments)
        {
            var verts = new List<Vector3> { Vector3.zero };
            var uvs = new List<Vector2> { new Vector2(0.5f, 0.5f) };
            var tris = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
                uvs.Add(new Vector2(Mathf.Cos(a) * 0.5f + 0.5f, Mathf.Sin(a) * 0.5f + 0.5f));
                if (i > 0) { tris.Add(0); tris.Add(i + 1); tris.Add(i); }
            }
            var m = new Mesh { name = "Disc" };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// 1.9: a block of rock with an undercut alcove in its front: the Rock Shelter. Local frame: +Z out of the hill, the
        /// front face at z = 0, the back at z = -depth; y = 0 is the shelter floor, the block runs from <paramref name="bottom"/>
        /// (buried) to <paramref name="height"/>. Front-face vertices with |x| ≤ alcoveHalfWidth and y ≤ ceiling are pushed back
        /// to z = -alcoveDepth, so the face itself forms the alcove's back wall, sides and roof: the lowest point of the roof is
        /// exactly <paramref name="ceiling"/> (no noise there in y), which is what the clearance is designed from. Every face is a
        /// grid displaced by a noise function of the undisplaced position, so faces meet without cracks. Box-projected UVs at 2.5 m.
        /// </summary>
        public static Mesh Crag(int seed, float halfWidth, float depth, float height, float bottom,
            float alcoveHalfWidth, float alcoveDepth, float ceiling)
        {
            float o = seed * 3.77f;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float[] xs = { -halfWidth, -halfWidth * 0.82f, alcoveHalfWidth * -1.16f, -alcoveHalfWidth, -alcoveHalfWidth * 0.5f, 0f,
                           alcoveHalfWidth * 0.5f, alcoveHalfWidth, alcoveHalfWidth * 1.16f, halfWidth * 0.82f, halfWidth };
            float[] ys = { bottom, 0f, ceiling * 0.35f, ceiling * 0.7f, ceiling, ceiling + 0.4f, (ceiling + 0.4f + height) * 0.5f, height };
            float[] zs = { -depth, -depth * 0.75f, -depth * 0.5f, -depth * 0.25f, 0f };

            Vector3 Displace(Vector3 p, bool alcove)
            {
                float nx = Mathf.PerlinNoise(o + p.y * 0.55f, o + p.z * 0.55f + 11f) - 0.5f;
                float ny = Mathf.PerlinNoise(o + p.x * 0.55f + 23f, o + p.z * 0.55f) - 0.5f;
                float nz = Mathf.PerlinNoise(o + p.x * 0.55f, o + p.y * 0.55f + 37f) - 0.5f;
                Vector3 d = new Vector3(nx, ny, nz) * 0.9f;
                // The top lumps more; the base and the alcove stay true (clearance and footing are designed, not noise).
                if (p.y >= height - 0.01f) d.y += (Mathf.PerlinNoise(o + p.x * 0.3f, o + p.z * 0.3f) - 0.5f) * 1.2f;
                if (alcove) d = new Vector3(d.x * 0.15f, 0f, d.z * 0.25f);
                if (p.y <= bottom + 0.01f) d.y = 0f;
                return p + d;
            }
            bool InAlcove(float x, float y) => Mathf.Abs(x) <= alcoveHalfWidth + 0.001f && y <= ceiling + 0.001f;

            // One face: a grid over (a, b) axes; position(i, j) gives the undisplaced point; uv from two world axes.
            void Face(int na, int nb, System.Func<int, int, Vector3> at, System.Func<int, int, bool> alcove, int uvA, int uvB, bool flip)
            {
                int start = verts.Count;
                for (int j = 0; j < nb; j++)
                for (int i = 0; i < na; i++)
                {
                    Vector3 p = at(i, j);
                    bool al = alcove != null && alcove(i, j);
                    Vector3 v = Displace(p, al);
                    verts.Add(v);
                    uvs.Add(new Vector2(v[uvA], v[uvB]) / 2.5f + new Vector2(seed * 0.13f, seed * 0.29f));
                }
                for (int j = 0; j < nb - 1; j++)
                for (int i = 0; i < na - 1; i++)
                {
                    int a = start + j * na + i, b = a + 1, c = a + na, d = c + 1;
                    if (flip) { tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(b); tris.Add(d); tris.Add(c); }
                    else { tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d); }
                }
            }

            // Front (faces +Z) with the alcove pushed back.
            Face(xs.Length, ys.Length, (i, j) =>
            {
                float x = xs[i], y = ys[j];
                return new Vector3(x, y, InAlcove(x, y) ? -alcoveDepth : 0f);
            }, (i, j) => InAlcove(xs[i], ys[j]), 0, 1, true);
            // Back (faces -Z).
            Face(xs.Length, ys.Length, (i, j) => new Vector3(xs[i], ys[j], -depth), null, 0, 1, false);
            // Left (-X) and right (+X).
            Face(zs.Length, ys.Length, (i, j) => new Vector3(-halfWidth, ys[j], zs[i]), null, 2, 1, true);
            Face(zs.Length, ys.Length, (i, j) => new Vector3(halfWidth, ys[j], zs[i]), null, 2, 1, false);
            // Top (+Y) and bottom (-Y).
            Face(xs.Length, zs.Length, (i, j) => new Vector3(xs[i], height, zs[j]), null, 0, 2, false);
            Face(xs.Length, zs.Length, (i, j) => new Vector3(xs[i], bottom, zs[j]), null, 0, 2, true);

            var m = new Mesh { name = "Crag" };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        /// <summary>Terrain-hugging ribbon along a closed loop, or an open path when <paramref name="closed"/> is false.</summary>
        public static Mesh TrailRibbon(IReadOnlyList<Vector3> loop, float width, float sampleSpacing, bool closed = true)
        {
            // Resample the path at even spacing.
            var samples = new List<Vector3>();
            float carry = 0f;
            int segments = closed ? loop.Count : loop.Count - 1;
            for (int i = 0; i < segments; i++)
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
            if (!closed && loop.Count > 0) samples.Add(loop[loop.Count - 1]);

            float[] offsets = { -0.5f, -0.25f, 0f, 0.25f, 0.5f };
            float[] lift = { 0.015f, 0.05f, 0.06f, 0.05f, 0.015f };
            int cols = offsets.Length;
            int n = samples.Count;
            var verts = new Vector3[(closed ? n + 1 : n) * cols];
            var uvs = new Vector2[verts.Length];
            var tris = new List<int>(n * (cols - 1) * 6);
            float distance = 0f;

            int rows = closed ? n + 1 : n;
            for (int i = 0; i < rows; i++)
            {
                Vector3 p = samples[i % n];
                Vector3 prev = closed ? samples[(i - 1 + n) % n] : samples[Mathf.Max(0, i - 1)];
                Vector3 next = closed ? samples[(i + 1) % n] : samples[Mathf.Min(n - 1, i + 1)];
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
            for (int i = 0; i < rows - 1; i++)
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
