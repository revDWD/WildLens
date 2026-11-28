using UnityEngine;
using WildLens.Core;
using WildLens.Environment;

namespace WildLens.EditorTools
{
    /// <summary>Trail furniture: trailhead kiosk and bike, numbered marker posts and benches facing the meadow.</summary>
    public static class PropBuilder
    {
        private static Material _wood, _woodDark, _markerCap;

        public struct Footprint { public Vector3 position; public float radius; }

        /// <summary>Places the builder must keep clear of scenery (kiosk, bike parking).</summary>
        public static readonly System.Collections.Generic.List<Footprint> Footprints = new System.Collections.Generic.List<Footprint>();
        /// <summary>Where the bike is parked at the trailhead: the Reserve Road starts here (0.9).</summary>
        public static Vector3 BikeParking;

        public static void Build(TrailPath trail, TrailBuilder.Loop loop, Transform parent, bool trailBike = true)
        {
            _wood = GeneratedAssets.Material("Wood", new Color(0.52f, 0.39f, 0.26f));
            _woodDark = GeneratedAssets.Material("WoodDark", new Color(0.33f, 0.24f, 0.16f));
            _markerCap = GeneratedAssets.Material("MarkerCap", new Color(0.85f, 0.55f, 0.15f), 0.3f);

            float half = trail.Width * 0.5f;

            Footprints.Clear();
            BikeParking = Vector3.zero;
            Vector3 spawn = trail.GetPoint(0f);

            // Trailhead kiosk: a roofed park notice board with the live reserve board (ReserveInfoBoard), angled to
            // face the player's start so it's the first thing you see.
            {
                Vector3 p = trail.GetPoint(7f);
                Vector3 outward = Outward(p, loop);
                Vector3 pos = TerrainUtil.Ground(p + outward * (half + 2.9f));
                Vector3 face = TerrainUtil.Flat(spawn - pos).normalized;
                Vector3 bayP = trail.GetPoint(12f);
                bayP += Outward(bayP, loop) * (half + 3.2f);
                BuildKiosk(parent, pos, Quaternion.LookRotation(face), TerrainUtil.Flat(bayP - pos));
                Footprints.Add(new Footprint { position = pos, radius = 4.2f });
            }

            // 1.6: the trail bike stands in a bay beside the Reserve Board, angled out towards the trail, ready to ride.
            if (trailBike)
            {
                Vector3 p = trail.GetPoint(12f), dir = trail.GetDirection(12f);
                Vector3 outward = Outward(p, loop);
                Vector3 pos = TerrainUtil.Ground(p + outward * (half + 3.2f));
                Vector3 facing = TerrainUtil.Flat(dir - outward * 0.6f).normalized;
                BuildBikeBay(parent, pos, facing);
                var bike = new GameObject("Trail Bike");
                bike.transform.SetParent(parent, false);
                bike.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(facing));
                var model = new GameObject("Model").transform;
                model.SetParent(bike.transform, false);
                WildLens.Vehicles.TrailBikeModel.Build(model, (name, c, sm) => GeneratedAssets.Material(name, c, sm));
                bike.AddComponent<WildLens.Vehicles.TrailBike>();
                Footprints.Add(new Footprint { position = pos, radius = 3.2f });
                BikeParking = pos;
            }

            // Numbered marker posts.
            for (int k = 1; k < trail.MarkerCount; k++)
            {
                float d = k * trail.MarkerSpacing;
                Vector3 p = trail.GetPoint(d), dir = trail.GetDirection(d);
                Vector3 pos = TerrainUtil.Ground(p - Outward(p, loop) * (half + 0.6f));
                var post = Group("Trail Marker " + k, parent, pos, Quaternion.LookRotation(-dir));
                Box("Post", post, new Vector3(0f, 0.5f, 0f), new Vector3(0.14f, 1f, 0.14f), _woodDark);
                Box("Cap", post, new Vector3(0f, 1.02f, 0f), new Vector3(0.18f, 0.06f, 0.18f), _markerCap);
                Label(post, new Vector3(0f, 0.82f, 0.075f), k.ToString(), 0.03f);
                AddCollider(post, new Vector3(0f, 0.5f, 0f), new Vector3(0.14f, 1f, 0.14f));
            }

