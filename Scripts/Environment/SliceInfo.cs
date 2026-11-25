using UnityEngine;
using WildLens.Core;

namespace WildLens.Environment
{
    /// <summary>
    /// Written onto the slice root by the scene builder: which package and environment version built this scene.
    /// A missing or older marker means the scene predates the current art and should be rebuilt.
    /// </summary>
    [DisallowMultipleComponent]
    public class SliceInfo : MonoBehaviour
    {
        public string packageVersion;
        public int environmentVersion;
        public bool scannedArt;

        public bool IsCurrent => environmentVersion >= WildLensVersion.Environment;

        /// <summary>Null when the scene has a current slice (or no slice at all, e.g. a custom scene).</summary>
        public static string StaleReason()
        {
            var info = SceneQuery.FindOne<SliceInfo>();
            if (info != null)
                return info.IsCurrent ? null : $"built with WildLens {info.packageVersion}";
            // Slices built before 0.6 have no marker; recognise them by the root name.
            return GameObject.Find("WildLens Vertical Slice") != null ? "built with WildLens 0.5 or older" : null;
        }
    }
}
