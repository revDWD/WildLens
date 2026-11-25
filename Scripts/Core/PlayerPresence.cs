using UnityEngine;

namespace WildLens.Core
{
    /// <summary>
    /// What wildlife can perceive about the player. Written by the player controller, read by
    /// <c>WildlifeAwareness</c>. Keeps the wildlife code independent of the player implementation.
    ///
    /// Two channels:
    ///   <see cref="Noise"/>   how audible the player is (footfall): speed, sprinting, crouching
    ///   <see cref="Profile"/> how visible the player's silhouette is (standing 1, crouched ~0.55); only counts
    ///                         when the animal has line of sight, and crouching also lowers the eye/head point,
    ///                         so bushes and rocks hide a crouched player
    /// </summary>
    public static class PlayerPresence
    {
        public const float StandingEyeHeight = 1.6f;

        public static bool Exists { get; private set; }
        public static Vector3 Position { get; private set; }
        public static float EyeHeight { get; private set; } = StandingEyeHeight;
        public static Vector3 HeadPosition => Position + Vector3.up * EyeHeight;
        /// <summary>Root of the player's hierarchy (its colliders count as "the player" for line of sight).</summary>
        public static Transform Root { get; private set; }
        public static bool IsSprinting { get; private set; }
        public static bool IsCrouching { get; private set; }
        public static bool IsInCameraMode { get; private set; }
        public static float Speed { get; private set; }
        public static bool IsStill => Speed < 0.15f;

        /// <summary>Audibility: standing still 0.15 (crouched 0.08) · creeping ≈0.2–0.4 · walking 1 · sprinting 2.2.</summary>
        public static float Noise
        {
            get
            {
                if (!Exists) return 0f;
                if (IsSprinting) return 2.2f;
                if (IsStill) return IsCrouching ? 0.08f : 0.15f;
                float n = Mathf.Lerp(0.2f, 1f, Mathf.Clamp01(Speed / 3f));
                return IsCrouching ? n * 0.5f : n; // careful footfall
            }
        }

        /// <summary>Visibility of the player's silhouette (1 standing, 0.55 crouched).</summary>
        public static float Profile => IsCrouching ? 0.55f : 1f;

        public static void Report(Transform root, Vector3 position, float speed, bool sprinting, bool crouching,
            bool cameraMode, float eyeHeight)
        {
            Exists = true;
            Root = root;
            Position = position;
            Speed = speed;
            IsSprinting = sprinting;
            IsCrouching = crouching;
            IsInCameraMode = cameraMode;
            EyeHeight = eyeHeight;
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