            // Benches on the outer edge, looking across the trail into the meadow.
            foreach (float f in new[] { 0.33f, 0.68f })
            {
                float d = trail.Length * f;
                Vector3 p = trail.GetPoint(d);
                Vector3 inward = -Outward(p, loop);
                Vector3 pos = TerrainUtil.Ground(p - inward * (half + 1.3f));
                var bench = Group("Bench", parent, pos, Quaternion.LookRotation(inward));
                Box("Seat", bench, new Vector3(0f, 0.45f, 0f), new Vector3(1.6f, 0.07f, 0.42f), _wood);
                Box("Back", bench, new Vector3(0f, 0.78f, -0.2f), new Vector3(1.6f, 0.32f, 0.06f), _wood);
                Box("LegL", bench, new Vector3(-0.65f, 0.22f, 0f), new Vector3(0.08f, 0.45f, 0.4f), _woodDark);
                Box("LegR", bench, new Vector3(0.65f, 0.22f, 0f), new Vector3(0.08f, 0.45f, 0.4f), _woodDark);
                AddCollider(bench, new Vector3(0f, 0.45f, -0.02f), new Vector3(1.6f, 0.9f, 0.45f));
            }
        }

        /// <summary>
        /// The Reserve Board (1.6): one three-sided kiosk under a single roof instead of a kiosk ringed by separate signs.
        ///   Face A (towards the visitor's start): the live reserve board (<see cref="ReserveInfoBoard"/>) and the header.
        ///   Face B: "PLEASE", the reserve's etiquette in three pictogram lines.
        ///   Face C: the Reserve Road and the bike, with the trail register mounted below.
        /// The faces stand on an equilateral triangle (side <c>W</c>); posts at the corners; a three-sided pitched roof.
        /// </summary>
        private static void BuildKiosk(Transform parent, Vector3 pos, Quaternion rot, Vector3 towardBay)
        {
            var roofMat = GeneratedAssets.Material("KioskRoof", new Color(0.24f, 0.27f, 0.22f), 0.15f, null, doubleSided: true);
            var kiosk = Group("Reserve Board", parent, pos, rot);
            const float W = 2.8f, boardY = 1.66f, boardH = 1.86f;
            float inR = W / (2f * Mathf.Sqrt(3f)), outR = W / Mathf.Sqrt(3f);

            // Corner posts (between the faces).
            for (int k = 0; k < 3; k++)
            {
                Vector3 c = Quaternion.Euler(0f, k * 120f + 60f, 0f) * Vector3.forward * (outR + 0.05f);
                PrimitiveMeshes.CreatePart("Post", PrimitiveType.Cylinder, kiosk, c + Vector3.up * 1.45f, new Vector3(0.2f, 1.45f, 0.2f), _woodDark);
            }
            // Three-sided pitched roof, its corners over the posts.
            var roofMesh = GeneratedAssets.LoadOrCreate("Mesh_ConeTri", () => ProceduralMeshes.Cone(3, cap: false));
            var roof = new GameObject("Roof");
            roof.transform.SetParent(kiosk, false);
            roof.transform.localPosition = new Vector3(0f, 2.92f, 0f);
            roof.transform.localRotation = Quaternion.Euler(0f, -30f, 0f);
            roof.transform.localScale = new Vector3(outR + 0.9f, 0.8f, outR + 0.9f); // unit cone: radius 1 (eaves ~0.45 m past each face)
            roof.AddComponent<MeshFilter>().sharedMesh = roofMesh;
            roof.AddComponent<MeshRenderer>().sharedMaterial = roofMat;
            PrimitiveMeshes.CreatePart("Finial", PrimitiveType.Cylinder, kiosk, new Vector3(0f, 3.78f, 0f), new Vector3(0.12f, 0.08f, 0.12f), _woodDark);

            Transform Face(int k, string name)
            {
                var face = Group(name, kiosk, kiosk.TransformPoint(Quaternion.Euler(0f, k * 120f, 0f) * Vector3.forward * inR),
                    kiosk.rotation * Quaternion.Euler(0f, k * 120f, 0f));
                Box("Backboard", face, new Vector3(0f, boardY, -0.02f), new Vector3(W, boardH, 0.08f), _wood);
                Box("Frame Top", face, new Vector3(0f, boardY + boardH * 0.5f, 0.03f), new Vector3(W + 0.06f, 0.07f, 0.07f), _woodDark);
                Box("Frame Bottom", face, new Vector3(0f, boardY - boardH * 0.5f, 0.03f), new Vector3(W + 0.06f, 0.07f, 0.07f), _woodDark);
                AddCollider(face, new Vector3(0f, 1.5f, -0.02f), new Vector3(W, 3f, 0.12f));
                return face;
            }

            // ---- Face A: the live board.
            var a = Face(0, "Face A: Reserve Board");
            Box("Ledge", a, new Vector3(0f, boardY - boardH * 0.5f - 0.02f, 0.12f), new Vector3(W, 0.04f, 0.22f), _woodDark);
            Box("Header Plank", a, new Vector3(0f, 2.72f, 0.06f), new Vector3(2.3f, 0.3f, 0.05f), SignBoard);
            SignKit.Label(a, new Vector3(0f, 2.72f, 0.09f), "MEADOW LOOP TRAILHEAD", SignKit.Fit("MEADOW LOOP TRAILHEAD", 0.02f, 2.1f, true),
                SignKit.Cream, TextAnchor.MiddleCenter, true);
            var faceGo = new GameObject("Board Face");
            faceGo.transform.SetParent(a, false);
            faceGo.transform.localPosition = new Vector3(0f, boardY, 0.025f);
            var board = faceGo.AddComponent<ReserveInfoBoard>();
            board.size = new Vector2(W - 0.16f, boardH - 0.12f);
            // The board shows best times as the same pictograms as the destination signs.
            board.timeIcons = new[] { SignKit.IconTexture(SignIcon.Dawn), SignKit.IconTexture(SignIcon.Day),
                SignKit.IconTexture(SignIcon.GoldenHour), SignKit.IconTexture(SignIcon.Dusk), SignKit.IconTexture(SignIcon.Rain) };

            // ---- Face B: etiquette, large.
            // The road face is whichever back face looks towards the bike bay, so it is read on the way to the bike.
            int roadK = Vector3.Dot(rot * Quaternion.Euler(0f, 120f, 0f) * Vector3.forward, towardBay) >
                        Vector3.Dot(rot * Quaternion.Euler(0f, 240f, 0f) * Vector3.forward, towardBay) ? 1 : 2;
            var b = Face(3 - roadK, "Face B: Please");
            Box("Panel", b, new Vector3(0f, boardY, 0.03f), new Vector3(W - 0.2f, boardH - 0.16f, 0.03f), SignBoard);
            Box("Rule", b, new Vector3(0f, boardY + 0.52f, 0.047f), new Vector3(W - 0.5f, 0.016f, 0.005f), SignTrim);
            SignKit.Label(b, new Vector3(0f, boardY + 0.7f, 0.05f), "PLEASE", 0.034f, SignKit.Cream, TextAnchor.MiddleCenter, true);
            var etiquette = new[] { (SignIcon.Walk, "Stay on the trails"), (SignIcon.Paw, "Give wildlife space"), (SignIcon.Binoculars, "Watch quietly") };
            for (int i = 0; i < etiquette.Length; i++)
            {
                float y = boardY + 0.28f - i * 0.36f;
                SignKit.Icon(b, new Vector3(-1.0f, y, 0.048f), etiquette[i].Item1, 0.28f, SignKit.Cream);
                SignKit.Label(b, new Vector3(-0.78f, y, 0.05f), etiquette[i].Item2, SignKit.Fit(etiquette[i].Item2, 0.024f, 1.85f, false), SignKit.Cream, TextAnchor.MiddleLeft);
            }
            SignKit.Label(b, new Vector3(0f, boardY - 0.74f, 0.05f), "Willow Creek Conservation Trust · est. 1962", 0.011f, SignKit.Cream);

            // ---- Face C: the Reserve Road and the bike; the trail register below.
            var c3 = Face(roadK, "Face C: Reserve Road");
            Box("Panel", c3, new Vector3(0f, boardY + 0.12f, 0.03f), new Vector3(W - 0.2f, boardH - 0.4f, 0.03f), SignBoard);
            SignKit.Icon(c3, new Vector3(-0.95f, boardY + 0.62f, 0.048f), SignIcon.Bike, 0.34f, SignKit.Cream);
            SignKit.Label(c3, new Vector3(-0.66f, boardY + 0.62f, 0.05f), "RESERVE ROAD", 0.03f, SignKit.Cream, TextAnchor.MiddleLeft, true);
            var road = new[] { "Ride slowly: 45 km/h", "Park at the pull-offs", "Walk the last stretch" };
            for (int i = 0; i < road.Length; i++)
                SignKit.Label(c3, new Vector3(-1.1f, boardY + 0.22f - i * 0.3f, 0.05f), road[i], SignKit.Fit(road[i], 0.022f, 2.2f, false), SignKit.Cream, TextAnchor.MiddleLeft);
            var reg = Group("Trail Register", c3, c3.TransformPoint(new Vector3(0.75f, 0f, 0.22f)), c3.rotation);
            Box("Box", reg, new Vector3(0f, 0.86f, 0f), new Vector3(0.5f, 0.34f, 0.3f), _wood);
            Box("Lid", reg, new Vector3(0f, 1.05f, 0f), new Vector3(0.56f, 0.05f, 0.36f), _woodDark);
            Box("Plate", reg, new Vector3(0f, 0.86f, 0.155f), new Vector3(0.42f, 0.2f, 0.01f), SignBoard);
            SignKit.Label(reg, new Vector3(0f, 0.86f, 0.162f), "TRAIL\nREGISTER", 0.0095f, SignKit.Cream, TextAnchor.MiddleCenter, true);
            Box("Post", reg, new Vector3(0f, 0.35f, -0.05f), new Vector3(0.09f, 0.7f, 0.09f), _woodDark);
        }

