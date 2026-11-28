using System;
using System.Collections.Generic;

// The recipes for the generated small life (1.9): one function per model, written in plain C# with no UnityEngine, so the same
// code can be run outside Unity and its shapes and painted textures looked at (see the note in SmallLifeGeometry.cs).
// Every model is authored in metres, normalised so its measured largest side equals its stated size, with the pivot at the feet
// or attachment point. Separate children where motion needs them: "Wing L" / "Wing R" (and "Wing FL" etc.) hinge at the body
// and MacroMotion rolls them about their local Z; a dragonfly sits on a "Perch" stem with the animal under a "Rider" group.
namespace WildLens.EditorTools.SmallLife
{
    public static class SmallLifeShapes
    {
        public static readonly string[] Keys =
        {
            "Monarch", "Swallowtail", "CabbageWhite", "LunaMoth", "Whitetail", "Jewelwing", "BumbleBee", "HoneyBee", "Firefly", "LadyBeetle",
            "Mantis", "LeopardFrog", "GreenFrog", "GrayTreefrog", "RedEft", "RedBackedSalamander", "Snail", "HoneyMushroom", "YellowFlyAgaric",
            "TurkeyTail", "OxeyeDaisy", "Milkweed", "BlackEyedSusan", "Violet", "HaircapMoss", "Cattail",
        };

        /// <summary>The authoring size (largest side, metres) of each model: what <see cref="Build"/> normalises to. The subject's own size is applied relative to this.</summary>
        public static float Size(string key)
        {
            switch (key)
            {
                case "Monarch": return 0.09f;
                case "Swallowtail": return 0.10f;
                case "CabbageWhite": return 0.045f;
                case "LunaMoth": return 0.10f;
                case "Whitetail": return 0.065f;
                case "Jewelwing": return 0.055f;
                case "BumbleBee": return 0.02f;
                case "HoneyBee": return 0.013f;
                case "Firefly": return 0.012f;
                case "LadyBeetle": return 0.012f;
                case "Mantis": return 0.06f;
                case "LeopardFrog": return 0.08f;
                case "GreenFrog": return 0.085f;
                case "GrayTreefrog": return 0.05f;
                case "RedEft": return 0.07f;
                case "RedBackedSalamander": return 0.09f;
                case "Snail": return 0.03f;
                case "HoneyMushroom": return 0.07f;
                case "YellowFlyAgaric": return 0.10f;
                case "TurkeyTail": return 0.08f;
                case "OxeyeDaisy": return 0.05f;
                case "BlackEyedSusan": return 0.07f;
                case "Milkweed": return 0.09f;
                case "Violet": return 0.025f;
                case "HaircapMoss": return 0.08f;
                case "Cattail": return 0.14f;
                default: return 0f;
            }
        }

        public static Model Build(string key)
        {
            Model m;
            switch (key)
            {
                case "Monarch": m = Butterfly(key, 0.09f, BfStyle.Monarch); break;
                case "Swallowtail": m = Butterfly(key, 0.10f, BfStyle.Swallowtail); break;
                case "CabbageWhite": m = Butterfly(key, 0.045f, BfStyle.CabbageWhite); break;
                case "LunaMoth": m = Butterfly(key, 0.10f, BfStyle.Luna); break;
                case "Whitetail": m = Dragonfly(key, 0.065f, false); break;
                case "Jewelwing": m = Dragonfly(key, 0.055f, true); break;
                case "BumbleBee": m = Bee(key, 0.02f, true); break;
                case "HoneyBee": m = Bee(key, 0.013f, false); break;
                case "Firefly": m = Firefly(key, 0.012f); break;
                case "LadyBeetle": m = LadyBeetle(key, 0.012f); break;
                case "Mantis": m = Mantis(key, 0.06f); break;
                case "LeopardFrog": m = Frog(key, 0.08f, FrogKind.Leopard); break;
                case "GreenFrog": m = Frog(key, 0.085f, FrogKind.Green); break;
                case "GrayTreefrog": m = Frog(key, 0.05f, FrogKind.Gray); break;
                case "RedEft": m = Salamander(key, 0.07f, true); break;
                case "RedBackedSalamander": m = Salamander(key, 0.09f, false); break;
                case "Snail": m = Snail(key, 0.03f); break;
                case "HoneyMushroom": m = HoneyMushrooms(key, 0.07f); break;
                case "YellowFlyAgaric": m = FlyAgaric(key, 0.10f); break;
                case "TurkeyTail": m = TurkeyTail(key, 0.08f); break;
                case "OxeyeDaisy": m = DaisyLike(key, 0.05f, 0); break;
                case "BlackEyedSusan": m = DaisyLike(key, 0.07f, 1); break;
                case "Milkweed": m = Milkweed(key, 0.09f); break;
                case "Violet": m = Violet(key, 0.025f); break;
                case "HaircapMoss": m = Moss(key, 0.08f); break;
                case "Cattail": m = Cattail(key, 0.14f); break;
                default: return null;
            }
            Finish(m);
            return m;
        }

        // ------------------------------------------------------------------ shared finishing

        private static void Finish(Model m)
        {
            m.Finish();      // UVs from model-space positions, vertices made local to each part's origin
            Normalize(m);    // then the whole model scaled so its measured largest side is the stated size
            m.Measure(out V3 mn, out V3 mx);
            var focus = m.Find("Focus");
            if (focus != null) focus.origin = (mn + mx) * 0.5f; // the focus group sits at the centre of what is measured
        }

        /// <summary>Scales the whole model (local vertices and part origins) so its measured largest side equals <c>m.size</c>.</summary>
        private static void Normalize(Model m)
        {
            m.Measure(out V3 mn, out V3 mx);
            float largest = Math.Max(mx.x - mn.x, Math.Max(mx.y - mn.y, mx.z - mn.z));
            if (largest < 1e-7f) return;
            float k = m.size / largest;
            foreach (var p in m.parts)
            {
                p.origin = p.origin * k;
                for (int i = 0; i < p.v.Count; i++) p.v[i] = p.v[i] * k;
            }
        }

        private static Col C(int hex) => Col.Hex(hex);
        private static float Rf(Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);
        private static V3 P(float x, float y, float z) => new V3(x, y, z);

        // ------------------------------------------------------------------ butterflies and moths

        private enum BfStyle { Monarch, Swallowtail, CabbageWhite, Luna }

        /// <summary>The wing outlines (right side, units of the wingspan, x outward from the body, y toward the head) and what is painted on them.</summary>
        private sealed class WingField
        {
            public V2[] fore, hind;
            public readonly List<V2[]> exterior = new List<V2[]>(); // pairs (a, b) of outline pieces that are real edges of the wing
            public const float XMax = 0.52f, YMin = -0.72f, YMax = 0.24f;

            public WingField(V2[] f, V2[] h, float rootX = 0.035f)
            {
                fore = Sh.Chaikin(f, 2);
                hind = Sh.Chaikin(h, 2);
                AddEdges(fore, hind, rootX);
                AddEdges(hind, fore, rootX);
            }

            private void AddEdges(V2[] poly, V2[] other, float rootX)
            {
                for (int i = 0; i < poly.Length; i++)
                {
                    V2 a = poly[i], b = poly[(i + 1) % poly.Length];
                    int pieces = 4;
                    for (int k = 0; k < pieces; k++)
                    {
                        V2 q0 = new V2(a.x + (b.x - a.x) * k / pieces, a.y + (b.y - a.y) * k / pieces);
                        V2 q1 = new V2(a.x + (b.x - a.x) * (k + 1) / pieces, a.y + (b.y - a.y) * (k + 1) / pieces);
                        V2 mid = new V2((q0.x + q1.x) * 0.5f, (q0.y + q1.y) * 0.5f);
                        if (mid.x < rootX) continue;               // the edge at the body
                        if (Sh.Inside(mid.x, mid.y, other)) continue; // buried under the other wing
                        exterior.Add(new[] { q0, q1 });
                    }
                }
            }

            public bool Inside(float x, float y) => Sh.Inside(x, y, fore) || Sh.Inside(x, y, hind);
            public bool InFore(float x, float y) => Sh.Inside(x, y, fore);

            public float Border(float x, float y)
            {
                float best = 1e9f;
                foreach (var e in exterior) best = Math.Min(best, Sh.DistSeg(x, y, e[0], e[1]));
                return best;
            }

            /// <summary>Points just inside the outer edge, one every <paramref name="spacing"/> of edge, <paramref name="inset"/> in from it.</summary>
            public List<V2> Dots(float spacing, float inset)
            {
                var dots = new List<V2>();
                float carry = spacing * 0.5f;
                foreach (var e in exterior)
                {
                    float dx = e[1].x - e[0].x, dy = e[1].y - e[0].y, len = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (len < 1e-6f) continue;
                    carry -= len;
                    if (carry > 0f) continue;
                    carry += spacing;
                    V2 mid = new V2((e[0].x + e[1].x) * 0.5f, (e[0].y + e[1].y) * 0.5f);
                    V2 nrm = new V2(-dy / len, dx / len);
                    if (!Sh.Inside(mid.x + nrm.x * 0.01f, mid.y + nrm.y * 0.01f, fore) && !Sh.Inside(mid.x + nrm.x * 0.01f, mid.y + nrm.y * 0.01f, hind)) nrm = new V2(-nrm.x, -nrm.y);
                    dots.Add(new V2(mid.x + nrm.x * inset, mid.y + nrm.y * inset));
                }
                return dots;
            }

            /// <summary>Distance to the nearest of a set of straight lines from the root (the veins).</summary>
            public float VeinDistance(float x, float y, List<V2> ends, V2 root)
            {
                float best = 1e9f;
                foreach (var e in ends) best = Math.Min(best, Sh.DistSeg(x, y, root, e));
                return best;
            }
        }

        private static V2[] Poly(params float[] xy)
        {
            var p = new V2[xy.Length / 2];
            for (int i = 0; i < p.Length; i++) p[i] = new V2(xy[i * 2], xy[i * 2 + 1]);
            return p;
        }

        private static WingField WingsFor(BfStyle s)
        {
            switch (s)
            {
                case BfStyle.Swallowtail:
                    return new WingField(
                        Poly(0.01f, 0.07f, 0.14f, 0.15f, 0.30f, 0.20f, 0.44f, 0.17f, 0.50f, 0.09f, 0.46f, 0.0f, 0.38f, -0.07f, 0.25f, -0.10f, 0.10f, -0.08f, 0.01f, -0.05f),
                        Poly(0.01f, -0.03f, 0.15f, -0.06f, 0.30f, -0.10f, 0.40f, -0.19f, 0.37f, -0.28f, 0.34f, -0.31f, 0.345f, -0.56f, 0.285f, -0.34f, 0.19f, -0.32f, 0.09f, -0.27f, 0.02f, -0.16f));
                case BfStyle.CabbageWhite:
                    return new WingField(
                        Poly(0.01f, 0.07f, 0.14f, 0.14f, 0.30f, 0.19f, 0.44f, 0.15f, 0.49f, 0.07f, 0.45f, -0.03f, 0.36f, -0.08f, 0.22f, -0.10f, 0.08f, -0.08f, 0.01f, -0.05f),
                        Poly(0.01f, -0.03f, 0.14f, -0.07f, 0.28f, -0.10f, 0.37f, -0.16f, 0.36f, -0.26f, 0.27f, -0.32f, 0.14f, -0.31f, 0.05f, -0.23f, 0.01f, -0.12f));
                case BfStyle.Luna:
                    return new WingField(
                        Poly(0.01f, 0.06f, 0.20f, 0.14f, 0.40f, 0.17f, 0.50f, 0.07f, 0.44f, -0.06f, 0.28f, -0.11f, 0.03f, -0.08f),
                        Poly(0.0f, -0.05f, 0.15f, -0.09f, 0.30f, -0.14f, 0.38f, -0.24f, 0.34f, -0.34f, 0.335f, -0.70f, 0.28f, -0.40f, 0.20f, -0.36f, 0.10f, -0.28f, 0.02f, -0.16f));
                default:
                    return new WingField(
                        Poly(0.01f, 0.07f, 0.12f, 0.13f, 0.30f, 0.19f, 0.46f, 0.17f, 0.50f, 0.11f, 0.47f, 0.02f, 0.40f, -0.06f, 0.30f, -0.10f, 0.15f, -0.09f, 0.02f, -0.06f),
                        Poly(0.01f, -0.04f, 0.12f, -0.07f, 0.28f, -0.10f, 0.36f, -0.17f, 0.34f, -0.26f, 0.24f, -0.32f, 0.12f, -0.31f, 0.04f, -0.22f, 0.0f, -0.12f));
            }
        }

