using System;
using System.Collections.Generic;

// Pure geometry for the generated small life (1.9). This file and SmallLifeShapes.cs use NOTHING from UnityEngine, so the
// vertex maths and the painted textures can be run and looked at outside Unity (a throwaway console harness compiles exactly
// these two files). SmallLifeModels.cs is the thin Unity layer that turns the result into meshes, materials and prefabs.
//
// Conventions: metres; +Z forward (the head), +Y up, +X the model's right; the pivot is at the feet or attachment point;
// triangles are wound so that (b - a) x (c - a) points toward the front face (Unity's clockwise-front rule).
namespace WildLens.EditorTools.SmallLife
{
    public struct V3
    {
        public float x, y, z;
        public V3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static V3 operator +(V3 a, V3 b) => new V3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static V3 operator -(V3 a, V3 b) => new V3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static V3 operator -(V3 a) => new V3(-a.x, -a.y, -a.z);
        public static V3 operator *(V3 a, float k) => new V3(a.x * k, a.y * k, a.z * k);
        public static V3 operator *(float k, V3 a) => new V3(a.x * k, a.y * k, a.z * k);
        public static V3 operator /(V3 a, float k) => new V3(a.x / k, a.y / k, a.z / k);
        public static float Dot(V3 a, V3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static V3 Cross(V3 a, V3 b) => new V3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public float Length => (float)Math.Sqrt(x * x + y * y + z * z);
        public V3 Normalized { get { float l = Length; return l < 1e-9f ? new V3(0f, 1f, 0f) : new V3(x / l, y / l, z / l); } }
        public static V3 Lerp(V3 a, V3 b, float t) => a + (b - a) * t;
        public static readonly V3 Zero = new V3(0f, 0f, 0f), Up = new V3(0f, 1f, 0f), Fwd = new V3(0f, 0f, 1f), Right = new V3(1f, 0f, 0f);
    }

    public struct V2
    {
        public float x, y;
        public V2(float x, float y) { this.x = x; this.y = y; }
    }

    public struct Col
    {
        public float r, g, b, a;
        public Col(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Col Hex(int rgb) => new Col(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);
        public static Col Lerp(Col x, Col y, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return new Col(x.r + (y.r - x.r) * t, x.g + (y.g - x.g) * t, x.b + (y.b - x.b) * t, x.a + (y.a - x.a) * t);
        }
        public Col Mul(float k) => new Col(r * k, g * k, b * k, a);
        public Col WithAlpha(float al) => new Col(r, g, b, al);
        public static readonly Col White = new Col(1f, 1f, 1f), Black = new Col(0f, 0f, 0f);
    }

    /// <summary>A rotated basis (local x, y, z in model space).</summary>
    public struct Frame
    {
        public V3 x, y, z;
        public static Frame Identity => new Frame { x = V3.Right, y = V3.Up, z = V3.Fwd };
        public V3 Apply(V3 l) => x * l.x + y * l.y + z * l.z;

        public static V3 Rot(V3 v, V3 axis, float deg)
        {
            axis = axis.Normalized;
            float a = deg * (float)Math.PI / 180f, c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            return v * c + V3.Cross(axis, v) * s + axis * (V3.Dot(axis, v) * (1f - c));
        }

        /// <summary>Unity's Euler order: roll about Z first, then pitch about X, then yaw about Y (all about the world axes).</summary>
        public static Frame Euler(float pitch, float yaw, float roll)
        {
            var f = Identity;
            f.x = Rot(Rot(Rot(f.x, V3.Fwd, roll), V3.Right, pitch), V3.Up, yaw);
            f.y = Rot(Rot(Rot(f.y, V3.Fwd, roll), V3.Right, pitch), V3.Up, yaw);
            f.z = Rot(Rot(Rot(f.z, V3.Fwd, roll), V3.Right, pitch), V3.Up, yaw);
            return f;
        }

