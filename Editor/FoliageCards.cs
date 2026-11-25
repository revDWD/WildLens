using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WildLens.EditorTools
{
    /// <summary>
    /// Alpha-cut foliage cards for the procedural trees (0.6). Silhouettes made of leaf and branch cards read as
    /// foliage; smooth blobs and cones read as placeholders.
    ///
    /// Textures (Art~/Foliage, composed from CC0 Poly Haven leaf and fir-twig atlases):
    ///   leaf_cluster_rgba.png    a dense, top-lit cluster of broadleaf leaves
    ///   conifer_branch_rgba.png  a fir branch spray, base at the bottom of the texture, tip at the top
    ///
    /// Meshes (a handful of variants, shared by every tree, stored in Generated/FoliageCardMeshes.asset):
    ///   Canopy  ~40 cards scattered over a unit sphere (matches the canopy blob it is parented to), facing outward
    ///   Conifer 13 drooping branch cards radiating around a unit cone tier, from the trunk out past the cone edge
    /// Vertex normals are bent outward from the crown centre so the foliage lights as a soft volume instead of
    /// showing flat card faces (two-sided cards otherwise light inconsistently).
    /// </summary>
    public static class FoliageCards
    {
        private const string MeshAsset = GeneratedAssets.Folder + "/FoliageCardMeshes.asset";
        private const int Variants = 6;

        private static Mesh[] _canopy, _conifer;

        public static bool Available =>
            ArtLibrary.Texture("Foliage/leaf_cluster_rgba.png") != null && ArtLibrary.Texture("Foliage/conifer_branch_rgba.png") != null;

        public static Material LeafMaterial(string key, Color tint) =>
            ArtLibrary.AlphaCutMaterial("Leaves_" + key, ArtLibrary.Texture("Foliage/leaf_cluster_rgba.png"), null, tint);

        public static Material BranchMaterial(string key, Color tint) =>
            ArtLibrary.AlphaCutMaterial("Branches_" + key, ArtLibrary.Texture("Foliage/conifer_branch_rgba.png"), null, tint);

        public static Mesh Canopy(System.Random rng) { Ensure(); return _canopy[rng.Next(_canopy.Length)]; }
        public static Mesh ConiferTier(System.Random rng) { Ensure(); return _conifer[rng.Next(_conifer.Length)]; }

        private static void Ensure()
        {
            if (_canopy != null && _canopy[0] != null) return;
            AssetDatabase.DeleteAsset(MeshAsset);
            _canopy = new Mesh[Variants];
            _conifer = new Mesh[Variants];
            Object container = null;
            for (int i = 0; i < Variants; i++)
            {
                _canopy[i] = BuildCanopy(new System.Random(100 + i), $"CanopyCards_{i}");
                _conifer[i] = BuildConifer(new System.Random(200 + i), $"ConiferCards_{i}");
                foreach (var m in new[] { _canopy[i], _conifer[i] })
                {
                    if (container == null) { AssetDatabase.CreateAsset(m, MeshAsset); container = m; }
                    else AssetDatabase.AddObjectToAsset(m, container);
                }
            }
        }

        private static float R(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

        private static Vector3 RandomDirection(System.Random rng)
        {
            float z = R(rng, -0.55f, 1f); // favour the upper hemisphere: crowns are lit and seen from below-side
            float a = R(rng, 0f, Mathf.PI * 2f);
            float r = Mathf.Sqrt(1f - z * z);
            return new Vector3(Mathf.Cos(a) * r, z, Mathf.Sin(a) * r);
        }

        private static Mesh BuildCanopy(System.Random rng, string name)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            Vector3 centre = new Vector3(0f, -0.12f, 0f);
            for (int c = 0; c < 40; c++)
            {
                Vector3 dir = RandomDirection(rng);
                Vector3 pos = dir * R(rng, 0.4f, 0.56f); // canopy blob radius is ~0.5
                Vector3 normal = (dir + new Vector3(R(rng, -0.5f, 0.5f), R(rng, -0.2f, 0.4f), R(rng, -0.5f, 0.5f))).normalized;
                Vector3 tangent = Vector3.Cross(normal, Mathf.Abs(normal.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                tangent = Quaternion.AngleAxis(R(rng, 0f, 360f), normal) * tangent;
                Vector3 bitangent = Vector3.Cross(normal, tangent);
                float s = R(rng, 0.16f, 0.24f);
                AddQuad(v, n, uv, t, pos, tangent * s, bitangent * s, centre);
            }
            return Finish(name, v, n, uv, t);
        }

        private static Mesh BuildConifer(System.Random rng, string name)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            int branches = 13;
            for (int b = 0; b < branches; b++)
            {
                float a = (b + R(rng, -0.3f, 0.3f)) / branches * Mathf.PI * 2f;
                Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 side = Vector3.Cross(Vector3.up, radial);
                // Unit cone surface is y = 1 - r. Each card runs just above it from near the tip, out and drooping
                // past the cone's edge, so the dark core shows only in the gaps.
                Vector3 inner = radial * 0.12f + Vector3.up * R(rng, 0.9f, 0.98f);
                Vector3 outer = radial * R(rng, 1.08f, 1.22f) + Vector3.up * R(rng, -0.08f, 0.04f);
                float wIn = 0.1f, wOut = R(rng, 0.38f, 0.5f);
                Vector3 up = (Vector3.up * 0.8f + radial * 0.6f).normalized;
                int i0 = v.Count;
                v.Add(inner - side * wIn); v.Add(inner + side * wIn); v.Add(outer + side * wOut); v.Add(outer - side * wOut);
                for (int k = 0; k < 4; k++) n.Add(up);
                uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(1f, 0f)); uv.Add(new Vector2(1f, 1f)); uv.Add(new Vector2(0f, 1f));
                t.Add(i0); t.Add(i0 + 2); t.Add(i0 + 1); t.Add(i0); t.Add(i0 + 3); t.Add(i0 + 2);
            }
            return Finish(name, v, n, uv, t);
        }

        private static void AddQuad(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t,
            Vector3 c, Vector3 du, Vector3 dv, Vector3 normalCentre)
        {
            int i0 = v.Count;
            v.Add(c - du - dv); v.Add(c + du - dv); v.Add(c + du + dv); v.Add(c - du + dv);
            for (int k = 0; k < 4; k++) n.Add((v[i0 + k] - normalCentre).normalized);
            uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(1f, 0f)); uv.Add(new Vector2(1f, 1f)); uv.Add(new Vector2(0f, 1f));
            t.Add(i0); t.Add(i0 + 2); t.Add(i0 + 1); t.Add(i0); t.Add(i0 + 3); t.Add(i0 + 2);
        }

        private static Mesh Finish(string name, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)
        {
            var m = new Mesh { name = name };
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }
    }
}