        /// <summary>
        /// The trailhead bike bay (1.6): the bike parked in a bay beside the Reserve Board, angled out ready to ride, with a
        /// hitching rail and a gravel pad (a pull-off, so it reads as part of the Reserve Road where it begins).
        /// </summary>
        private static void BuildBikeBay(Transform parent, Vector3 pos, Vector3 facing)
        {
            var bay = Group("Trailhead Bike Bay", parent, pos, Quaternion.LookRotation(facing));
            Vector3 right = Vector3.Cross(Vector3.up, facing);
            var rail = Group("Hitching Rail", bay, TerrainUtil.Ground(pos + right * 1.0f), Quaternion.LookRotation(facing));
            foreach (float z in new[] { -0.9f, 0.9f })
                Box("Post", rail, new Vector3(0f, 0.45f, z), new Vector3(0.12f, 0.9f, 0.12f), _woodDark);
            Box("Rail", rail, new Vector3(0f, 0.82f, 0f), new Vector3(0.1f, 0.1f, 2.1f), _wood);
            AddCollider(rail, new Vector3(0f, 0.45f, 0f), new Vector3(0.14f, 0.9f, 2.0f));
            var pad = bay.gameObject.AddComponent<ReserveParking>();
            pad.title = "Trailhead";
            pad.hint = "the Reserve Road starts here";
            pad.radius = 2.6f;
        }