        private static Col[] PaintWing(BfStyle s, WingField f, int w, int h)
        {
            // Vein ends: every second outline point of each wing (not at the body).
            var ends = f.Dots(0.115f, 0f);
            V2 root = new V2(0.01f, 0.0f);
            List<V2> dots = s == BfStyle.Monarch ? f.Dots(0.052f, 0.0125f) : s == BfStyle.Swallowtail ? f.Dots(0.062f, 0.022f) : new List<V2>();

            return Sh.Paint(w, h, (u, v) =>
            {
                float x = u * WingField.XMax, y = WingField.YMin + v * (WingField.YMax - WingField.YMin);
                bool inside = f.Inside(x, y);
                float d = f.Border(x, y);
                float n = Sh.Noise(x * 60f, y * 60f, 5);
                Col c;
                switch (s)
                {
                    case BfStyle.Swallowtail:
                    {
                        c = Col.Lerp(C(0xF2D21A), C(0xFBE45A), Math.Min(1f, x * 1.6f));
                        float stripeShift = y * 0.35f;
                        bool fore = f.InFore(x, y) && y > -0.075f;
                        if (fore)
                            for (int i = 0; i < 4; i++)
                                if (Math.Abs(x - (0.095f + 0.085f * i + stripeShift)) < 0.0105f && y < 0.17f - x * 0.04f) c = C(0x14110C);
                        if (x < 0.03f && fore) c = Col.Lerp(c, C(0x14110C), 0.5f);
                        if (f.VeinDistance(x, y, ends, root) < 0.0034f) c = Col.Lerp(c, C(0x14110C), 0.8f);
                        float band = fore ? 0.034f : 0.030f;
                        if (d < band) c = C(0x14110C);
                        foreach (var dot in dots)
                        {
                            float dd = (float)Math.Sqrt((x - dot.x) * (x - dot.x) + (y - dot.y) * (y - dot.y));
                            if (dd < 0.0085f) c = y < -0.14f ? C(0x5C86C8) : C(0xF6DC3A);
                        }
                        float ox = x - 0.085f, oy = y + 0.24f;
                        if (ox * ox + oy * oy < 0.0001f) c = C(0xE8851C); // orange spot at the inner corner
                        break;
                    }
                    case BfStyle.CabbageWhite:
                    {
                        c = f.InFore(x, y) ? C(0xF6F6F0) : C(0xF3EFD2);
                        c = Col.Lerp(c, C(0xDADAD0), Math.Max(0f, 0.25f - x) * 0.8f);
                        float ax = x - 0.49f, ay = y - 0.10f;
                        float apex = (float)Math.Sqrt(ax * ax + ay * ay);
                        if (f.InFore(x, y)) c = Col.Lerp(c, C(0x3B3B38), Sh.Smooth(0.15f, 0.07f, apex) * 0.9f);
                        foreach (var sp in new[] { new V2(0.30f, 0.045f), new V2(0.27f, -0.045f) })
                            if ((x - sp.x) * (x - sp.x) + (y - sp.y) * (y - sp.y) < 0.00022f) c = C(0x2A2A28);
                        if (f.VeinDistance(x, y, ends, root) < 0.0030f) c = Col.Lerp(c, C(0xB8B8AC), 0.6f);
                        if (d < 0.008f) c = Col.Lerp(c, C(0x4A4A44), 0.4f);
                        break;
                    }
                    case BfStyle.Luna:
                    {
                        c = Col.Lerp(C(0xBFE3A0), C(0xA8D890), n * 0.5f);
                        if (d < 0.012f) c = C(0xD9E9A0);
                        // The purple-brown leading edge of the forewing.
                        if (f.InFore(x, y) && y > 0.145f - 0.28f * (x - 0.1f) * (x - 0.1f) * 2f - 0.02f && y > 0.10f && x < 0.46f) c = C(0x5A2E5A);
                        if (f.VeinDistance(x, y, ends, root) < 0.0032f) c = Col.Lerp(c, C(0x79A870), 0.8f);
                        c = Eyespot(c, x, y, 0.27f, 0.035f, 0.040f);
                        c = Eyespot(c, x, y, 0.22f, -0.17f, 0.036f);
                        if (x < 0.07f) c = Col.Lerp(c, C(0xF2F2E6), 0.6f);
                        break;
                    }
                    default: // Monarch
                    {
                        float t = Math.Min(1f, x * 2f);
                        c = Col.Lerp(C(0xD9600B), C(0xF08A1C), t);
                        c = Col.Lerp(c, C(0xFFA83A), n * 0.18f);
                        if (f.VeinDistance(x, y, ends, root) < 0.0046f) c = C(0x1A100A);
                        if (d < 0.019f) c = C(0x15100C);
                        foreach (var dot in dots)
                        {
                            float dd = (float)Math.Sqrt((x - dot.x) * (x - dot.x) + (y - dot.y) * (y - dot.y));
                            if (dd < 0.0062f) c = C(0xF4F0E4);
                        }
                        foreach (var sp in new[] { new V2(0.41f, 0.115f), new V2(0.375f, 0.085f), new V2(0.345f, 0.15f) })
                            if (f.InFore(x, y) && (x - sp.x) * (x - sp.x) + (y - sp.y) * (y - sp.y) < 0.00016f) c = C(0xFBE7C2);
                        if (x < 0.04f) c = Col.Lerp(c, C(0x1A100A), 0.55f);
                        break;
                    }
                }
                return c.WithAlpha(inside ? 1f : 0f);
            });
        }

        private static Col Eyespot(Col c, float x, float y, float cx, float cy, float r)
        {
            float d = (float)Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            if (d > r) return c;
            if (d < r * 0.22f) return C(0xE8EEF8);
            if (d < r * 0.5f) return C(0x6E78B8);
            if (d < r * 0.72f) return C(0x1A1218);
            return C(0xEDD05A);
        }

        private static Model Butterfly(string key, float S, BfStyle style)
        {
            var m = new Model { key = key, size = S };
            bool luna = style == BfStyle.Luna;
            var palette = m.AddMat("Body", MatKind.Palette, 0.1f);
            var wingMat = m.AddMat("Wing", MatKind.Painted, 0.1f);
            var field = WingsFor(style);
            wingMat.w = 192; wingMat.h = 256;
            wingMat.pixels = PaintWing(style, field, wingMat.w, wingMat.h);

            float yH = 0.035f * S; // wing hinge height
            float zH = 0.07f * S;  // hinge position along the body
            var body = m.AddPart("Body", palette, V3.Zero);
            Col dark = luna ? C(0xEFEFE6) : C(0x1E1A16);
            Col spot = C(0xF2F2EA);
            // Abdomen, thorax, head, antennae.
            Gm.Loft(body, new[] { P(0f, yH * 0.9f, -0.20f * S), P(0f, yH * 0.95f, -0.10f * S), P(0f, yH, 0.0f), P(0f, yH, 0.02f * S) },
                new[] { 0.004f * S, luna ? 0.036f * S : 0.022f * S, luna ? 0.040f * S : 0.024f * S, 0.02f * S },
                new[] { 0.004f * S, luna ? 0.034f * S : 0.021f * S, luna ? 0.036f * S : 0.023f * S, 0.02f * S }, V3.Up, dark, 8);
            Gm.Ellipsoid(body, P(0f, yH * 1.02f, zH), luna ? 0.05f * S : 0.030f * S, luna ? 0.05f * S : 0.030f * S, luna ? 0.075f * S : 0.055f * S, dark, 9, 6);
            Gm.Ellipsoid(body, P(0f, yH * 1.0f, zH + (luna ? 0.09f : 0.07f) * S), 0.024f * S, 0.024f * S, 0.024f * S, dark, 8, 5);
            if (!luna) for (int i = -1; i <= 1; i += 2) Gm.Ellipsoid(body, P(0.012f * S * i, yH * 1.4f, zH + 0.01f * S), 0.0045f * S, 0.0045f * S, 0.0045f * S, spot, 5, 3);
            float ax = luna ? 0.07f : 0.05f;
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                V3 a0 = P(0.008f * S * sgn, yH * 1.05f, zH + 0.09f * S), a1 = P(0.045f * S * sgn * (luna ? 1.2f : 1f), yH * 1.05f + 0.1f * S, zH + 0.2f * S);
                Gm.Stick(body, a0, a1, 0.0013f * S, 0.0007f * S, luna ? C(0xCFC8A8) : dark, 4);
                if (!luna) Gm.Ellipsoid(body, a1, 0.004f * S, 0.004f * S, 0.006f * S, dark, 5, 3);
                // Legs: three short rods a side down to the ground (the pivot).
                for (int l = 0; l < 3; l++)
                {
                    float lz = zH - 0.01f * S + l * 0.035f * S;
                    Gm.Stick(body, P(0.01f * S * sgn, yH * 0.9f, lz), P(0.04f * S * sgn, 0.0f, lz + 0.02f * S), 0.0012f * S, 0.0006f * S, dark, 4);
                }
            }
            // Wings: one painted quad a side, hinged at the body (rolled about Z by MacroMotion).
            for (int side = 0; side < 2; side++)
            {
                float sg = side == 0 ? -1f : 1f; // L at -X, R at +X
                var wing = m.AddPart(side == 0 ? "Wing L" : "Wing R", wingMat, P(0f, yH, zH));
                float x1 = WingField.XMax * S, y0 = WingField.YMin * S, y1 = WingField.YMax * S;
                Gm.TexQuad(wing, P(0f, yH, zH + y0), P(sg * x1, yH, zH + y0), P(sg * x1, yH, zH + y1), P(0f, yH, zH + y1),
                    new V2(0f, 0f), new V2(1f, 0f), new V2(1f, 1f), new V2(0f, 1f), V3.Up);
            }
            m.AddGroup("Focus", V3.Zero);
            return m;
        }

        // ------------------------------------------------------------------ dragonflies and damselflies