        /// <summary>Basis whose z axis points along <paramref name="dir"/> (y as near <paramref name="up"/> as possible).</summary>
        public static Frame Look(V3 dir, V3 up)
        {
            V3 z = dir.Normalized;
            V3 x = V3.Cross(up, z);
            if (x.Length < 1e-5f) x = V3.Cross(Math.Abs(z.y) > 0.9f ? V3.Fwd : V3.Up, z);
            x = x.Normalized;
            return new Frame { x = x, y = V3.Cross(z, x).Normalized, z = z };
        }
    }

    public enum MatKind
    {
        Palette,        // opaque, colour from a tiny palette texture (one cell per colour)
        PaletteDouble,  // the same, two-sided (leaves, petals)
        Painted,        // painted texture with alpha cut-out, two-sided (wings)
        PaintedOpaque,  // painted texture, opaque, one-sided (frog skin, newt skin, shelf fungi)
        Glow,           // palette plus an emission the runtime can pulse (a firefly's lantern)
        Additive,       // unlit additive sprite (a firefly's halo)
    }

    public sealed class Mat
    {
        public string name;
        public MatKind kind;
        public float smoothness = 0.15f;
        public Col emission = Col.Black;
        public List<Col> palette = new List<Col>();
        public int w, h;
        public Col[] pixels;

        public bool IsPalette => kind == MatKind.Palette || kind == MatKind.PaletteDouble || kind == MatKind.Glow;

        public int PalIndex(Col c)
        {
            for (int i = 0; i < palette.Count; i++)
            {
                Col p = palette[i];
                if (p.r == c.r && p.g == c.g && p.b == c.b) return i;
            }
            palette.Add(c);
            return palette.Count - 1;
        }

        /// <summary>The palette strip as pixels: width a power of two (at least 4), one row, point-sampled by the Unity layer.</summary>
        public int PaletteWidth { get { int n = 4; while (n < palette.Count) n *= 2; return n; } }
    }

    public sealed class Part
    {
        public string name, parent = "";
        public V3 origin;
        public float restRoll;       // degrees about the part's Z axis in the saved pose (wings held up or back)
        public Mat mat;
        public bool empty;           // a transform with no mesh (a group such as "Rider")
        public bool noMeasure;       // left out of the model's measured size (halos)
        public List<V3> v = new List<V3>(), n = new List<V3>();
        public List<V2> uv = new List<V2>();
        public List<int> t = new List<int>();
        public Func<V3, V3, V2> uvFn;  // optional: UV from position and normal (frog skin), applied once the part is finished
    }

    public sealed class Model
    {
        public string key;
        public float size;     // the authoring size: the subject's size is applied relative to this
        public List<Part> parts = new List<Part>();
        public List<Mat> mats = new List<Mat>();

        public Mat AddMat(string name, MatKind kind, float smooth = 0.15f)
        {
            var m = new Mat { name = key + "_" + name, kind = kind, smoothness = smooth };
            mats.Add(m);
            return m;
        }

        public Part AddPart(string name, Mat mat, V3 origin, string parent = "")
        {
            var p = new Part { name = name, mat = mat, origin = origin, parent = parent };
            parts.Add(p);
            return p;
        }

        public Part AddGroup(string name, V3 origin, string parent = "")
        {
            var p = new Part { name = name, origin = origin, parent = parent, empty = true };
            parts.Add(p);
            return p;
        }

        public Part Find(string name) { foreach (var p in parts) if (p.name == name) return p; return null; }

        /// <summary>Finishes parts: UV functions applied, palette cells turned into texture coordinates, vertices made local to the part's origin.</summary>
        public void Finish()
        {
            foreach (var p in parts)
            {
                if (p.empty) continue;
                for (int i = 0; i < p.v.Count; i++)
                {
                    if (p.uvFn != null) p.uv[i] = p.uvFn(p.v[i], p.n[i]);
                    else if (p.mat != null && p.mat.IsPalette) p.uv[i] = new V2((p.uv[i].x + 0.5f) / p.mat.PaletteWidth, 0.5f);
                    p.v[i] = p.v[i] - p.origin;
                }
            }
        }

