using UnityEngine;
using WildLens.Data;

namespace WildLens.Environment
{
    /// <summary>
    /// A place where certain wildlife is more likely: a flower meadow for deer, a brush pile for rabbits, a big old
    /// tree for squirrels, a dead snag or boulder for hawks. Species list the hotspot types they favour
    /// (<see cref="SpeciesDefinition.preferredHotspots"/>); the spawner centres most home ranges on them and hawks
    /// favour perches inside Perch hotspots. Nothing is scripted: animals still wander, flee and return, but a
    /// player who learns the landscape will find wildlife where they expect it.
    ///
    /// Placed by the scene builder; add your own anywhere. With none in the scene, WildlifeWorld derives them
    /// from the scenery (open ground, bush clusters, tallest trees and perches).
    /// </summary>
    public class HabitatHotspot : MonoBehaviour
    {
        public HotspotType type = HotspotType.Meadow;
        public float radius = 15f;
        [Tooltip("Relative chance of being chosen among hotspots of the same type.")]
        [Min(0f)] public float weight = 1f;

        private void OnDrawGizmos()
        {
            Gizmos.color = Colour(type);
            Gizmos.DrawWireSphere(transform.position, radius);
        }

        public static Color Colour(HotspotType t)
        {
            switch (t)
            {
                case HotspotType.Meadow: return new Color(0.9f, 0.85f, 0.3f, 0.8f);
                case HotspotType.BrushPile: return new Color(0.6f, 0.4f, 0.2f, 0.8f);
                case HotspotType.MatureTree: return new Color(0.3f, 0.8f, 0.3f, 0.8f);
                case HotspotType.Perch: return new Color(0.4f, 0.7f, 1f, 0.8f);
                default: return Color.white;
            }
        }
    }
}