        private static Col[] PaintInsectWing(int w, int h, int kind)
        {
            // kind 0: whitetail (clear cells, dark band), 1: jewelwing (solid black, a little sheen), 2: bee (smoky membrane), 3: damsel-like clear
            return Sh.Paint(w, h, (u, v) =>
            {
                // u: 0 at the body, 1 at the tip; v: 0 trailing edge, 1 leading edge.
                float edge = 0.5f - Math.Abs(v - 0.5f);        // distance from the long edges
                float taper = 0.5f * (float)Math.Sqrt(Math.Max(0f, 1f - Math.Pow(Math.Max(0f, 2f * u - 1f), 2.4)));
                float half = Math.Max(0.04f, kind == 0 ? 0.30f : kind == 1 ? 0.5f : 0.5f * (1f - (float)Math.Pow(u, 3.0)));
                float tip = (float)Math.Sqrt(Math.Max(0f, 1f - u * u * 1.0f));
                bool inside;
                if (kind == 1) inside = Math.Abs(v - 0.5f) < 0.5f * (0.85f + 0.15f * Math.Min(1f, u * 5f)) * (float)Math.Sqrt(Math.Max(0f, 1f - Math.Pow(u, 6.0))) && u > 0.02f;
                else if (kind == 2) inside = Math.Abs(v - 0.5f) < 0.5f * (float)Math.Sqrt(Math.Max(0f, 1f - Math.Pow(2f * u - 1f, 2.0))) && u > 0.01f;
                else inside = Math.Abs(v - 0.5f) < (0.46f - 0.12f * u) * (float)Math.Sqrt(Math.Max(0f, 1f - Math.Pow(u, 10.0))) && u > 0.01f;
                if (kind == 0 || kind == 3)
                {
                    bool vein = Math.Abs(v - 0.5f) > 0.40f - 0.12f * u;                    // costa and trailing edge
                    float lane = (v * 5f) % 1f;
                    if (lane < 0.07f || lane > 0.93f) vein = true;                         // longitudinal veins
                    float cross = (u * 22f + Sh.Hash((int)(v * 5f), 3, 11) * 0.7f) % 1f;
                    if (cross < 0.08f) vein = true;                                        // cross veins
                    bool band = kind == 0 && u > 0.38f && u < 0.62f;
                    bool stigma = u > 0.8f && u < 0.9f && v > 0.74f;
                    Col c = vein || stigma ? C(0x2A2118) : band ? C(0x4A3020) : C(0xCFD8DC);
                    float a = inside && (vein || band || stigma) ? 1f : 0f;
                    if (kind == 3 && inside && !vein) a = 0f;
                    return c.WithAlpha(a);
                }
                if (kind == 1)
                {
                    Col c = Col.Lerp(C(0x0E0D12), C(0x2A2A3A), Sh.Noise(u * 30f, v * 8f, 2) * 0.5f);
                    return c.WithAlpha(inside ? 1f : 0f);
                }
                Col m2 = Col.Lerp(C(0xC9C5BC), C(0x9A968C), Math.Abs(v - 0.5f) * 2f);
                if (u < 0.6f && Math.Abs(((v * 6f) % 1f) - 0.5f) > 0.45f) m2 = C(0x6A6458);
                return m2.WithAlpha(inside ? 1f : 0f);
            });
        }

        private static Model Dragonfly(string key, float S, bool damsel)
        {
            // Built at true size (metres): the animal sits on a bare stem; the stem is not measured.
            var m = new Model { key = key, size = S };
            var pal = m.AddMat("Body", MatKind.Palette, damsel ? 0.6f : 0.35f);
            var wingMat = m.AddMat("Wing", MatKind.Painted, 0.5f);
            wingMat.w = 192; wingMat.h = 48;
            wingMat.pixels = PaintInsectWing(wingMat.w, wingMat.h, damsel ? 1 : 0);

            float perchH = 0.38f;
            var perch = m.AddPart("Perch", pal, V3.Zero);
            perch.noMeasure = true;
            Col reed = C(0x9A8A5A);
            Gm.Loft(perch, new[] { P(0f, 0f, 0f), P(0.004f, perchH * 0.5f, 0f), P(0.002f, perchH - 0.006f, 0f) }, new[] { 0.0022f, 0.0016f, 0.0012f }, new[] { 0.0022f, 0.0016f, 0.0012f }, V3.Fwd, reed, 5);
            m.AddGroup("Rider", P(0.002f, perchH, 0f));
            m.parts[m.parts.Count - 1].noMeasure = false;

            V3 o = P(0.002f, perchH, 0f);
            var body = m.AddPart("Body", pal, o, "Rider");
            float L = damsel ? 0.0085f : 0.0060f; // abdomen thickness scale
            Col thorax = damsel ? C(0x1F9A6A) : C(0x8A6A3A), abdomen = damsel ? C(0x1B8F78) : C(0xEDEFEA), eye = damsel ? C(0x2A2A30) : C(0x5C7A52);
            float bodyY = o.y + (damsel ? 0.0018f : 0.003f);
            float abLen = damsel ? 0.034f : 0.026f;
            // The animal rests with its head slightly up and the abdomen angled gently down behind it.
            Gm.Ellipsoid(body, P(o.x, bodyY, 0.0f), damsel ? 0.0025f : 0.0045f, damsel ? 0.0028f : 0.0045f, damsel ? 0.0075f : 0.0085f, thorax, 9, 6);
            Gm.Ellipsoid(body, P(o.x, bodyY + 0.0008f, damsel ? 0.0105f : 0.0105f), damsel ? 0.0030f : 0.0050f, damsel ? 0.0026f : 0.0040f, damsel ? 0.0026f : 0.0040f, thorax, 8, 5);
            for (int sgn = -1; sgn <= 1; sgn += 2)
                Gm.Ellipsoid(body, P(o.x + sgn * (damsel ? 0.0026f : 0.0042f), bodyY + 0.0014f, damsel ? 0.0112f : 0.0112f), damsel ? 0.0024f : 0.0040f, damsel ? 0.0024f : 0.0040f, damsel ? 0.0024f : 0.0038f, eye, 8, 5);
            var abPts = new V3[5];
            var abR = new float[5];
            for (int i = 0; i < 5; i++)
            {
                float t = i / 4f;
                abPts[i] = P(o.x, bodyY - 0.0006f - 0.0035f * t * t, -0.005f - abLen * t);
                abR[i] = (damsel ? 0.0018f : 0.0032f) * (i == 0 ? 0.9f : 1f - 0.45f * t) * (damsel ? 1f : 1f + 0.25f * (float)Math.Sin(t * Math.PI));
            }
            Gm.Loft(body, abPts, abR, abR, V3.Up, abdomen, 8);
            if (!damsel)
            {
                // A brown base to the abdomen, white beyond it (the male whitetail).
                Gm.Loft(body, new[] { abPts[0], P(o.x, bodyY - 0.0009f, -0.005f - abLen * 0.2f) }, new[] { abR[0] * 1.02f, abR[0] * 1.0f }, new[] { abR[0] * 1.02f, abR[0] * 1.0f }, V3.Up, C(0x6A5030), 8);
            }
            else
            {
                Gm.Ellipsoid(body, abPts[4] + P(0f, 0f, -0.001f), 0.0010f, 0.0010f, 0.0020f, C(0x0F6E58), 6, 4);
            }
            // Legs: three a side, gripping the stem.
            for (int sgn = -1; sgn <= 1; sgn += 2)
                for (int l = 0; l < 3; l++)
                {
                    float lz = -0.002f + l * 0.003f;
                    Gm.Stick(body, P(o.x + sgn * 0.002f, bodyY - 0.002f, lz), P(o.x + sgn * 0.005f, o.y - 0.0015f, lz + 0.003f), 0.0005f, 0.0003f, C(0x2A2218), 4);
                }
            // Wings: two a side (fore and hind), flat out to the sides (the dragonfly) or held up and together (the damselfly).
            float wl = damsel ? 0.027f : 0.034f, wc = damsel ? 0.0065f : 0.0095f;
            string[] names = { "Wing FL", "Wing FR", "Wing HL", "Wing HR" };
            for (int i = 0; i < 4; i++)
            {
                float sg = (i % 2 == 0) ? -1f : 1f;
                float zc = (i < 2 ? 0.0036f : -0.0010f);
                var wing = m.AddPart(names[i], wingMat, P(o.x, bodyY + 0.0030f, zc), "Rider");
                if (damsel) wing.restRoll = sg * -1f * 72f * -1f; // rolled up about the hinge (sign: the right wing lifts for +roll)
                Gm.TexQuad(wing, P(o.x, bodyY + 0.0030f, zc + wc * 0.5f), P(o.x + sg * wl, bodyY + 0.0030f, zc + wc * 0.5f), P(o.x + sg * wl, bodyY + 0.0030f, zc - wc * 0.5f), P(o.x, bodyY + 0.0030f, zc - wc * 0.5f),
                    new V2(0f, 1f), new V2(1f, 1f), new V2(1f, 0f), new V2(0f, 0f), V3.Up);
            }
            m.AddGroup("Focus", V3.Zero, "Rider");
            return m;
        }

        // ------------------------------------------------------------------ bees