        /// <summary>Largest side of the whole model's box, with every part at its saved pose (halos excluded).</summary>
        public void Measure(out V3 min, out V3 max)
        {
            min = new V3(1e9f, 1e9f, 1e9f);
            max = new V3(-1e9f, -1e9f, -1e9f);
            foreach (var p in parts)
            {
                if (p.empty || p.noMeasure) continue;
                foreach (var l in p.v)
                {
                    V3 w = l;
                    if (Math.Abs(p.restRoll) > 0.001f) w = Frame.Rot(l, V3.Fwd, p.restRoll);
                    w = w + p.origin;
                    min = new V3(Math.Min(min.x, w.x), Math.Min(min.y, w.y), Math.Min(min.z, w.z));
                    max = new V3(Math.Max(max.x, w.x), Math.Max(max.y, w.y), Math.Max(max.z, w.z));
                }
            }
        }
    }

    /// <summary>Mesh primitives that write into a <see cref="Part"/> in model space.</summary>
    public static class Gm
    {
        private const float Eps = 1e-5f;

        private static void Tri(Part p, int a, int b, int c)
        {
            V3 e1 = p.v[b] - p.v[a], e2 = p.v[c] - p.v[a];
            V3 cr = V3.Cross(e1, e2);
            if (cr.Length < 1e-14f) return; // degenerate (a pole or a collapsed ring)
            V3 nn = p.n[a] + p.n[b] + p.n[c];
            if (V3.Dot(cr, nn) < 0f) { int tmp = b; b = c; c = tmp; }
            p.t.Add(a); p.t.Add(b); p.t.Add(c);
        }

        private static int Vtx(Part p, V3 pos, V3 normal, Col col, float u, float v)
        {
            p.v.Add(pos);
            p.n.Add(normal.Normalized);
            // Palette parts carry the palette cell index in uv.x until Model.Finish; painted parts carry real UVs.
            p.uv.Add(p.mat != null && p.mat.IsPalette ? new V2(p.mat.PalIndex(col), 0.5f) : new V2(u, v));
            return p.v.Count - 1;
        }

        public static void Ellipsoid(Part p, V3 c, V3 r, Frame f, Col col, int seg = 10, int rings = 6)
        {
            int b0 = p.v.Count;
            for (int i = 0; i <= rings; i++)
            {
                float phi = (float)Math.PI * i / rings, sp = (float)Math.Sin(phi), cp = (float)Math.Cos(phi);
                for (int j = 0; j <= seg; j++)
                {
                    float th = 2f * (float)Math.PI * j / seg;
                    V3 l = new V3(r.x * sp * (float)Math.Cos(th), r.y * cp, r.z * sp * (float)Math.Sin(th));
                    V3 nl = new V3(l.x / (r.x * r.x), l.y / (r.y * r.y), l.z / (r.z * r.z));
                    Vtx(p, c + f.Apply(l), f.Apply(nl), col, j / (float)seg, 1f - i / (float)rings);
                }
            }
            for (int i = 0; i < rings; i++)
                for (int j = 0; j < seg; j++)
                {
                    int a = b0 + i * (seg + 1) + j, b = a + 1, cc = a + seg + 1, d = cc + 1;
                    Tri(p, a, b, cc); Tri(p, b, d, cc);
                }
        }

        public static void Ellipsoid(Part p, V3 c, float rx, float ry, float rz, Col col, int seg = 10, int rings = 6)
            => Ellipsoid(p, c, new V3(rx, ry, rz), Frame.Identity, col, seg, rings);

