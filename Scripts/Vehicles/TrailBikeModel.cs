using UnityEngine;
using WildLens.Core;

namespace WildLens.Vehicles
{
    /// <summary>
    /// Placeholder trail bike built from primitives: a small step-through, high-fender trail bike in reserve-service
    /// olive with a cream body panel, rear rack and cargo box. Friendly and utilitarian rather than sporty.
    /// Bike space: +Z forward, ground at y = 0, wheelbase 1.27 m.
    /// </summary>
    public static class TrailBikeModel
    {
        public const float WheelRadius = 0.33f;
        public const float FrontAxleZ = 0.64f;
        public const float RearAxleZ = -0.63f;

        public struct Parts
        {
            public Transform lean;       // rolls in corners and onto the kickstand
            public Transform steer;      // fork + bars, yaws with steering
            public Transform frontWheel; // spins about local X
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
            var cream = M("BikeCream", new Color(0.86f, 0.82f, 0.7f), 0.35f);
            var black = M("BikeBlack", new Color(0.07f, 0.07f, 0.07f), 0.2f);
            var tyre = M("BikeTyre", new Color(0.09f, 0.09f, 0.085f), 0.05f);
            var metal = M("BikeMetal", new Color(0.62f, 0.63f, 0.64f), 0.75f);
            var engine = M("BikeEngine", new Color(0.26f, 0.26f, 0.27f), 0.45f);
            var lamp = M("BikeLamp", new Color(1f, 0.96f, 0.82f), 0.9f);
            var crate = M("BikeCrate", new Color(0.5f, 0.38f, 0.24f), 0.15f);
            var tail = M("BikeTailLight", new Color(0.7f, 0.08f, 0.05f), 0.6f);

            var p = new Parts();
            p.lean = PrimitiveMeshes.CreatePivot("Lean", root, Vector3.zero);
            var b = p.lean;

            // Wheels
            p.rearWheel = Wheel("Rear Wheel", b, new Vector3(0f, WheelRadius, RearAxleZ), tyre, metal);

            // Steering head: fork raked back 15°, bars, headlight, front fender.
            p.steer = PrimitiveMeshes.CreatePivot("Steer", b, new Vector3(0f, 0.98f, 0.47f), new Vector3(-15f, 0f, 0f));
            var fork = PrimitiveMeshes.CreatePivot("Fork", p.steer, Vector3.zero);
            Part("Fork L", PrimitiveType.Cylinder, fork, new Vector3(-0.075f, -0.34f, 0f), new Vector3(0.04f, 0.34f, 0.04f), metal);
            Part("Fork R", PrimitiveType.Cylinder, fork, new Vector3(0.075f, -0.34f, 0f), new Vector3(0.04f, 0.34f, 0.04f), metal);
            Part("Bars", PrimitiveType.Cylinder, p.steer, new Vector3(0f, 0.14f, -0.06f), new Vector3(0.028f, 0.36f, 0.028f), black, new Vector3(0f, 0f, 90f));
            Part("Grip L", PrimitiveType.Cylinder, p.steer, new Vector3(-0.33f, 0.14f, -0.06f), new Vector3(0.04f, 0.06f, 0.04f), black, new Vector3(0f, 0f, 90f));
            Part("Grip R", PrimitiveType.Cylinder, p.steer, new Vector3(0.33f, 0.14f, -0.06f), new Vector3(0.04f, 0.06f, 0.04f), black, new Vector3(0f, 0f, 90f));
            // Small and low, ahead of the bars, so it doesn't block the rider's view of the track.
            Part("Headlight Shell", PrimitiveType.Cylinder, p.steer, new Vector3(0f, -0.1f, 0.13f), new Vector3(0.13f, 0.04f, 0.13f), black, new Vector3(90f, 0f, 0f));
            Part("Headlight", PrimitiveType.Sphere, p.steer, new Vector3(0f, -0.1f, 0.16f), new Vector3(0.11f, 0.11f, 0.03f), lamp);
            Part("Front Fender", PrimitiveType.Cube, p.steer, new Vector3(0f, -0.2f, 0.06f), new Vector3(0.12f, 0.025f, 0.38f), paint, new Vector3(-8f, 0f, 0f));
            // The front wheel hangs from the fork; its axle sits at the bike's front axle position when steering is straight.
            Vector3 axleLocal = p.steer.InverseTransformPoint(root.TransformPoint(new Vector3(0f, WheelRadius, FrontAxleZ)));
            p.frontWheel = Wheel("Front Wheel", fork, axleLocal, tyre, metal);