        private static Vector3 Outward(Vector3 p, TrailBuilder.Loop loop)
        {
            Vector3 o = TerrainUtil.Flat(p - loop.center);
            return o.sqrMagnitude > 0.001f ? o.normalized : Vector3.right;
        }

        internal static Transform Group(string name, Transform parent, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            return go.transform;
        }

        internal static void Box(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat)
        {
            var go = PrimitiveMeshes.CreatePart(name, PrimitiveType.Cube, parent, localPos, size, mat);
            // Textured boxes (woodwork) get real-scale UVs so planks aren't stretched over long or wide pieces (1.2).
            if (mat != null && mat.mainTexture != null) go.GetComponent<MeshFilter>().sharedMesh = MetricCube(size);
        }

        // ---- real-scale box UVs (1.2)

        private const string BoxMeshAsset = GeneratedAssets.Folder + "/Mesh_PropBoxes.asset";
        private static System.Collections.Generic.Dictionary<string, Mesh> _boxes;

        /// <summary>
        /// A unit cube (scaled by the transform, like the built-in cube) whose UVs measure each face in metres of the wood
        /// scan, with U along the face's longer side: posts get vertical grain, boards and walls horizontal. Cached as
        /// sub-assets of one mesh asset, one per size.
        /// </summary>
        internal static Mesh MetricCube(Vector3 s)
        {
            string key = $"Box_{Mathf.RoundToInt(s.x * 1000f)}_{Mathf.RoundToInt(s.y * 1000f)}_{Mathf.RoundToInt(s.z * 1000f)}";
            if (_boxes == null)
            {
                _boxes = new System.Collections.Generic.Dictionary<string, Mesh>();
                foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(BoxMeshAsset))
                    if (o is Mesh m) _boxes[m.name] = m;
            }
            if (_boxes.TryGetValue(key, out var cached) && cached != null) return cached;

