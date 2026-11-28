using UnityEngine;
using WildLens.Core;

namespace WildLens.Vehicles
{
    /// <summary>
    /// Placeholder trail bike built from primitives (1.5: reshaped after small 125 cc trail bikes of the Trail 125 kind):
    /// exposed underbone frame, horizontal single with a skid plate, a raised air-intake snorkel and an upswept exhaust
    /// with heat shield, long flat seat, big tubular rear carrier with a gear box, twin rear shocks, high front fender,
    /// round headlight, wide raised bars and mirrors. Reserve-service olive panels on a black frame. Friendly and
    /// practical, never sporty.
    ///
    /// Suspension is visible: the front wheel and fork sliders hang from <see cref="Parts.frontAxle"/>, which slides along
    /// the fork; the rear wheel rides on <see cref="Parts.swingarm"/>, which pivots. <see cref="TrailBike"/> moves both
    /// so the wheels meet the real ground under a body that is smoothed for the rider.
    /// Bike space: +Z forward, ground at y = 0, wheelbase 1.27 m.
    /// </summary>
    public static class TrailBikeModel
    {
        public const float WheelRadius = 0.33f;
        public const float FrontAxleZ = 0.64f;
        public const float RearAxleZ = -0.63f;
        public const float SwingarmPivotZ = -0.1f;
        public const float SwingarmPivotY = 0.4f;

        public struct Parts
        {
            public Transform lean;       // rolls in corners and onto the kickstand
            public Transform steer;      // fork + bars, yaws with steering
            public Transform frontAxle;  // slides along the fork (suspension travel); holds the front wheel and sliders
            public Transform frontWheel; // spins about local X
            public Transform swingarm;   // pivots about local X (rear suspension); holds the rear wheel
            public Transform rearWheel;
            public Transform kickstand;
            public Transform seat;       // rider anchor (on the un-leaned body)
        }