            // Frame and body: step-through underbone, cream leg shield, olive side covers.
            Part("Down Tube", PrimitiveType.Cylinder, b, new Vector3(0f, 0.66f, 0.3f), new Vector3(0.07f, 0.3f, 0.07f), paint, new Vector3(-50f, 0f, 0f));
            Part("Leg Shield", PrimitiveType.Cube, b, new Vector3(0f, 0.66f, 0.36f), new Vector3(0.34f, 0.5f, 0.05f), cream, new Vector3(-18f, 0f, 0f));
            Part("Floor", PrimitiveType.Cube, b, new Vector3(0f, 0.42f, 0.1f), new Vector3(0.2f, 0.06f, 0.42f), black);
            Part("Engine", PrimitiveType.Cube, b, new Vector3(0f, 0.36f, -0.08f), new Vector3(0.24f, 0.26f, 0.32f), engine);
            Part("Cylinder", PrimitiveType.Cube, b, new Vector3(0f, 0.38f, 0.14f), new Vector3(0.16f, 0.16f, 0.16f), engine, new Vector3(-70f, 0f, 0f));
            Part("Body", PrimitiveType.Cube, b, new Vector3(0f, 0.72f, -0.36f), new Vector3(0.3f, 0.26f, 0.62f), paint);
            Part("Seat", PrimitiveType.Cube, b, new Vector3(0f, 0.88f, -0.34f), new Vector3(0.26f, 0.09f, 0.62f), black);
            Part("Rear Fender", PrimitiveType.Cube, b, new Vector3(0f, 0.7f, -0.78f), new Vector3(0.14f, 0.025f, 0.42f), paint, new Vector3(12f, 0f, 0f));
            Part("Swingarm", PrimitiveType.Cube, b, new Vector3(0.11f, 0.36f, -0.36f), new Vector3(0.04f, 0.05f, 0.56f), black, new Vector3(-6f, 0f, 0f));
            Part("Shock", PrimitiveType.Cylinder, b, new Vector3(0.13f, 0.56f, -0.55f), new Vector3(0.05f, 0.17f, 0.05f), metal, new Vector3(-20f, 0f, 0f));
            // High exhaust, typical of a trail bike: up the right side, along under the rack.
            Part("Exhaust", PrimitiveType.Cylinder, b, new Vector3(0.17f, 0.6f, -0.42f), new Vector3(0.07f, 0.3f, 0.07f), metal, new Vector3(-72f, 0f, 0f));
            Part("Heat Shield", PrimitiveType.Cube, b, new Vector3(0.2f, 0.63f, -0.44f), new Vector3(0.02f, 0.08f, 0.36f), black);
            // Rear rack with a cargo box for camera gear.
            Part("Rack", PrimitiveType.Cube, b, new Vector3(0f, 0.92f, -0.82f), new Vector3(0.34f, 0.025f, 0.4f), black);
            Part("Cargo Box", PrimitiveType.Cube, b, new Vector3(0f, 1.06f, -0.84f), new Vector3(0.32f, 0.26f, 0.34f), crate);
            Part("Box Lid", PrimitiveType.Cube, b, new Vector3(0f, 1.2f, -0.84f), new Vector3(0.34f, 0.03f, 0.36f), paint);
            Part("Tail Light", PrimitiveType.Cube, b, new Vector3(0f, 0.74f, -1.0f), new Vector3(0.08f, 0.04f, 0.03f), tail);

            p.kickstand = PrimitiveMeshes.CreatePivot("Kickstand", b, new Vector3(-0.12f, 0.32f, -0.12f));
            Part("Stand", PrimitiveType.Cylinder, p.kickstand, new Vector3(0f, -0.15f, 0f), new Vector3(0.03f, 0.16f, 0.03f), black);

            p.seat = PrimitiveMeshes.CreatePivot("Seat Anchor", root, new Vector3(0f, 0f, -0.3f));
            return p;
        }

        private static Transform Wheel(string name, Transform parent, Vector3 pos, Material tyre, Material metal)
        {
            var w = PrimitiveMeshes.CreatePivot(name, parent, pos);
            Part("Tyre", PrimitiveType.Cylinder, w, Vector3.zero, new Vector3(WheelRadius * 2f, 0.05f, WheelRadius * 2f), tyre, new Vector3(0f, 0f, 90f));
            Part("Rim", PrimitiveType.Cylinder, w, Vector3.zero, new Vector3(WheelRadius * 1.45f, 0.055f, WheelRadius * 1.45f), metal, new Vector3(0f, 0f, 90f));
            Part("Hub", PrimitiveType.Cylinder, w, Vector3.zero, new Vector3(0.1f, 0.07f, 0.1f), metal, new Vector3(0f, 0f, 90f));
            // Two spokes-ish bars so rotation reads.
            Part("Spoke A", PrimitiveType.Cube, w, Vector3.zero, new Vector3(0.02f, WheelRadius * 1.4f, 0.02f), tyre);
            Part("Spoke B", PrimitiveType.Cube, w, Vector3.zero, new Vector3(0.02f, 0.02f, WheelRadius * 1.4f), tyre);
            return w;
        }

        private static void Part(string name, PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default)
        {
            var go = PrimitiveMeshes.CreatePart(name, type, parent, pos, scale, mat, euler);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }
}