        /// <summary>A tube along a path with an elliptical section that changes along it (bodies, tails, stems, legs).</summary>
        public static void Loft(Part p, V3[] pts, float[] rx, float[] ry, V3 upHint, Col col, int seg = 8, bool capStart = true, bool capEnd = true)
        {
            int K = pts.Length;
            int b0 = p.v.Count;
            for (int k = 0; k < K; k++)
            {
                V3 t = (k == 0 ? pts[1] - pts[0] : k == K - 1 ? pts[K - 1] - pts[K - 2] : pts[k + 1] - pts[k - 1]).Normalized;
                V3 side = V3.Cross(upHint, t);
                if (side.Length < 1e-4f) side = V3.Cross(Math.Abs(t.z) < 0.9f ? V3.Fwd : V3.Right, t);
                side = side.Normalized;
                V3 up2 = V3.Cross(t, side).Normalized;
                float ax = Math.Max(rx[k], Eps), ay = Math.Max(ry[k], Eps);
                for (int j = 0; j <= seg; j++)
                {
                    float th = 2f * (float)Math.PI * j / seg, cs = (float)Math.Cos(th), sn = (float)Math.Sin(th);
                    V3 pos = pts[k] + side * (ax * cs) + up2 * (ay * sn);
                    V3 nn = side * (cs / ax) + up2 * (sn / ay);
                    Vtx(p, pos, nn, col, j / (float)seg, k / (float)(K - 1));
                }
            }
            for (int k = 0; k < K - 1; k++)
                for (int j = 0; j < seg; j++)
                {
                    int a = b0 + k * (seg + 1) + j, b = a + 1, cc = a + seg + 1, d = cc + 1;
                    Tri(p, a, b, cc); Tri(p, b, d, cc);
                }
            if (capStart) Cap(p, b0, pts[0], (pts[0] - pts[1]).Normalized, seg, col);
            if (capEnd) Cap(p, b0 + (K - 1) * (seg + 1), pts[K - 1], (pts[K - 1] - pts[K - 2]).Normalized, seg, col);
        }

        private static void Cap(Part p, int ringStart, V3 centre, V3 normal, int seg, Col col)
        {
            int c = Vtx(p, centre, normal, col, 0.5f, 0.5f);
            int r0 = p.v.Count;
            for (int j = 0; j <= seg; j++) Vtx(p, p.v[ringStart + j], normal, col, 0.5f, 0.5f);
            for (int j = 0; j < seg; j++) Tri(p, c, r0 + j, r0 + j + 1);
        }

        public static void Loft(Part p, V3[] pts, float r, Col col, int seg = 6) // constant round section
        {
            var rr = new float[pts.Length];
            for (int i = 0; i < rr.Length; i++) rr[i] = r;
            Loft(p, pts, rr, rr, Math.Abs((pts[pts.Length - 1] - pts[0]).Normalized.y) > 0.9f ? V3.Fwd : V3.Up, col, seg);
        }

        /// <summary>A thin round rod from a to b (legs, antennae, stalks, twigs).</summary>
        public static void Stick(Part p, V3 a, V3 b, float r0, float r1, Col col, int seg = 4)
        {
            V3 d = (b - a).Normalized;
            V3 hint = Math.Abs(d.y) > 0.9f ? V3.Fwd : V3.Up;
            Loft(p, new[] { a, b }, new[] { r0, r1 }, new[] { r0, r1 }, hint, col, seg);
        }

