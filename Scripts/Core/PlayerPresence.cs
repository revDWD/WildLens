using UnityEngine;

namespace WildLens.Core
{
    /// <summary>
    /// What wildlife can perceive about the player. Written by the player controller, read by
    /// <c>WildlifeAwareness</c>. Keeps the wildlife code independent of the player implementation.
    /// </summary>
    public static class PlayerPresence
    {
        public const float EyeHeight = 1.6f;

        public static bool Exists { get; private set; }
        public static Vector3 Position { get; private set; }
        public static Vector3 HeadPosition => Position + Vector3.up * EyeHeight;
        /// <summary>Root of the player's hierarchy (its colliders count as "the player" for line of sight).</summary>
        public static Transform Root { get; private set; }
        public static bool IsSprinting { get; private set; }
        public static bool IsInCameraMode { get; private set; }
        public static float Speed { get; private set; }

        /// <summary>
        /// How conspicuous the player is right now (≈0.15 standing still … 1 walking … 2.2 sprinting).
        /// Scales how fast animals become suspicious. Creeping in camera mode is quiet; standing still is quietest.
        /// </summary>
        public static float Noise
        {
            get
            {
                if (!Exists) return 0f;
                if (IsSprinting) return 2.2f;
                if (Speed < 0.15f) return 0.15f;
                if (IsInCameraMode) return Mathf.Lerp(0.3f, 0.5f, Mathf.Clamp01(Speed / 1.5f));
                return Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(Speed / 3f));
            }
        }

        public static void Report(Transform root, Vector3 position, float speed, bool sprinting, bool cameraMode)
        {
            Exists = true;
            Root = root;
            Position = position;
            Speed = speed;
            IsSprinting = sprinting;
            IsInCameraMode = cameraMode;
        }

        public static void Clear()
        {
            Exists = false;
            Root = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Clear();
    }
}