        private static Model Bee(string key, float S, bool bumble)
        {
            var m = new Model { key = key, size = S };
            var pal = m.AddMat("Body", MatKind.Palette, 0.04f);
            var wingMat = m.AddMat("Wing", MatKind.Painted, 0.5f);
            wingMat.w = 96; wingMat.h = 48;
            wingMat.pixels = PaintInsectWing(wingMat.w, wingMat.h, 2);
            var body = m.AddPart("Body", pal, V3.Zero);
            float u = S / 0.02f; // everything below is written for a 2 cm bee
            Col fur = bumble ? C(0xF2CB1C) : C(0x9A6B2C), black = bumble ? C(0x17140F) : C(0x2C1F10), amber = bumble ? C(0xF2CB1C) : C(0xD9A02C);
            float y = 0.0058f * u;
            Gm.Ellipsoid(body, P(0f, y, 0.0048f * u), 0.0058f * u, 0.0056f * u, 0.0058f * u, fur, 10, 7);
            Gm.Ellipsoid(body, P(0f, y + 0.0030f * u, 0.0050f * u), 0.0042f * u, 0.0020f * u, 0.0020f * u, black, 8, 5); // the dark patch between the wings
            Gm.Ellipsoid(body, P(0f, y + 0.0006f * u, 0.0093f * u), 0.0030f * u, 0.0032f * u, 0.0030f * u, black, 8, 5);   // head
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                Gm.Ellipsoid(body, P(0.0020f * sgn * u, y + 0.0014f * u, 0.0103f * u), 0.0010f * u, 0.0016f * u, 0.0012f * u, black, 5, 3);
                Gm.Stick(body, P(0.0012f * sgn * u, y + 0.0020f * u, 0.0113f * u), P(0.0036f * sgn * u, y + 0.0055f * u, 0.0150f * u), 0.0003f * u, 0.0002f * u, black, 3);
            }
            // The abdomen in four bands behind the thorax.
            Col[] bands = bumble ? new[] { amber, black, black, black } : new[] { amber, black, amber, black };
            for (int i = 0; i < 4; i++)
            {
                float t = i / 3f;
                float rz = 0.0036f * u, rr = (0.0058f - 0.0018f * t * t) * u;
                Gm.Ellipsoid(body, P(0f, y - 0.0006f * u - 0.0006f * t * u, (-0.0010f - 0.0034f * i) * u), rr, rr * 0.95f, rz, bands[i], 9, 5);
            }
            for (int sgn = -1; sgn <= 1; sgn += 2)
                for (int l = 0; l < 3; l++)
                {
                    float lz = (0.0030f + l * 0.0022f) * u;
                    Gm.Stick(body, P(0.0030f * sgn * u, y - 0.0030f * u, lz), P(0.0060f * sgn * u, 0.0f, lz + 0.001f * u), 0.0004f * u, 0.0003f * u, black, 3);
                }
            for (int side = 0; side < 2; side++)
            {
                float sg = side == 0 ? -1f : 1f;
                float wl = 0.0120f * u, wc = 0.0050f * u;
                V3 hinge = P(0f, y + 0.0046f * u, 0.0040f * u);
                var wing = m.AddPart(side == 0 ? "Wing L" : "Wing R", wingMat, hinge);
                wing.restRoll = sg * -1f * -28f; // lifted a little
                Gm.TexQuad(wing, hinge + P(0f, 0f, 0.0006f * u), hinge + P(sg * wl, 0f, 0.0006f * u), hinge + P(sg * wl, 0f, -wc), hinge + P(0f, 0f, -wc),
                    new V2(0f, 1f), new V2(1f, 1f), new V2(1f, 0f), new V2(0f, 0f), V3.Up);
            }
            m.AddGroup("Focus", V3.Zero);
            return m;
        }

        // ------------------------------------------------------------------ fireflies and beetles

        private static Model Firefly(string key, float S)
        {
            var m = new Model { key = key, size = S };
            var pal = m.AddMat("Body", MatKind.Palette, 0.35f);
            var glow = m.AddMat("Lantern", MatKind.Glow, 0.3f);
            glow.emission = new Col(0.72f, 1f, 0.25f);
            var halo = m.AddMat("Halo", MatKind.Additive, 0f);
            halo.w = 32; halo.h = 32;
            halo.pixels = Sh.Paint(32, 32, (u, v) =>
            {
                float d = (float)Math.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
                float a = Math.Max(0f, 1f - d);
                a = a * a * (3f - 2f * a);
                return new Col(0.75f * a, 1f * a, 0.35f * a, a);
            });
            var wingMat = m.AddMat("Wing", MatKind.Painted, 0.5f);
            wingMat.w = 96; wingMat.h = 48;
            wingMat.pixels = PaintInsectWing(wingMat.w, wingMat.h, 2);

            float k = S / 0.012f;
            var body = m.AddPart("Body", pal, V3.Zero);
            Col brown = C(0x2A1C0E), edge = C(0xC9AE62), pro = C(0xE6A08A), dark = C(0x1A130C);
            float y = 0.0034f * k;
            // The two wing covers, side by side, with a pale rim.
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                Gm.Ellipsoid(body, P(0.0017f * sgn * k, y, -0.0022f * k), 0.0021f * k, 0.0017f * k, 0.0055f * k, brown, 8, 6);
                Gm.Ellipsoid(body, P(0.0032f * sgn * k, y - 0.0003f * k, -0.0022f * k), 0.0008f * k, 0.0013f * k, 0.0052f * k, edge, 6, 4);
            }
            Gm.Ellipsoid(body, P(0f, y + 0.0002f * k, 0.0033f * k), 0.0030f * k, 0.0016f * k, 0.0026f * k, pro, 8, 5);                 // pronotum, pinkish
            Gm.Ellipsoid(body, P(0f, y + 0.0011f * k, 0.0033f * k), 0.0012f * k, 0.0007f * k, 0.0017f * k, dark, 6, 4);                // its dark centre
            Gm.Ellipsoid(body, P(0f, y - 0.0004f * k, 0.0056f * k), 0.0014f * k, 0.0012f * k, 0.0012f * k, dark, 6, 4);               // head
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                Gm.Stick(body, P(0.0007f * sgn * k, y, 0.0063f * k), P(0.0024f * sgn * k, y + 0.0014f * k, 0.0088f * k), 0.0002f * k, 0.00012f * k, dark, 3);
                for (int l = 0; l < 3; l++)
                {
                    float lz = (0.0030f - l * 0.0022f) * k;
                    Gm.Stick(body, P(0.0020f * sgn * k, y - 0.0010f * k, lz), P(0.0045f * sgn * k, 0f, lz + 0.0008f * k), 0.0002f * k, 0.00015f * k, dark, 3);
                }
            }
            var lantern = m.AddPart("Lantern", glow, V3.Zero);
            Gm.Ellipsoid(lantern, P(0f, y - 0.0010f * k, -0.0066f * k), 0.0019f * k, 0.0011f * k, 0.0020f * k, C(0xEAF08A), 8, 5);
            for (int side = 0; side < 2; side++)
            {
                float sg = side == 0 ? -1f : 1f;
                V3 hinge = P(0.0012f * sg * k, y + 0.0016f * k, 0.0006f * k);
                var wing = m.AddPart(side == 0 ? "Wing L" : "Wing R", wingMat, hinge);
                wing.restRoll = sg * -1f * -20f;
                wing.noMeasure = true;
                float wl = 0.0058f * k, wc = 0.0016f * k;
                Gm.TexQuad(wing, hinge, hinge + P(sg * wl, 0f, 0f), hinge + P(sg * wl, 0f, -wc * 2.2f), hinge + P(0f, 0f, -wc * 2.2f),
                    new V2(0f, 1f), new V2(1f, 1f), new V2(1f, 0f), new V2(0f, 0f), V3.Up);
            }
            var haloPart = m.AddPart("Halo", halo, P(0f, y - 0.0010f * k, -0.0066f * k));
            haloPart.noMeasure = true;
            float hs = 0.045f;
            V3 hc = haloPart.origin;
            Gm.TexQuad(haloPart, hc + P(-hs, -hs, 0f), hc + P(hs, -hs, 0f), hc + P(hs, hs, 0f), hc + P(-hs, hs, 0f),
                new V2(0f, 0f), new V2(1f, 0f), new V2(1f, 1f), new V2(0f, 1f), P(0f, 0f, -1f), false);
            m.AddGroup("Focus", V3.Zero);
            return m;
        }

        private static Model LadyBeetle(string key, float S)
        {
            var m = new Model { key = key, size = S };
            var pal = m.AddMat("Body", MatKind.Palette, 0.7f);
            var body = m.AddPart("Body", pal, V3.Zero);
            float k = S / 0.012f;
            Col red = C(0xD8505A), black = C(0x15110F), margin = C(0xF0C8B8);
            float y = 0.0006f * k;
            // The domed wing covers: an ellipsoid sunk into the ground plane (its lower half hidden by the legs and a flat underside).
            V3 dome = P(0f, y, -0.0004f * k), rr = P(0.0031f * k, 0.0030f * k, 0.0042f * k);
            Gm.Ellipsoid(body, dome, rr, Frame.Identity, red, 12, 8);
            Gm.Ellipsoid(body, P(0f, y + 0.0004f * k, 0.0028f * k), 0.0024f * k, 0.0014f * k, 0.0013f * k, black, 8, 4); // pronotum
            Gm.Ellipsoid(body, P(0f, y + 0.0000f * k, 0.0049f * k), 0.0017f * k, 0.0012f * k, 0.0012f * k, black, 8, 4); // head
            Gm.Ellipsoid(body, P(0f, y + 0.0003f * k, 0.0033f * k), 0.0028f * k, 0.0012f * k, 0.0008f * k, margin, 8, 4); // pale front margin
            // Six black spots on the wing covers, laid on the dome surface.
            // (polar angle from the top, azimuth round the long axis: z is the head end) in radians: front, middle and rear pairs.
            float[,] sp = { { 0.62f, 1.00f }, { 0.62f, 2.14f }, { 0.98f, 0.20f }, { 0.98f, 2.94f }, { 0.74f, -0.95f }, { 0.74f, -2.19f } };
            for (int i = 0; i < sp.GetLength(0); i++)
            {
                float phi = sp[i, 0], th = sp[i, 1];
                V3 loc = P(rr.x * (float)(Math.Sin(phi) * Math.Cos(th)), rr.y * (float)Math.Cos(phi), rr.z * (float)(Math.Sin(phi) * Math.Sin(th)) - 0.0004f * k);
                V3 nrm = P(loc.x / (rr.x * rr.x), loc.y / (rr.y * rr.y), (loc.z + 0.0004f * k) / (rr.z * rr.z)).Normalized;
                // Pair the spots left and right of the midline: azimuth th and pi - th.
                Gm.Ellipsoid(body, P(loc.x, loc.y + y, loc.z) + nrm * (0.00008f * k), new V3(0.0007f * k, 0.0007f * k, 0.0003f * k), Frame.Look(nrm, V3.Fwd), black, 7, 4);
            }
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                Gm.Stick(body, P(0.0010f * sgn * k, y + 0.0004f * k, 0.0058f * k), P(0.0022f * sgn * k, y + 0.0008f * k, 0.0078f * k), 0.00018f * k, 0.00012f * k, black, 3);
                for (int l = 0; l < 3; l++)
                {
                    float lz = (0.0026f - l * 0.0028f) * k;
                    Gm.Stick(body, P(0.0022f * sgn * k, y - 0.0003f * k, lz), P(0.0045f * sgn * k, 0f, lz + 0.0006f * k), 0.00022f * k, 0.00015f * k, black, 3);
                }
            }
            m.AddGroup("Focus", V3.Zero);
            return m;
        }

        private static Model Mantis(string key, float S)
        {
            var m = new Model { key = key, size = S };
            var pal = m.AddMat("Body", MatKind.Palette, 0.25f);
            var body = m.AddPart("Body", pal, V3.Zero);
            Col green = C(0x7E8A3A), tan = C(0xA9A06A), dark = C(0x3B3A22), eye = C(0xC9B97A);
            // Abdomen (long, slim, a little raised), thorax (the long pronotum, angled up), then the triangular head.
            Gm.Loft(body, new[] { P(0f, 0.0125f, -0.0300f), P(0f, 0.0132f, -0.0170f), P(0f, 0.0138f, -0.0050f), P(0f, 0.0140f, 0.0010f) },
                new[] { 0.0016f, 0.0030f, 0.0033f, 0.0026f }, new[] { 0.0015f, 0.0028f, 0.0030f, 0.0026f }, V3.Up, green, 8);
            Gm.Loft(body, new[] { P(0f, 0.0140f, 0.0000f), P(0f, 0.0170f, 0.0040f), P(0f, 0.0235f, 0.0085f), P(0f, 0.0292f, 0.0105f) },
                new[] { 0.0023f, 0.0019f, 0.0017f, 0.0016f }, new[] { 0.0021f, 0.0017f, 0.0015f, 0.0014f }, V3.Up, green, 8);
            // Folded wings along the back.
            Gm.Ellipsoid(body, P(0f, 0.0160f, -0.0150f), 0.0034f, 0.0010f, 0.0230f, tan, 8, 6);
            // The head: wide and flat, with big eyes at the corners.
            Gm.Ellipsoid(body, P(0f, 0.0312f, 0.0118f), 0.0030f, 0.0020f, 0.0022f, green, 8, 5);
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                Gm.Ellipsoid(body, P(0.0030f * sgn, 0.0322f, 0.0120f), 0.0016f, 0.0018f, 0.0016f, eye, 6, 4);
                Gm.Stick(body, P(0.0010f * sgn, 0.0332f, 0.0128f), P(0.0030f * sgn, 0.0375f, 0.0170f), 0.0002f, 0.0001f, dark, 3);
                // The raptorial forelegs, folded in front of the chest: coxa, femur forward and down, tibia folded back up.
                Gm.Stick(body, P(0.0020f * sgn, 0.0232f, 0.0088f), P(0.0030f * sgn, 0.0222f, 0.0170f), 0.0011f, 0.0008f, green, 5);
                Gm.Stick(body, P(0.0030f * sgn, 0.0222f, 0.0170f), P(0.0026f * sgn, 0.0290f, 0.0150f), 0.0008f, 0.0004f, green, 5);
                Gm.Stick(body, P(0.0026f * sgn, 0.0290f, 0.0150f), P(0.0020f * sgn, 0.0300f, 0.0126f), 0.0004f, 0.0002f, dark, 3);
                // Walking legs: two pairs reaching the ground (the pivot).
                for (int l = 0; l < 2; l++)
                {
                    float lz = -0.0020f - l * 0.0100f;
                    Gm.Stick(body, P(0.0020f * sgn, 0.0138f, lz), P(0.0105f * sgn, 0.0150f, lz + 0.0010f), 0.0009f, 0.0006f, green, 4);
                    Gm.Stick(body, P(0.0105f * sgn, 0.0150f, lz + 0.0010f), P(0.0150f * sgn, 0.0f, lz + 0.0040f), 0.0006f, 0.0003f, green, 4);
                }
            }
            m.AddGroup("Focus", V3.Zero);
            return m;
        }

        // ------------------------------------------------------------------ frogs

        private enum FrogKind { Leopard, Green, Gray }

        // The frog's trunk and head as stations (z in body lengths, half-width, half-height, centre height); the skin painter uses the same table.
        private static readonly float[] FrogZ = { -0.36f, -0.24f, -0.05f, 0.14f, 0.30f, 0.44f, 0.55f, 0.63f };
        private static readonly float[] FrogRx = { 0.12f, 0.30f, 0.35f, 0.33f, 0.27f, 0.20f, 0.12f, 0.04f };
        private static readonly float[] FrogRy = { 0.10f, 0.17f, 0.20f, 0.19f, 0.15f, 0.115f, 0.085f, 0.05f };
        private static readonly float[] FrogCy = { 0.16f, 0.19f, 0.205f, 0.205f, 0.195f, 0.17f, 0.15f, 0.13f };

        private static float FrogHalfWidth(float z)
        {
            if (z <= FrogZ[0]) return FrogRx[0];
            for (int i = 1; i < FrogZ.Length; i++)
                if (z <= FrogZ[i]) return FrogRx[i - 1] + (FrogRx[i] - FrogRx[i - 1]) * (z - FrogZ[i - 1]) / (FrogZ[i] - FrogZ[i - 1]);
            return FrogRx[FrogRx.Length - 1];
        }

        private const float FrogU = 0.70f;     // texture width in body lengths
        private const float FrogV0 = 0.10f, FrogVSpan = 0.90f, FrogZ0 = -0.40f, FrogZSpan = 1.05f; // texture v range and the z range it covers

        private static Model Frog(string key, float S, FrogKind kind)
        {
            var m = new Model { key = key, size = S };
            var skin = m.AddMat("Skin", MatKind.PaintedOpaque, kind == FrogKind.Gray ? 0.35f : 0.55f);
            var det = m.AddMat("Detail", MatKind.Palette, 0.7f);
            skin.w = 128; skin.h = 160;
            skin.pixels = PaintFrogSkin(kind, skin.w, skin.h);
            var body = m.AddPart("Body", skin, V3.Zero);
            float sq = kind == FrogKind.Gray ? 0.9f : 1f;
            int K = FrogZ.Length;
            var pts = new V3[K]; var rx = new float[K]; var ry = new float[K];
            for (int i = 0; i < K; i++) { pts[i] = P(0f, FrogCy[i], FrogZ[i]); rx[i] = FrogRx[i]; ry[i] = FrogRy[i] * (i > 0 && i < 5 ? sq : 1f); }
            Gm.Loft(body, pts, rx, ry, V3.Up, Col.White, 14);
            // UV: seen from above (x across, z along); anything facing down takes the pale belly strip at the bottom of the texture.
            body.uvFn = (p, n) =>
            {
                if (n.y < -0.25f) return new V2(0.5f, 0.04f);
                return new V2(Math.Max(0.01f, Math.Min(0.99f, p.x / FrogU + 0.5f)),
                    Math.Max(FrogV0 + 0.01f, Math.Min(0.99f, FrogV0 + FrogVSpan * (p.z - FrogZ0) / FrogZSpan)));
            };
            // Eyes: golden spheres standing out from the top of the head, each with a dark horizontal pupil facing outward and forward.
            var eyes = m.AddPart("Eyes", det, V3.Zero);
            Col gold = kind == FrogKind.Gray ? C(0xC9A04A) : C(0xB89440), pupil = C(0x0E0C08);
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                V3 ec = P(0.14f * sgn, 0.315f, 0.33f);
                Gm.Ellipsoid(eyes, ec, 0.082f, 0.082f, 0.082f, gold, 10, 7);
                V3 dir = P(0.55f * sgn, 0.25f, 0.8f).Normalized;
                Gm.Ellipsoid(eyes, ec + dir * 0.074f, new V3(0.040f, 0.014f, 0.010f), Frame.Look(dir, V3.Up), pupil, 7, 4);
            }
            // Legs. Hind legs folded: thigh forward, shank back, foot forward again; front legs short and straight.
            Col legCol = kind == FrogKind.Gray ? C(0x7E806E) : kind == FrogKind.Green ? C(0x5E7030) : C(0x6E6E32);
            var legs = m.AddPart("Legs", det, V3.Zero);
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                V3 hip = P(0.22f * sgn, 0.18f, -0.22f), knee = P(0.39f * sgn, 0.16f, 0.06f), ankle = P(0.31f * sgn, 0.055f, -0.30f), toe = P(0.40f * sgn, 0.014f, -0.04f);
                Gm.Loft(legs, new[] { hip, V3.Lerp(hip, knee, 0.5f), knee }, new[] { 0.10f, 0.115f, 0.065f }, new[] { 0.095f, 0.105f, 0.060f }, V3.Up, legCol, 8);
                Gm.Loft(legs, new[] { knee, ankle }, new[] { 0.062f, 0.036f }, new[] { 0.056f, 0.032f }, V3.Up, legCol, 7);
                Gm.Loft(legs, new[] { ankle, toe }, new[] { 0.034f, 0.024f }, new[] { 0.024f, 0.014f }, V3.Up, legCol, 6);
                V3 sh = P(0.22f * sgn, 0.15f, 0.20f), elbow = P(0.27f * sgn, 0.075f, 0.24f), hand = P(0.20f * sgn, 0.014f, 0.37f);
                Gm.Loft(legs, new[] { sh, elbow }, new[] { 0.066f, 0.044f }, new[] { 0.060f, 0.040f }, V3.Up, legCol, 6);
                Gm.Loft(legs, new[] { elbow, hand }, new[] { 0.040f, 0.028f }, new[] { 0.036f, 0.018f }, V3.Up, legCol, 6);
                if (kind == FrogKind.Gray) // the big sticky toe pads
                {
                    Col pad = C(0xD8D4A8);
                    Gm.Ellipsoid(legs, hand + P(0.015f * sgn, 0f, 0.025f), 0.032f, 0.015f, 0.032f, pad, 6, 3);
                    Gm.Ellipsoid(legs, toe + P(0.03f * sgn, 0f, 0.02f), 0.034f, 0.015f, 0.034f, pad, 6, 3);
                }
            }
            if (kind == FrogKind.Gray) // the eardrum
                for (int sgn = -1; sgn <= 1; sgn += 2) Gm.Ellipsoid(eyes, P(0.215f * sgn, 0.22f, 0.20f), new V3(0.03f, 0.04f, 0.04f), Frame.Look(P(sgn, 0f, 0f), V3.Up), C(0x8A8C78), 6, 3);
            m.AddGroup("Focus", V3.Zero);
            return m;
        }

        private static Col[] PaintFrogSkin(FrogKind kind, int w, int h)
        {
            return Sh.Paint(w, h, (u, v) =>
            {
                if (v < 0.08f) return kind == FrogKind.Gray ? C(0xDCD8C8) : C(0xE8E4C8); // belly
                float x = (u - 0.5f) * FrogU, z = ((v - FrogV0) / FrogVSpan) * FrogZSpan + FrogZ0;
                float nz = Sh.Noise(u * 22f, v * 22f, 3), nz2 = Sh.Noise(u * 70f, v * 70f, 9);
                float hw = FrogHalfWidth(z);
                Col c;
                if (kind == FrogKind.Leopard)
                {
                    c = Col.Lerp(C(0x6D7A2E), C(0x8A7A3A), nz);
                    // Dark, pale-ringed spots on a jittered grid, round in body-length units.
                    for (int gy = -1; gy <= 1; gy++)
                        for (int gx = -1; gx <= 1; gx++)
                        {
                            int cx = (int)(u * 7f) + gx, cy = (int)(v * 8f) + gy;
                            float px = (cx + 0.5f + (Sh.Hash(cx, cy, 1) - 0.5f) * 0.8f) / 7f, py = (cy + 0.5f + (Sh.Hash(cx, cy, 2) - 0.5f) * 0.8f) / 8f;
                            float rr = 0.030f + Sh.Hash(cx, cy, 3) * 0.020f;
                            float ex = (u - px) * FrogU, ez = (v - py) * (FrogVSpan > 0f ? FrogZSpan / FrogVSpan : 1f);
                            float dd = (float)Math.Sqrt(ex * ex + ez * ez);
                            float pz = (py - FrogV0) / FrogVSpan * FrogZSpan + FrogZ0, pxx = (px - 0.5f) * FrogU;
                            if (Math.Abs(pxx) > FrogHalfWidth(pz) * 0.75f || pz < -0.34f || pz > 0.50f) continue;
                            if (dd < rr) c = C(0x2B2012);
                            else if (dd < rr + 0.009f) c = C(0xC9C48A);
                        }
                    if (Math.Abs(Math.Abs(x) - 0.19f) < 0.011f && z > -0.30f && z < 0.34f) c = C(0xD8CC8C); // the pale ridges down each side
                }
                else if (kind == FrogKind.Green)
                {
                    c = Col.Lerp(C(0x56702A), C(0x6E7C34), nz);
                    c = Col.Lerp(c, C(0x2E3A18), Math.Max(0f, nz2 - 0.62f) * 1.6f);
                    if (Math.Abs(Math.Abs(x) - 0.19f) < 0.013f && z > -0.30f && z < 0.34f) c = C(0x9AAE3A); // the ridge down each side
                    if (z > 0.26f && Math.Abs(x) > hw - 0.035f) c = C(0x7FC030); // the bright green lip
                    if (z > 0.52f) c = Col.Lerp(c, C(0x7FC030), 0.5f);
                }
                else
                {
                    c = Col.Lerp(C(0x8E9078), C(0x7C8468), nz);
                    float blot = Sh.Noise(u * 9f + 3f, v * 9f, 21);
                    if (blot > 0.58f) c = Col.Lerp(c, C(0x30362A), Math.Min(1f, (blot - 0.58f) * 7f));
                    c = Col.Lerp(c, C(0xB8BCA0), Math.Max(0f, nz2 - 0.7f));
                }
                return c;
            });
        }

        // ------------------------------------------------------------------ salamanders

        private static Model Salamander(string key, float S, bool eft)
        {
            var m = new Model { key = key, size = S };
            var skin = m.AddMat("Skin", MatKind.PaintedOpaque, eft ? 0.25f : 0.5f);
            var det = m.AddMat("Detail", MatKind.Palette, 0.8f);
            skin.w = 64; skin.h = 192;
            float len = S;
            skin.pixels = PaintSalamander(eft, skin.w, skin.h, len);
            var body = m.AddPart("Body", skin, V3.Zero);
            // A gentle S-curve from the nose back along the tail. The head is the front 12 percent.
            int K = 12;
            var pts = new V3[K]; var rx = new float[K]; var ry = new float[K];
            for (int i = 0; i < K; i++)
            {
                float t = i / (float)(K - 1);
                float z = (0.5f - t) * len;
                pts[i] = P((float)Math.Sin(t * 5.2f) * 0.07f * len * Math.Min(1f, t * 3f), (eft ? 0.05f : 0.035f) * len, z);
                float wBody = t < 0.10f ? 0.045f + 0.05f * (t / 0.10f) : t < 0.52f ? 0.075f : 0.075f * (1f - (t - 0.52f) / 0.48f * 0.93f);
                rx[i] = wBody * len * (eft ? 0.95f : 0.8f);
                ry[i] = (t < 0.52f ? wBody * 0.9f : wBody * (1.0f + 0.9f * (t - 0.52f))) * len * (eft ? 0.9f : 0.75f);
            }
            rx[0] = 0.030f * len; ry[0] = 0.025f * len;
            Gm.Loft(body, pts, rx, ry, V3.Up, Col.White, 10);
            var eyes = m.AddPart("Eyes", det, V3.Zero);
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                Gm.Ellipsoid(eyes, P(pts[1].x + 0.034f * len * sgn, pts[1].y + 0.035f * len, pts[1].z - 0.002f * len), 0.014f * len, 0.014f * len, 0.014f * len, C(0x0E0C0A), 5, 3);
                // Legs: out and down from the shoulder and the hip, a little foot turned forward.
                foreach (float lt in new[] { 0.24f, 0.46f })
                {
                    int i = (int)(lt * (K - 1));
                    V3 a = pts[i] + P(0.05f * len * sgn, 0.0f, 0f), b = a + P(0.075f * len * sgn, -0.005f * len, 0.0f), c = b + P(0.020f * len * sgn, -0.025f * len, 0.040f * len);
                    Col leg = eft ? C(0xE0620E) : C(0x2A2622);
                    var legs = m.Find("Legs") ?? m.AddPart("Legs", det, V3.Zero);
                    Gm.Stick(legs, a, b, 0.019f * len, 0.015f * len, leg, 6);
                    Gm.Stick(legs, b, c, 0.015f * len, 0.010f * len, leg, 6);
                }
            }
            m.AddGroup("Focus", V3.Zero);
            return m;
        }

        private static Col[] PaintSalamander(bool eft, int w, int h, float len)
        {
            // u wraps round the body (0.25 = the back, 0.75 = the belly, 0 and 0.5 the flanks); v runs from the nose (0) to the tail tip (1).
            // The trunk is about 0.12 of the length across, so one unit of u is roughly 0.38 of the length (a circumference) and one of v the length.
            const float CircU = 0.38f;
            return Sh.Paint(w, h, (u, v) =>
            {
                float t = v;
                bool back = u < 0.5f;
                float n = Sh.Noise(u * 40f, v * 90f, 7);
                if (eft)
                {
                    Col c = back ? Col.Lerp(C(0xE06A10), C(0xEA7A1E), n) : Col.Lerp(C(0xEEAA3A), C(0xF2BE50), n);
                    // A row of black-ringed red spots down each side of the back (u = 0.14 and 0.36), one every 0.07 of the length.
                    foreach (float uc in new[] { 0.14f, 0.36f })
                    {
                        float cell = (t / 0.07f) % 1f;
                        float dz = (cell - 0.5f) * 0.07f, dx = (u - uc) * CircU;
                        float r = (float)Math.Sqrt(dz * dz + dx * dx);
                        if (t > 0.14f && t < 0.62f)
                        {
                            if (r < 0.018f) c = C(0xC82A12);
                            else if (r < 0.026f) c = C(0x1A0E08);
                        }
                    }
                    if (!back && n > 0.72f) c = Col.Lerp(c, C(0x4A2A10), 0.6f); // dark specks on the belly
                    return c;
                }
                else
                {
                    Col c = back ? C(0x2A2622) : Col.Lerp(C(0x9A9690), C(0x6E6A66), n);
                    if (back)
                    {
                        float stripe = 0.085f * (1f - Math.Max(0f, t - 0.4f) / 0.6f * 0.7f);
                        if (Math.Abs(u - 0.25f) < stripe && t > 0.12f) c = Col.Lerp(C(0x9A4A22), C(0xB45E2A), n);
                    }
                    else if (n > 0.55f) c = Col.Lerp(c, C(0x1E1C1A), 0.5f);
                    return c;
                }
            });
        }

        // ------------------------------------------------------------------ snail

        private static Model Snail(string key, float S)
        {
            var m = new Model { key = key, size = S };
            var pal = m.AddMat("Body", MatKind.Palette, 0.55f);
            var shell = m.AddMat("Shell", MatKind.Palette, 0.45f);
            var bodyPart = m.AddPart("Body", pal, V3.Zero);
            Col flesh = C(0xA89A84), flesh2 = C(0x8E8070);
            // The foot: a long low body, a raised head with two long eye stalks and two short feelers.
            var pts = new[] { P(0f, 0.0010f, -0.0150f), P(0f, 0.0028f, -0.0085f), P(0f, 0.0034f, -0.0010f), P(0f, 0.0036f, 0.0060f), P(0f, 0.0058f, 0.0110f), P(0f, 0.0074f, 0.0128f) };
            var rx = new[] { 0.0004f, 0.0040f, 0.0050f, 0.0044f, 0.0034f, 0.0026f };
            var ry = new[] { 0.0004f, 0.0022f, 0.0028f, 0.0026f, 0.0024f, 0.0020f };
            Gm.Loft(bodyPart, pts, rx, ry, V3.Up, flesh, 10);
            Gm.Ellipsoid(bodyPart, P(0f, 0.0004f, -0.0010f), 0.0046f, 0.0006f, 0.0140f, flesh2, 10, 4); // the pale sole
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                V3 a = P(0.0011f * sgn, 0.0088f, 0.0120f), b = P(0.0028f * sgn, 0.0138f, 0.0170f);
                Gm.Stick(bodyPart, a, b, 0.0007f, 0.0005f, flesh2, 5);
                Gm.Ellipsoid(bodyPart, b, 0.0009f, 0.0009f, 0.0009f, C(0x1A1612), 5, 3);
                Gm.Stick(bodyPart, P(0.0008f * sgn, 0.0068f, 0.0138f), P(0.0016f * sgn, 0.0060f, 0.0170f), 0.0005f, 0.0003f, flesh2, 4);
            }
            // The shell: a tube coiled in a flat spiral (about 2.3 whorls) lying on the animal's back, its axis across the body, apex outward.
            var sh = m.AddPart("Shell", shell, V3.Zero);
            int N = 34;
            var sp = new V3[N]; var sr = new float[N];
            float whorls = 2.3f, r0 = 0.0010f, growth = (float)Math.Log(1f / r0 * 0.0096f) / (whorls * 2f * (float)Math.PI);
            for (int i = 0; i < N; i++)
            {
                float a = whorls * 2f * (float)Math.PI * i / (N - 1);
                float R = r0 * (float)Math.Exp(growth * a);
                // The spiral lies in the y-z plane (axis along x), centred above the middle of the body.
                float cx = -0.0006f * i / (N - 1), cy = 0.0150f, cz = -0.0030f;
                sp[i] = P(cx, cy + (float)Math.Sin(a) * R * 0.95f, cz + (float)Math.Cos(a) * R);
                sr[i] = Math.Max(0.0003f, R * 0.46f);
            }
            Gm.Loft(sh, sp, sr, sr, V3.Right, C(0x9A7040), 10);
            // A white flared lip at the opening (the end of the tube).
            Gm.Ellipsoid(sh, sp[N - 1] + P(0f, -0.0008f, 0.0010f), 0.0042f, 0.0036f, 0.0030f, C(0xF2EEE2), 8, 5);
            m.AddGroup("Focus", V3.Zero);
            return m;
        }

        // ------------------------------------------------------------------ fungi

        private static void Mushroom(Part part, V3 baseP, float height, float capR, float tiltDeg, float yaw, Col cap, Col capCentre, Col stem, Col under, bool skirt, bool bulb, int wartCount, Col wart, Random rng)
        {
            Frame f = Frame.Euler(tiltDeg, yaw, 0f);
            float stemR = capR * (bulb ? 0.22f : 0.20f);
            // The stem: a slim cylinder, thicker at the base (a bulb for the amanita), with the cap sitting on top.
            var prof = bulb
                ? new[] { new V2(0f, 0f), new V2(stemR * 1.9f, 0f), new V2(stemR * 2.1f, height * 0.06f), new V2(stemR * 1.2f, height * 0.18f), new V2(stemR * 0.95f, height * 0.5f), new V2(stemR * 0.85f, height), new V2(0f, height) }
                : new[] { new V2(0f, 0f), new V2(stemR * 1.5f, 0f), new V2(stemR * 1.2f, height * 0.08f), new V2(stemR * 0.9f, height * 0.5f), new V2(stemR * 0.8f, height), new V2(0f, height) };
            Gm.Lathe(part, prof, f, baseP, stem, 10);
            if (skirt)
            {
                float ry = height * 0.78f;
                Gm.Lathe(part, new[] { new V2(stemR * 0.9f, ry + stemR * 0.5f), new V2(stemR * (bulb ? 2.6f : 1.7f), ry - stemR * (bulb ? 1.4f : 0.9f)), new V2(stemR * (bulb ? 2.8f : 1.8f), ry - stemR * (bulb ? 1.6f : 1.0f)), new V2(stemR * 0.9f, ry - stemR * 0.1f) }, f, baseP, C(0xF1EEDC), 12);
            }
            // The cap: an underside first (gills), then the dome.
            V3 top = baseP + f.Apply(P(0f, height, 0f));
            Gm.Lathe(part, new[] { new V2(0f, -0.001f), new V2(capR * 0.98f, -0.001f), new V2(capR * 1.0f, capR * 0.04f), new V2(capR * 0.95f, capR * 0.06f), new V2(0f, capR * 0.06f) }, f, top, under, 14);
            var dome = new V2[9];
            for (int i = 0; i < 9; i++)
            {
                float a = i / 8f * (float)Math.PI * 0.5f; // 0 at the rim, 90 degrees at the crown
                dome[i] = new V2(capR * (float)Math.Cos(a) * (1f + 0.0f), capR * 0.06f + capR * 0.50f * (float)Math.Sin(a));
            }
            dome[0] = new V2(capR * 1.0f, capR * 0.04f);
            Gm.Lathe(part, dome, f, top, cap, 14);
            // The darker crown.
            var crown = new V2[5];
            for (int i = 0; i < 5; i++)
            {
                float a = (0.62f + 0.38f * i / 4f) * (float)Math.PI * 0.5f;
                crown[i] = new V2(capR * 1.0f * (float)Math.Cos(a) * 1.004f, capR * 0.06f + capR * 0.50f * (float)Math.Sin(a) * 1.004f);
            }
            if (capCentre.r >= 0f) Gm.Lathe(part, new[] { new V2(crown[0].x, crown[0].y), crown[1], crown[2], crown[3], crown[4] }, f, top, capCentre, 14);
            for (int w = 0; w < wartCount; w++)
            {
                float a = (0.12f + 0.80f * (float)Math.Sqrt((w + 0.5f) / wartCount)) * (float)Math.PI * 0.5f; // from the crown outward
                float th = w * 2.39996f;
                float rr = capR * (float)Math.Cos(a) * 1.0f, yy = capR * 0.06f + capR * 0.50f * (float)Math.Sin(a);
                V3 loc = P(rr * (float)Math.Cos(th), yy, rr * (float)Math.Sin(th));
                V3 nrm = P(loc.x / (capR * capR), (loc.y - capR * 0.06f) / (capR * 0.5f * capR * 0.5f), loc.z / (capR * capR)).Normalized;
                float wr = capR * (0.062f + 0.03f * (float)rng.NextDouble());
                Gm.Ellipsoid(part, top + f.Apply(loc) + f.Apply(nrm) * (wr * 0.15f), new V3(wr, wr, wr * 0.45f), Frame.Look(f.Apply(nrm), V3.Up), wart, 6, 3);
            }
        }

        private static Model HoneyMushrooms(string key, float S)
        {
            var m = new Model { key = key, size = S };
            var pal = m.AddMat("Fungus", MatKind.Palette, 0.25f);
            var part = m.AddPart("Cluster", pal, V3.Zero);
            var rng = new Random(11);
            Gm.Ellipsoid(part, P(0f, 0.002f, 0f), 0.014f, 0.007f, 0.014f, C(0x4A3622), 8, 4); // the dark base the stems grow from
            float[,] spec = { { 0.045f, 0.019f, 14f, 0f, 0.000f, 0.006f }, { 0.060f, 0.022f, 10f, 70f, 0.012f, 0.002f }, { 0.052f, 0.017f, 18f, 150f, 0.010f, -0.010f }, { 0.040f, 0.015f, 22f, 230f, -0.004f, -0.014f }, { 0.056f, 0.020f, 12f, 300f, -0.012f, 0.004f }, { 0.034f, 0.013f, 26f, 20f, 0.014f, -0.008f } };
            for (int i = 0; i < spec.GetLength(0); i++)
            {
                float yaw = spec[i, 3];
                var tilt = Frame.Euler(spec[i, 2], yaw, 0f);
                Mushroom(part, P(spec[i, 4], 0.004f, spec[i, 5]), spec[i, 0], spec[i, 1], spec[i, 2], yaw, C(0xC09040), C(0x7A4E1E), C(0xCDB890), C(0xE8D8B0), true, false, 0, Col.White, rng);
            }
            m.AddGroup("Focus", V3.Zero);
            return m;
        }

        private static Model FlyAgaric(string key, float S)
        {
            var m = new Model { key = key, size = S };
            var pal = m.AddMat("Fungus", MatKind.Palette, 0.3f);
            var part = m.AddPart("Cluster", pal, V3.Zero);
            var rng = new Random(23);
            float[,] spec = { { 0.085f, 0.048f, 3f, 0f, 0f, 0f }, { 0.060f, 0.034f, 12f, 120f, 0.060f, 0.030f }, { 0.040f, 0.024f, 20f, 240f, -0.045f, 0.045f } };
            for (int i = 0; i < spec.GetLength(0); i++)
                Mushroom(part, P(spec[i, 4], 0f, spec[i, 5]), spec[i, 0], spec[i, 1], spec[i, 2], spec[i, 3], C(0xEE9A22), C(0xE07A10), C(0xF3F0E0), C(0xF4EFD8), true, true, i == 2 ? 10 : 22, C(0xF8F2D2), rng);
            m.AddGroup("Focus", V3.Zero);
            return m;
        }

        private static Model TurkeyTail(string key, float S)
        {
            var m = new Model { key = key, size = S };
            var band = m.AddMat("Bands", MatKind.PaintedOpaque, 0.15f);
            band.w = 128; band.h = 16;
            band.pixels = Sh.Paint(band.w, band.h, (u, v) =>
            {
                if (u > 0.95f) return C(0xE9DFC8); // the pale underside
                float r = u / 0.95f;
                Col[] ring = { C(0x5A4332), C(0x8A7458), C(0x3F4A52), C(0xA86A3A), C(0x6E7C80), C(0x4A3A2C), C(0x9A8A6A), C(0x2F3A44), C(0xB0794A), C(0xE6DCC0) };
                float f = r * (ring.Length - 1) + (Sh.Noise(r * 8f, v * 9f, 4) - 0.5f) * 0.8f;
                int i = Math.Max(0, Math.Min(ring.Length - 2, (int)Math.Floor(f)));
                float t = Math.Max(0f, Math.Min(1f, f - i));
                return Col.Lerp(ring[i], ring[i + 1], Sh.Smooth(0.35f, 0.65f, t));
            });
            var part = m.AddPart("Shelf", band, V3.Zero);
            var rng = new Random(5);
            int fans = 7;
            for (int i = 0; i < fans; i++)
            {
                float R = Rf(rng, 0.020f, 0.031f), yaw = i * 360f / fans + Rf(rng, -18f, 18f);
                V3 c = P(0.011f * (float)Math.Cos(yaw * Math.PI / 180f) * (i % 2 == 0 ? 1f : 1.5f), 0.002f + 0.0016f * (i % 3), 0.011f * (float)Math.Sin(yaw * Math.PI / 180f));
                Fan(part, c, R, yaw, Rf(rng, -5f, 9f), rng, i);
            }
            m.AddGroup("Focus", V3.Zero);
            return m;
        }

        /// <summary>One bracket: a thin half-disc, rooted at its centre, ruffled at the rim, banded by radius (u = radius) and pale underneath.</summary>
        private static void Fan(Part part, V3 root, float R, float yawDeg, float tiltDeg, Random rng, int seed)
        {
            Frame f = Frame.Euler(tiltDeg, yawDeg, 0f);
            int A = 14, Rn = 6;
            float span = 150f * (float)Math.PI / 180f;
            float phase = (float)rng.NextDouble() * 6f;
            // Top surface then underside; the vertices share a thin gap.
            for (int side = 0; side < 2; side++)
            {
                int b0 = part.v.Count;
                for (int ri = 0; ri <= Rn; ri++)
                    for (int ai = 0; ai <= A; ai++)
                    {
                        float rr = ri / (float)Rn;
                        float a = -span * 0.5f + span * ai / A;
                        float ruffle = 1f + 0.06f * (float)Math.Sin(a * 5f + phase) * rr;
                        float r = R * rr * ruffle;
                        float droop = -0.004f * rr * rr;
                        float lift = side == 0 ? 0.0012f * (1f - rr * 0.6f) : -0.0004f;
                        V3 loc = P(r * (float)Math.Sin(a), droop + lift, r * (float)Math.Cos(a));
                        V3 nrm = P(0f, side == 0 ? 1f : -1f, 0f);
                        part.v.Add(root + f.Apply(loc));
                        part.n.Add(f.Apply(nrm).Normalized);
                        part.uv.Add(side == 0 ? new V2(Math.Min(0.94f, rr * 0.94f), 0.2f + 0.6f * ai / A) : new V2(0.99f, 0.5f));
                    }
                for (int ri = 0; ri < Rn; ri++)
                    for (int ai = 0; ai < A; ai++)
                    {
                        int a = b0 + ri * (A + 1) + ai, b = a + 1, c = a + A + 1, d = c + 1;
                        TriFan(part, a, b, c); TriFan(part, b, d, c);
                    }
            }
        }

        private static void TriFan(Part p, int a, int b, int c)
        {
            V3 cr = V3.Cross(p.v[b] - p.v[a], p.v[c] - p.v[a]);
            if (cr.Length < 1e-14f) return;
            if (V3.Dot(cr, p.n[a] + p.n[b] + p.n[c]) < 0f) { int t = b; b = c; c = t; }
            p.t.Add(a); p.t.Add(b); p.t.Add(c);
        }

        // ------------------------------------------------------------------ flowers and plants

        private static void Stem(Part plant, V3 bottom, V3 top, float r0, float r1, Col col, float bow)
        {
            V3 mid = (bottom + top) * 0.5f + P(bow, 0f, 0f);
            Gm.Loft(plant, new[] { bottom, V3.Lerp(bottom, mid, 0.9f) + P(0f, 0.0f, 0f), mid, V3.Lerp(mid, top, 0.9f), top }, new[] { r0, (r0 * 3 + r1) / 4f, (r0 + r1) / 2f, (r0 + r1 * 3) / 4f, r1 },
                new[] { r0, (r0 * 3 + r1) / 4f, (r0 + r1) / 2f, (r0 + r1 * 3) / 4f, r1 }, V3.Fwd, col, 6, false, false);
        }

        /// <summary>A daisy-family flower: kind 0 oxeye daisy (white rays, yellow disc), 1 black-eyed Susan (golden rays, dark domed disc).</summary>
        private static Model DaisyLike(string key, float S, int kind)
        {
            var m = new Model { key = key, size = S };
            var pal = m.AddMat("Plant", MatKind.PaletteDouble, 0.2f);
            var plant = m.AddPart("Plant", pal, V3.Zero);
            plant.noMeasure = true;
            var head = m.AddPart("Head", pal, V3.Zero);
            var rng = new Random(kind == 0 ? 3 : 8);
            float height = kind == 0 ? 0.45f : 0.55f;
            float rayLen = S * 0.5f;
            Col green = kind == 0 ? C(0x4F7A2E) : C(0x5A7A30), leaf = kind == 0 ? C(0x3F6E26) : C(0x557A2C);
            V3 top = P(0.02f, height, 0f);
            Stem(plant, P(0f, 0f, 0f), top, 0.0016f, 0.0012f, green, 0.02f);
            // Leaves: a few along the stem, narrow and pointed (daisy: toothed spoons low down; susan: broader, hairy).
            for (int i = 0; i < 5; i++)
            {
                float t = 0.04f + i * 0.12f, yaw = i * 137f + (kind * 40f);
                V3 d = P((float)Math.Cos(yaw * Math.PI / 180f), 0.35f - i * 0.05f, (float)Math.Sin(yaw * Math.PI / 180f));
                Gm.Leaf(plant, P(0.02f * t, height * t, 0f), d, (kind == 0 ? 0.065f : 0.075f) * (1f - i * 0.12f), kind == 0 ? 0.012f : 0.020f, 0.012f, leaf, 5, 0.7f);
            }
            // The flower head: rays around a disc, facing up and a little toward the viewer.
            Frame hf = Frame.Euler(-12f, 20f, 0f);
            V3 hc = top + P(0f, 0.004f, 0f);
            int rays = kind == 0 ? 21 : 13;
            Col rayCol = kind == 0 ? C(0xF8F6EC) : C(0xF0A912), rayEdge = kind == 0 ? C(0xE8E6D8) : C(0xF7B81F);
            for (int i = 0; i < rays; i++)
            {
                float a = i * 360f / rays + (float)rng.NextDouble() * 4f;
                float len = rayLen * (kind == 0 ? 0.62f : 0.55f) * (0.92f + 0.16f * (float)rng.NextDouble());
                float wd = kind == 0 ? rayLen * 0.10f : rayLen * 0.17f;
                V3 dir = hf.Apply(P((float)Math.Cos(a * Math.PI / 180f), 0.10f, (float)Math.Sin(a * Math.PI / 180f)));
                V3 side = V3.Cross(hf.y, dir).Normalized;
                V3 r0 = hc + dir * (rayLen * (kind == 0 ? 0.30f : 0.26f));
                V3[] pts = { r0, r0 + dir * (len * 0.45f) + hf.y * 0.0003f, r0 + dir * len };
                Gm.Strip(head, pts, new[] { wd * 0.4f, wd, 0.0004f }, side, i % 2 == 0 ? rayCol : rayEdge);
            }
            if (kind == 0)
            {
                Gm.Ellipsoid(head, hc + hf.y * 0.0012f, new V3(rayLen * 0.30f, rayLen * 0.16f, rayLen * 0.30f), hf, C(0xF0C21A), 10, 5);
                Gm.Ellipsoid(head, hc + hf.y * 0.0016f, new V3(rayLen * 0.20f, rayLen * 0.12f, rayLen * 0.20f), hf, C(0xE8A810), 8, 4);
            }
            else
            {
                Gm.Ellipsoid(head, hc + hf.y * 0.002f, new V3(rayLen * 0.27f, rayLen * 0.26f, rayLen * 0.27f), hf, C(0x2A180C), 10, 6);
                Gm.Ellipsoid(head, hc + hf.y * 0.004f, new V3(rayLen * 0.18f, rayLen * 0.20f, rayLen * 0.18f), hf, C(0x3A2210), 8, 5);
            }
            m.AddGroup("Focus", V3.Zero);
            return m;
        }

        private static Model Milkweed(string key, float S)
        {
            var m = new Model { key = key, size = S };
            var pal = m.AddMat("Plant", MatKind.PaletteDouble, 0.25f);
            var plant = m.AddPart("Plant", pal, V3.Zero);
            plant.noMeasure = true;
            var head = m.AddPart("Head", pal, V3.Zero);
            var rng = new Random(31);
            float height = 0.95f;
            Col stem = C(0x6C8B4A), leaf = C(0x4C7A3A), leafUnder = C(0x7E9C68);
            V3 top = P(0.03f, height, 0f);
            Stem(plant, P(0f, 0f, 0f), top, 0.0045f, 0.0030f, stem, 0.03f);
            // Big oval leaves in opposite pairs, drooping at their tips.
            for (int i = 0; i < 6; i++)
            {
                float t = 0.22f + i * 0.12f;
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    float yaw = (i % 2 == 0 ? 0f : 90f) + (sgn > 0 ? 0f : 180f);
                    V3 d = P((float)Math.Cos(yaw * Math.PI / 180f), 0.18f, (float)Math.Sin(yaw * Math.PI / 180f));
                    Gm.Leaf(plant, P(0.03f * t, height * t, 0f), d, 0.17f - i * 0.012f, 0.075f - i * 0.004f, 0.06f, i % 2 == 0 ? leaf : Col.Lerp(leaf, leafUnder, 0.3f), 6, 0.9f);
                }
            }
            // The umbel: a loose ball of small star-shaped florets on thin stalks.
            V3 hc = top + P(0f, 0.045f, 0f);
            Gm.Stick(head, top, hc, 0.0022f, 0.0018f, stem, 5);
            Col[] pinks = { C(0xC9728E), C(0xD98AA0), C(0xB85F7E), C(0xE4A2B2) };
            for (int i = 0; i < 110; i++)
            {
                float th = i * 2.39996f, y = 1f - 2f * (i + 0.5f) / 110f, r = (float)Math.Sqrt(1f - y * y);
                V3 dir = P(r * (float)Math.Cos(th), y * 0.8f, r * (float)Math.Sin(th));
                if (dir.y < -0.45f) continue;
                V3 pos = hc + dir * 0.030f;
                Gm.Stick(head, hc, pos, 0.0004f, 0.0003f, C(0x8EA070), 3);
                float cr = 0.0046f + 0.0012f * (float)rng.NextDouble();
                Gm.Ellipsoid(head, pos + dir * 0.002f, new V3(cr, cr, cr * 0.7f), Frame.Look(dir, V3.Up), pinks[i % pinks.Length], 7, 4);
                Gm.Ellipsoid(head, pos + dir * (0.0035f), new V3(cr * 0.55f, cr * 0.55f, cr * 0.5f), Frame.Look(dir, V3.Up), C(0xF2DCE4), 5, 3); // the pale corona
            }
            m.AddGroup("Focus", V3.Zero);
            return m;
        }

        private static Model Violet(string key, float S)
        {
            var m = new Model { key = key, size = S };
            var pal = m.AddMat("Plant", MatKind.PaletteDouble, 0.3f);
            var plant = m.AddPart("Plant", pal, V3.Zero);
            plant.noMeasure = true;
            var head = m.AddPart("Head", pal, V3.Zero);
            var rng = new Random(41);
            Col leaf = C(0x3F7030), leaf2 = C(0x4C8038), stem = C(0x6A8E4C), petal = C(0x6A4FA8), petal2 = C(0x7C62BC), throat = C(0xF1EFF4);
            // Heart-shaped leaves, low in a rosette, on short stalks.
            for (int i = 0; i < 6; i++)
            {
                float yaw = i * 60f + 15f;
                V3 d = P((float)Math.Cos(yaw * Math.PI / 180f), 0.0f, (float)Math.Sin(yaw * Math.PI / 180f));
                V3 stalkTop = d * 0.030f + P(0f, 0.050f - 0.006f * (i % 3), 0f);
                Gm.Stick(plant, P(0f, 0f, 0f), stalkTop, 0.0010f, 0.0008f, stem, 4);
                // The blade: two lobes either side of a notch, a point at the tip.
                V3 side = V3.Cross(V3.Up, d).Normalized;
                var outline = new[]
                {
                    P(0f, 0f, 0f), P(0.5f, 0f, 0.55f), P(0.95f, 0f, 0.30f), P(1.15f, 0f, -0.10f), P(0.95f, 0f, -0.55f), P(0.5f, 0f, -0.70f), P(0f, 0f, -0.45f),
                };
                // Built as a fan of six triangles from the notch in the plane of the leaf, dipping toward the tip.
                int b0 = plant.v.Count;
                float sc = 0.034f;
                V3 root = stalkTop;
                var pts = new List<V3>();
                pts.Add(root);
                foreach (var o in new[] { P(0f, 0f, 0.55f), P(0.45f, 0f, 0.80f), P(0.95f, 0f, 0.55f), P(1.25f, 0f, 0.0f), P(0.95f, 0f, -0.55f), P(0.45f, 0f, -0.80f), P(0f, 0f, -0.55f) })
                    pts.Add(root + d * (o.x * sc) + side * (o.z * sc) + P(0f, -0.004f * o.x, 0f));
                for (int k = 0; k < pts.Count; k++)
                {
                    plant.v.Add(pts[k]); plant.n.Add(V3.Up); plant.uv.Add(new V2(plant.mat.PalIndex(i % 2 == 0 ? leaf : leaf2), 0.5f));
                }
                for (int k = 1; k < pts.Count - 1; k++) TriFan(plant, b0, b0 + k, b0 + k + 1);
            }
            // Three flowers on slender stalks, each of five petals (two up, two out, one lip down).
            for (int fI = 0; fI < 3; fI++)
            {
                float yaw = fI * 120f + 40f;
                V3 d = P((float)Math.Cos(yaw * Math.PI / 180f), 0f, (float)Math.Sin(yaw * Math.PI / 180f));
                V3 stalkTop = d * (0.012f + 0.004f * fI) + P(0f, 0.070f + 0.010f * (fI % 2), 0f);
                Gm.Stick(plant, P(0f, 0f, 0f), stalkTop, 0.0007f, 0.0006f, stem, 4);
                Frame f = Frame.Look(d * 0.7f + P(0f, 0.25f, 0f), V3.Up); // the flower faces outward and up
                V3 c = stalkTop;
                float pr = S * 0.5f;
                float[] ang = { 40f, -40f, 100f, -100f, 180f };
                float[] ln = { 0.85f, 0.85f, 0.95f, 0.95f, 1.0f };
                for (int k = 0; k < 5; k++)
                {
                    float a = ang[k] * (float)Math.PI / 180f;
                    V3 dir = f.Apply(P((float)Math.Sin(a), (float)Math.Cos(a), 0f)); // petals fan in the face's plane (x right, y up)
                    V3 side = V3.Cross(f.z, dir).Normalized;
                    V3 r0 = c + dir * (pr * 0.15f);
                    V3[] p3 = { r0, r0 + dir * (pr * ln[k] * 0.5f) + f.z * 0.0005f, r0 + dir * (pr * ln[k]) };
                    Gm.Strip(head, p3, new[] { pr * 0.22f, pr * 0.48f, pr * 0.20f }, side, k < 4 ? (k % 2 == 0 ? petal : petal2) : petal2);
                }
                Gm.Ellipsoid(head, c + f.z * 0.0004f, pr * 0.14f, pr * 0.14f, pr * 0.05f, throat, 6, 3);
            }
            m.AddGroup("Focus", V3.Zero);
            return m;
        }

        private static Model Moss(string key, float S)
        {
            var m = new Model { key = key, size = S };
            var pal = m.AddMat("Moss", MatKind.PaletteDouble, 0.1f);
            var moss = m.AddPart("Moss", pal, V3.Zero);
            var rng = new Random(61);
            Col[] greens = { C(0x56902E), C(0x66A038), C(0x468226), C(0x78AE44), C(0x5A9A30) };
            float R = S * 0.5f;
            Gm.Ellipsoid(moss, P(0f, 0.002f, 0f), R * 0.95f, 0.0045f, R * 0.95f, C(0x3E6A28), 12, 4); // the dense cushion under the shoots
            // Hundreds of fine star-leaved shoots standing up: each a thin blade bundle, tallest at the middle.
            for (int i = 0; i < 190; i++)
            {
                float a = (float)rng.NextDouble() * 6.2832f, rr = R * 0.92f * (float)Math.Sqrt(rng.NextDouble());
                float hgt = (0.010f + 0.012f * (1f - rr / R)) * (0.7f + 0.6f * (float)rng.NextDouble()) * (S / 0.08f);
                V3 b = P(rr * (float)Math.Cos(a), 0.003f, rr * (float)Math.Sin(a));
                Col g = greens[rng.Next(greens.Length)];
                for (int bl = 0; bl < 3; bl++)
                {
                    float ba = bl * 2.094f + a;
                    V3 lean = P((float)Math.Cos(ba) * 0.0035f, 0f, (float)Math.Sin(ba) * 0.0035f);
                    V3 tip = b + P(0f, hgt, 0f) + lean + P((float)Math.Cos(a) * 0.003f * (rr / R), 0f, (float)Math.Sin(a) * 0.003f * (rr / R));
                    V3 sd = V3.Cross(V3.Up, lean).Normalized;
                    Gm.Strip(moss, new[] { b, V3.Lerp(b, tip, 0.5f), tip }, new[] { 0.0014f, 0.0016f, 0.0002f }, sd, g);
                }
            }
            // A few capsules on slender reddish stalks.
            for (int i = 0; i < 4; i++)
            {
                float a = (float)rng.NextDouble() * 6.2832f, rr = R * 0.6f * (float)Math.Sqrt(rng.NextDouble());
                V3 b = P(rr * (float)Math.Cos(a), 0.004f, rr * (float)Math.Sin(a));
                float hgt = 0.040f * (S / 0.08f) * (0.8f + 0.4f * (float)rng.NextDouble());
                V3 tip = b + P((float)Math.Cos(a + 1.5f) * 0.004f, hgt, (float)Math.Sin(a + 1.5f) * 0.004f);
                Gm.Stick(moss, b, tip, 0.00028f, 0.00022f, C(0xB8682E), 4);
                Gm.Ellipsoid(moss, tip + P(0f, 0.0016f, 0f), 0.0011f, 0.0022f, 0.0011f, C(0x8A6A30), 6, 4);
                Gm.Ellipsoid(moss, tip + P(0f, 0.0036f, 0f), 0.0014f, 0.0009f, 0.0014f, C(0xE6CC88), 6, 3); // the pale hairy cap
            }
            m.AddGroup("Focus", V3.Zero);
            return m;
        }

        private static Model Cattail(string key, float S)
        {
            var m = new Model { key = key, size = S };
            var pal = m.AddMat("Plant", MatKind.PaletteDouble, 0.2f);
            var plant = m.AddPart("Plant", pal, V3.Zero);
            plant.noMeasure = true;
            var head = m.AddPart("Head", pal, V3.Zero);
            float height = 1.45f;
            Col stem = C(0x7A8A3E), leaf = C(0x6C8238), brown = C(0x4A2C18), spike = C(0xB09A62);
            Stem(plant, P(0f, 0f, 0f), P(0.01f, height, 0f), 0.0065f, 0.0045f, stem, 0.04f);
            // Long strap leaves rising beside the stem and arching over.
            for (int i = 0; i < 4; i++)
            {
                float yaw = i * 95f + 20f;
                V3 d = P((float)Math.Cos(yaw * Math.PI / 180f), 0.0f, (float)Math.Sin(yaw * Math.PI / 180f));
                Gm.Leaf(plant, P(d.x * 0.01f, 0f, d.z * 0.01f), d * 0.35f + P(0f, 1f, 0f), 1.05f - i * 0.1f, 0.026f, 0.30f, leaf, 8, 0.45f);
            }
            // The brown sausage of tiny flowers with a pale spike above it.
            V3 bot = P(0.01f, height - 0.16f, 0f);
            Gm.Lathe(head, new[] { new V2(0f, 0f), new V2(0.0095f, 0.0f), new V2(0.0115f, 0.04f), new V2(0.0120f, 0.09f), new V2(0.0108f, 0.130f), new V2(0.0050f, 0.142f), new V2(0f, 0.144f) }, Frame.Identity, bot, brown, 10);
            Gm.Stick(head, bot + P(0f, 0.140f, 0f), bot + P(0f, 0.205f, 0f), 0.0030f, 0.0010f, spike, 5);
            m.AddGroup("Focus", V3.Zero);
            return m;
        }
    }
}