        /// <summary>A body of revolution about the local Y axis. The profile (radius, height) must run from the bottom centre outward,
        /// up the outside and inward over the top (counter-clockwise round the solid); a repeated point makes a hard edge.</summary>
        public static void Lathe(Part p, V2[] prof, Frame f, V3 origin, Col col, int seg = 12)
        {
            int P = prof.Length;
            var segN = new V2[P - 1];
            for (int i = 0; i < P - 1; i++)
            {
                float dr = prof[i + 1].x - prof[i].x, dy = prof[i + 1].y - prof[i].y;
                float l = (float)Math.Sqrt(dr * dr + dy * dy);
                segN[i] = l < 1e-9f ? new V2(1f, 0f) : new V2(dy / l, -dr / l);
            }
            for (int i = 0; i < P - 1; i++)
            {
                if (Math.Abs(prof[i + 1].x - prof[i].x) < 1e-9f && Math.Abs(prof[i + 1].y - prof[i].y) < 1e-9f) continue;
                V2 n0 = segN[i], n1 = segN[i];
                if (i > 0 && n0.x * segN[i - 1].x + n0.y * segN[i - 1].y > 0.6f) n0 = new V2(n0.x + segN[i - 1].x, n0.y + segN[i - 1].y);
                if (i < P - 2 && n1.x * segN[i + 1].x + n1.y * segN[i + 1].y > 0.6f) n1 = new V2(n1.x + segN[i + 1].x, n1.y + segN[i + 1].y);
                int b0 = p.v.Count;
                for (int e = 0; e < 2; e++)
                {
                    V2 q = prof[i + e], nq = e == 0 ? n0 : n1;
                    for (int j = 0; j <= seg; j++)
                    {
                        float th = 2f * (float)Math.PI * j / seg, cs = (float)Math.Cos(th), sn = (float)Math.Sin(th);
                        V3 l = new V3(q.x * cs, q.y, q.x * sn);
                        V3 nl = new V3(nq.x * cs, nq.y, nq.x * sn);
                        Vtx(p, origin + f.Apply(l), f.Apply(nl), col, j / (float)seg, (i + e) / (float)(P - 1));
                    }
                }
                for (int j = 0; j < seg; j++)
                {
                    int a = b0 + j, b = a + 1, cc = a + seg + 1, d = cc + 1;
                    Tri(p, a, b, cc); Tri(p, b, d, cc);
                }
            }
        }

        /// <summary>One flat quad (a, b, c, d around its edge) with a single normal chosen to face the given side.</summary>
        public static void Quad(Part p, V3 a, V3 b, V3 c, V3 d, Col col, V3 faceHint)
        {
            V3 nn = V3.Cross(b - a, d - a).Normalized;
            if (V3.Dot(nn, faceHint) < 0f) nn = -nn;
            int i0 = Vtx(p, a, nn, col, 0f, 0f), i1 = Vtx(p, b, nn, col, 1f, 0f), i2 = Vtx(p, c, nn, col, 1f, 1f), i3 = Vtx(p, d, nn, col, 0f, 1f);
            Tri(p, i0, i1, i2); Tri(p, i0, i2, i3);
        }

        /// <summary>A flat rectangle with its own texture coordinates (wings): corners given (u0,v0), (u1,v0), (u1,v1), (u0,v1).</summary>
        public static void TexQuad(Part p, V3 a, V3 b, V3 c, V3 d, V2 ua, V2 ub, V2 uc, V2 ud, V3 faceHint)
        {
            V3 nn = V3.Cross(b - a, d - a).Normalized;
            if (V3.Dot(nn, faceHint) < 0f) nn = -nn;
            int i0 = Vtx(p, a, nn, Col.White, ua.x, ua.y), i1 = Vtx(p, b, nn, Col.White, ub.x, ub.y);
            int i2 = Vtx(p, c, nn, Col.White, uc.x, uc.y), i3 = Vtx(p, d, nn, Col.White, ud.x, ud.y);
            Tri(p, i0, i1, i2); Tri(p, i0, i2, i3);
        }

        /// <summary>A leaf or blade: a strip along a path, widest in the middle, facing up.</summary>
        public static void Strip(Part p, V3[] pts, float[] halfWidth, V3 widthDir, Col col)
        {
            int K = pts.Length;
            int b0 = p.v.Count;
            for (int k = 0; k < K; k++)
            {
                V3 t = (k == 0 ? pts[1] - pts[0] : k == K - 1 ? pts[K - 1] - pts[K - 2] : pts[k + 1] - pts[k - 1]).Normalized;
                V3 nn = V3.Cross(t, widthDir).Normalized;
                if (nn.y < 0f) nn = -nn;
                Vtx(p, pts[k] - widthDir * halfWidth[k], nn, col, 0f, k / (float)(K - 1));
                Vtx(p, pts[k] + widthDir * halfWidth[k], nn, col, 1f, k / (float)(K - 1));
            }
            for (int k = 0; k < K - 1; k++)
            {
                int a = b0 + k * 2, b = a + 1, cc = a + 2, d = a + 3;
                Tri(p, a, b, cc); Tri(p, b, d, cc);
            }
        }

