using UnityEngine;

namespace WildLens.Core
{
    /// <summary>
    /// What wildlife can perceive about the player. Written by the player controller, read by animal senses.
    /// Keeps the wildlife code independent of the player implementation.
    /// </summary>
    public static class PlayerPresence
    {
        public static bool Exists { get; private set; }
        public static Vector3 Position { get; private set; }
        public static bool IsSprinting { get; private set; }
        public static bool IsCameraRaised { get; private set; }
        public static float Speed { get; private set; }

        /// <summary>Multiplier on animal detection distances: sprinting is loud, a raised camera means moving slowly.</summary>
        public static float DisturbanceScale
        {
            get
            {
                if (!Exists) return 0f;
                if (IsSprinting) return 1.6f;
                if (Speed < 0.2f) return 0.75f;   // standing still
                if (IsCameraRaised) return 0.85f;
                return 1f;
            }
        }

        public static void Report(Vector3 position, float speed, bool sprinting, bool cameraRaised)
        {
            Exists = true;
            Position = position;
            Speed = speed;
            IsSprinting = sprinting;
            IsCameraRaised = cameraRaised;
        }

        public static void Clear()
        {
            Exists = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Exists = false;
    }
}