        /// <param name="material">Material factory (name, colour, smoothness). The editor passes one that saves
        /// assets; at runtime materials are created in memory.</param>
        public static Parts Build(Transform root, System.Func<string, Color, float, Material> material = null)
        {
            Material M(string name, Color c, float s) => material != null ? material(name, c, s) : MaterialLibrary.Create(c, s);
            var paint = M("BikePaint", new Color(0.33f, 0.4f, 0.24f), 0.45f);   // reserve olive
            var frame = M("BikeFrame", new Color(0.06f, 0.06f, 0.06f), 0.35f);
            var black = M("BikeBlack", new Color(0.07f, 0.07f, 0.07f), 0.2f);
            var tyre = M("BikeTyre", new Color(0.09f, 0.09f, 0.085f), 0.05f);
            var metal = M("BikeMetal", new Color(0.62f, 0.63f, 0.64f), 0.75f);
            var chrome = M("BikeChrome", new Color(0.8f, 0.81f, 0.82f), 0.9f);
            var engine = M("BikeEngine", new Color(0.36f, 0.36f, 0.37f), 0.45f);
            var lamp = M("BikeLamp", new Color(1f, 0.96f, 0.82f), 0.9f);
            var crate = M("BikeCrate", new Color(0.5f, 0.38f, 0.24f), 0.15f);
            var tail = M("BikeTailLight", new Color(0.7f, 0.08f, 0.05f), 0.6f);

            var p = new Parts();
            p.lean = PrimitiveMeshes.CreatePivot("Lean", root, Vector3.zero);
            var b = p.lean;

            // ---- Rear: swingarm pivot behind the engine, wheel at its end, twin shocks up to the carrier.
            p.swingarm = PrimitiveMeshes.CreatePivot("Swingarm", b, new Vector3(0f, SwingarmPivotY, SwingarmPivotZ));
            Vector3 rearAxle = new Vector3(0f, WheelRadius - SwingarmPivotY, RearAxleZ - SwingarmPivotZ);
            foreach (float x in new[] { -0.11f, 0.11f })
            {
                var arm = PrimitiveMeshes.CreatePart("Arm", PrimitiveType.Cube, p.swingarm, new Vector3(x, rearAxle.y * 0.5f, rearAxle.z * 0.5f),
                    new Vector3(0.035f, 0.05f, rearAxle.magnitude), frame);
                arm.transform.localRotation = Quaternion.LookRotation(rearAxle.normalized, Vector3.up);
            }
            var rearHub = PrimitiveMeshes.CreatePivot("Rear Axle", p.swingarm, rearAxle);
            p.rearWheel = Wheel("Rear Wheel", rearHub, Vector3.zero, tyre, metal);
            foreach (float x in new[] { -0.14f, 0.14f })
            {
                Part("Shock", PrimitiveType.Cylinder, b, new Vector3(x, 0.6f, -0.58f), new Vector3(0.045f, 0.16f, 0.045f), black, new Vector3(-16f, 0f, 0f));
                Part("Spring", PrimitiveType.Cylinder, b, new Vector3(x, 0.57f, -0.59f), new Vector3(0.06f, 0.1f, 0.06f), chrome, new Vector3(-16f, 0f, 0f));
            }

            // ---- Front: raked steering head, fork tubes, high fender, round headlight, wide raised bars, mirrors.
            p.steer = PrimitiveMeshes.CreatePivot("Steer", b, new Vector3(0f, 0.98f, 0.47f), new Vector3(-24f, 0f, 0f));
            var fork = PrimitiveMeshes.CreatePivot("Fork", p.steer, Vector3.zero);
            Vector3 axleLocal = fork.InverseTransformPoint(root.TransformPoint(new Vector3(0f, WheelRadius, FrontAxleZ)));
            float forkLen = -axleLocal.y;
            foreach (float x in new[] { -0.08f, 0.08f })
                Part("Fork Tube", PrimitiveType.Cylinder, fork, new Vector3(x, -forkLen * 0.3f, axleLocal.z), new Vector3(0.036f, forkLen * 0.32f, 0.036f), chrome);
            p.frontAxle = PrimitiveMeshes.CreatePivot("Front Axle", fork, axleLocal);
            foreach (float x in new[] { -0.08f, 0.08f })
                Part("Fork Slider", PrimitiveType.Cylinder, p.frontAxle, new Vector3(x, forkLen * 0.22f, 0f), new Vector3(0.05f, forkLen * 0.22f, 0.05f), black);
            p.frontWheel = Wheel("Front Wheel", p.frontAxle, Vector3.zero, tyre, metal);
            Part("Front Fender", PrimitiveType.Cube, fork, new Vector3(0f, -forkLen * 0.52f, axleLocal.z + 0.02f), new Vector3(0.13f, 0.025f, 0.36f), paint, new Vector3(-6f, 0f, 0f));
            Part("Headlight Shell", PrimitiveType.Cylinder, p.steer, new Vector3(0f, 0.02f, 0.13f), new Vector3(0.17f, 0.05f, 0.17f), chrome, new Vector3(90f, 0f, 0f));
            Part("Headlight", PrimitiveType.Sphere, p.steer, new Vector3(0f, 0.02f, 0.17f), new Vector3(0.145f, 0.145f, 0.03f), lamp);
            Part("Bar Riser", PrimitiveType.Cylinder, p.steer, new Vector3(0f, 0.12f, -0.03f), new Vector3(0.05f, 0.08f, 0.05f), black);
            Part("Bars", PrimitiveType.Cylinder, p.steer, new Vector3(0f, 0.21f, -0.08f), new Vector3(0.026f, 0.39f, 0.026f), black, new Vector3(0f, 0f, 90f));
            foreach (float x in new[] { -0.37f, 0.37f })
            {
                Part("Grip", PrimitiveType.Cylinder, p.steer, new Vector3(x, 0.21f, -0.08f), new Vector3(0.04f, 0.06f, 0.04f), black, new Vector3(0f, 0f, 90f));
                Part("Mirror Stalk", PrimitiveType.Cylinder, p.steer, new Vector3(x * 0.75f, 0.33f, -0.07f), new Vector3(0.012f, 0.12f, 0.012f), black);
                Part("Mirror", PrimitiveType.Cylinder, p.steer, new Vector3(x * 0.78f, 0.46f, -0.07f), new Vector3(0.09f, 0.012f, 0.09f), chrome, new Vector3(90f, 0f, 0f));
            }

            // ---- Frame and body: exposed underbone spine with an olive cover, horizontal single, skid plate.
            Part("Spine", PrimitiveType.Cylinder, b, new Vector3(0f, 0.72f, 0.22f), new Vector3(0.075f, 0.33f, 0.075f), frame, new Vector3(-58f, 0f, 0f));
            Part("Spine Cover", PrimitiveType.Cube, b, new Vector3(0f, 0.78f, 0.24f), new Vector3(0.17f, 0.12f, 0.46f), paint, new Vector3(30f, 0f, 0f));
            Part("Crankcase", PrimitiveType.Cube, b, new Vector3(0f, 0.36f, -0.02f), new Vector3(0.28f, 0.26f, 0.32f), engine);
            Part("Cylinder", PrimitiveType.Cube, b, new Vector3(0f, 0.4f, 0.21f), new Vector3(0.15f, 0.14f, 0.2f), engine, new Vector3(-12f, 0f, 0f));
            Part("Cylinder Fins", PrimitiveType.Cube, b, new Vector3(0f, 0.4f, 0.24f), new Vector3(0.19f, 0.17f, 0.04f), metal, new Vector3(-12f, 0f, 0f));
            Part("Skid Plate", PrimitiveType.Cube, b, new Vector3(0f, 0.2f, 0.1f), new Vector3(0.24f, 0.02f, 0.5f), metal, new Vector3(-6f, 0f, 0f));
            Part("Footpegs", PrimitiveType.Cylinder, b, new Vector3(0f, 0.3f, -0.08f), new Vector3(0.03f, 0.25f, 0.03f), black, new Vector3(0f, 0f, 90f));
            // The trail bike's signatures on the right: a raised air intake and an upswept exhaust under a heat shield.
            Part("Air Intake", PrimitiveType.Cylinder, b, new Vector3(0.15f, 0.62f, -0.16f), new Vector3(0.05f, 0.24f, 0.05f), black, new Vector3(-32f, 0f, 0f));
            Part("Exhaust Down", PrimitiveType.Cylinder, b, new Vector3(0.12f, 0.33f, 0.24f), new Vector3(0.045f, 0.1f, 0.045f), metal, new Vector3(60f, 0f, 0f));
            Part("Exhaust", PrimitiveType.Cylinder, b, new Vector3(0.19f, 0.5f, -0.24f), new Vector3(0.055f, 0.38f, 0.055f), metal, new Vector3(-64f, 0f, 0f));
            Part("Heat Shield", PrimitiveType.Cube, b, new Vector3(0.22f, 0.54f, -0.3f), new Vector3(0.02f, 0.09f, 0.46f), black, new Vector3(-22f, 0f, 0f));
            Part("Muffler", PrimitiveType.Cylinder, b, new Vector3(0.19f, 0.68f, -0.62f), new Vector3(0.08f, 0.12f, 0.08f), metal, new Vector3(90f, 0f, 0f));
            Part("Side Cover", PrimitiveType.Cube, b, new Vector3(0f, 0.66f, -0.34f), new Vector3(0.28f, 0.18f, 0.34f), paint);
            Part("Seat", PrimitiveType.Cube, b, new Vector3(0f, 0.8f, -0.36f), new Vector3(0.27f, 0.1f, 0.7f), black);
            Part("Rear Fender", PrimitiveType.Cube, b, new Vector3(0f, 0.74f, -0.84f), new Vector3(0.15f, 0.025f, 0.36f), paint, new Vector3(10f, 0f, 0f));

            // Big tubular rear carrier with a gear box: the bike's working end.
            foreach (float x in new[] { -0.2f, 0.2f })
                Part("Rack Rail", PrimitiveType.Cube, b, new Vector3(x, 0.86f, -0.83f), new Vector3(0.025f, 0.025f, 0.56f), black);
            foreach (float z in new[] { -0.6f, -0.83f, -1.06f })
                Part("Rack Bar", PrimitiveType.Cube, b, new Vector3(0f, 0.86f, z), new Vector3(0.42f, 0.022f, 0.022f), black);
            Part("Rack Strut", PrimitiveType.Cube, b, new Vector3(0f, 0.8f, -1.02f), new Vector3(0.36f, 0.12f, 0.02f), black);
            Part("Cargo Box", PrimitiveType.Cube, b, new Vector3(0f, 0.98f, -0.86f), new Vector3(0.34f, 0.22f, 0.32f), crate);
            Part("Box Lid", PrimitiveType.Cube, b, new Vector3(0f, 1.1f, -0.86f), new Vector3(0.36f, 0.03f, 0.34f), paint);
            Part("Tail Light", PrimitiveType.Cube, b, new Vector3(0f, 0.76f, -1.03f), new Vector3(0.09f, 0.045f, 0.03f), tail);

            p.kickstand = PrimitiveMeshes.CreatePivot("Kickstand", b, new Vector3(-0.12f, 0.32f, -0.12f));
            Part("Stand", PrimitiveType.Cylinder, p.kickstand, new Vector3(0f, -0.15f, 0f), new Vector3(0.03f, 0.16f, 0.03f), black);

            p.seat = PrimitiveMeshes.CreatePivot("Seat Anchor", root, new Vector3(0f, 0f, -0.3f));
            return p;
        }