        /// <summary>A leaf of the usual outline lying along dir, drooping by <paramref name="droop"/> over its length.</summary>
        public static void Leaf(Part p, V3 root, V3 dir, float length, float width, float droop, Col col, int steps = 5, float shape = 0.8f)
        {
            dir = dir.Normalized;
            V3 side = V3.Cross(V3.Up, dir);
            if (side.Length < 1e-4f) side = V3.Right;
            side = side.Normalized;
            var pts = new V3[steps + 1];
            var w = new float[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                pts[i] = root + dir * (length * t) + V3.Up * (-droop * t * t);
                w[i] = Math.Max(0.0004f, width * 0.5f * (float)Math.Sin(Math.PI * Math.Pow(Math.Max(t, 0.02f), shape)));
            }
            w[0] = 0.0004f;
            w[steps] = 0.0004f;
            Strip(p, pts, w, side, col);
        }

        public static V3 Pt(float x, float y, float z) => new V3(x, y, z);
    }

    /// <summary>2D helpers for painted textures: polygons, distances and a tiny deterministic noise.</summary>
    public static class Sh
    {
        public static bool Inside(float x, float y, V2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if (((poly[i].y > y) != (poly[j].y > y)) && (x < (poly[j].x - poly[i].x) * (y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
                    inside = !inside;
            }
            return inside;
        }

        public static float DistSeg(float px, float py, V2 a, V2 b)
        {
            float dx = b.x - a.x, dy = b.y - a.y;
            float l2 = dx * dx + dy * dy;
            float t = l2 < 1e-12f ? 0f : Math.Max(0f, Math.Min(1f, ((px - a.x) * dx + (py - a.y) * dy) / l2));
            float qx = a.x + dx * t - px, qy = a.y + dy * t - py;
            return (float)Math.Sqrt(qx * qx + qy * qy);
        }

        public static float DistPoly(float px, float py, V2[] poly)
        {
            float best = 1e9f;
            for (int i = 0; i < poly.Length; i++) best = Math.Min(best, DistSeg(px, py, poly[i], poly[(i + 1) % poly.Length]));
            return best;
        }

        public static V2[] Chaikin(V2[] poly, int iterations)
        {
            for (int it = 0; it < iterations; it++)
            {
                var next = new V2[poly.Length * 2];
                for (int i = 0; i < poly.Length; i++)
                {
                    V2 a = poly[i], b = poly[(i + 1) % poly.Length];
                    next[i * 2] = new V2(a.x * 0.75f + b.x * 0.25f, a.y * 0.75f + b.y * 0.25f);
                    next[i * 2 + 1] = new V2(a.x * 0.25f + b.x * 0.75f, a.y * 0.25f + b.y * 0.75f);
                }
                poly = next;
            }
            return poly;
        }

        /// <summary>Hash noise in 0..1 at an integer lattice point.</summary>
        public static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xffffff) / (float)0x1000000;
            }
        }

        /// <summary>Smooth value noise, 0..1.</summary>
        public static float Noise(float x, float y, int seed)
        {
            int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed), c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
            return (a + (b - a) * fx) + ((c + (d - c) * fx) - (a + (b - a) * fx)) * fy;
        }

        public static float Smooth(float e0, float e1, float x)
        {
            float t = Math.Max(0f, Math.Min(1f, (x - e0) / (e1 - e0)));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Paints a w x h texture: the function gets (u, v) in 0..1, v = 0 at the bottom row.</summary>
        public static Col[] Paint(int w, int h, Func<float, float, Col> f)
        {
            var px = new Col[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = f((x + 0.5f) / w, (y + 0.5f) / h);
            return px;
        }
    }
}