            var verts = new System.Collections.Generic.List<Vector3>();
            var normals = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var tris = new System.Collections.Generic.List<int>();
            // A different patch of the scan for each size, so identical-looking pieces don't line up.
            int hash = (Mathf.RoundToInt(s.x * 1000f) * 73856093) ^ (Mathf.RoundToInt(s.y * 1000f) * 19349663) ^ (Mathf.RoundToInt(s.z * 1000f) * 83492791);
            Vector2 offset = new Vector2((hash & 1023) / 1023f, ((hash >> 10) & 1023) / 1023f); // stable across sessions
            void Face(Vector3 n, Vector3 a, Vector3 b, float la, float lb)
            {
                if (lb > la) { var t = a; a = b; b = t; var l = la; la = lb; lb = l; }
                int i0 = verts.Count;
                foreach (var (su, sv) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
                {
                    verts.Add(n * 0.5f + a * su * 0.5f + b * sv * 0.5f);
                    normals.Add(n);
                    uvs.Add(new Vector2((su * 0.5f + 0.5f) * la, (sv * 0.5f + 0.5f) * lb) / GeneratedAssets.WoodTextureSize + offset);
                }
                // Unity's front face: Cross(p1 - p0, p2 - p0) points out of it.
                bool outward = Vector3.Dot(Vector3.Cross(a, b), n) > 0f;
                if (outward) tris.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 });
                else tris.AddRange(new[] { i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 });
            }
            Face(Vector3.right, Vector3.forward, Vector3.up, s.z, s.y);
            Face(Vector3.left, Vector3.forward, Vector3.up, s.z, s.y);
            Face(Vector3.up, Vector3.right, Vector3.forward, s.x, s.z);
            Face(Vector3.down, Vector3.right, Vector3.forward, s.x, s.z);
            Face(Vector3.forward, Vector3.right, Vector3.up, s.x, s.y);
            Face(Vector3.back, Vector3.right, Vector3.up, s.x, s.y);
            var mesh = new Mesh { name = key };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            GeneratedAssets.EnsureFolders();
            if (UnityEditor.AssetDatabase.LoadMainAssetAtPath(BoxMeshAsset) == null) UnityEditor.AssetDatabase.CreateAsset(mesh, BoxMeshAsset);
            else UnityEditor.AssetDatabase.AddObjectToAsset(mesh, BoxMeshAsset);
            _boxes[key] = mesh;
            return mesh;
        }

        internal static void AddCollider(Transform t, Vector3 center, Vector3 size)
        {
            var c = t.gameObject.AddComponent<BoxCollider>();
            c.center = center;
            c.size = size;
        }

        /// <summary>Reserve sign standard (0.9): stained board, cream lettering, green rule.</summary>
        internal static Material SignBoard => GeneratedAssets.Material("SignBoard", new Color(0.24f, 0.16f, 0.1f), 0.2f);
        internal static Material SignTrim => GeneratedAssets.Material("SignTrim", new Color(0.24f, 0.36f, 0.22f), 0.3f);
        internal static readonly Color SignText = new Color(0.93f, 0.88f, 0.72f);

        internal static void Label(Transform parent, Vector3 localPos, string text, float charSize) =>
            Label(parent, localPos, text, charSize, new Color(0.12f, 0.1f, 0.07f));

        internal static void Label(Transform parent, Vector3 localPos, string text, float charSize, Color colour)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            // TextMesh reads on its -Z side; flip so it faces the same way as the board front (+Z).
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = UI.UIFactory.DefaultFont;
            tm.fontSize = 64;
            tm.characterSize = charSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = colour;
            var mr = go.GetComponent<MeshRenderer>();
            if (tm.font != null) mr.sharedMaterial = tm.font.material;
        }
    }
}