        private static Transform Wheel(string name, Transform parent, Vector3 pos, Material tyre, Material metal)
        {
            var w = PrimitiveMeshes.CreatePivot(name, parent, pos);
            // Chunkier dual-sport tyre than before (a trail bike's knobblies read in silhouette).
            Part("Tyre", PrimitiveType.Cylinder, w, Vector3.zero, new Vector3(WheelRadius * 2f, 0.065f, WheelRadius * 2f), tyre, new Vector3(0f, 0f, 90f));
            Part("Rim", PrimitiveType.Cylinder, w, Vector3.zero, new Vector3(WheelRadius * 1.5f, 0.07f, WheelRadius * 1.5f), metal, new Vector3(0f, 0f, 90f));
            Part("Hub", PrimitiveType.Cylinder, w, Vector3.zero, new Vector3(0.12f, 0.085f, 0.12f), metal, new Vector3(0f, 0f, 90f));
            Part("Spoke A", PrimitiveType.Cube, w, Vector3.zero, new Vector3(0.015f, WheelRadius * 1.45f, 0.015f), metal);
            Part("Spoke B", PrimitiveType.Cube, w, Vector3.zero, new Vector3(0.015f, 0.015f, WheelRadius * 1.45f), metal);
            Part("Spoke C", PrimitiveType.Cube, w, Vector3.zero, new Vector3(0.015f, WheelRadius * 1.45f, 0.015f), metal, new Vector3(45f, 0f, 0f));
            Part("Spoke D", PrimitiveType.Cube, w, Vector3.zero, new Vector3(0.015f, WheelRadius * 1.45f, 0.015f), metal, new Vector3(-45f, 0f, 0f));
            return w;
        }

        private static void Part(string name, PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default)
        {
            var go = PrimitiveMeshes.CreatePart(name, type, parent, pos, scale, mat, euler);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }
}
