using System.Collections.Generic;
using UnityEngine;
using WildLens.Environment;

namespace WildLens.Data
{
    /// <summary>What kind of small subject (append only: serialized as ints).</summary>
    public enum SubjectGroup { Wildflower, Fungus, Butterfly, Dragonfly, Bee, Beetle, Firefly, Mantis, Frog, Salamander }

    /// <summary>
    /// How a small subject behaves. 1.5 implements <see cref="Still"/> (plants, fungi, a resting beetle); the others are
    /// reserved so data can be written ahead of the behaviour (append only).
    /// </summary>
    public enum SubjectBehaviour { Still, Perches, Flits, Hops, Glows }

    /// <summary>
    /// A small subject for macro photography (1.5 groundwork): a wildflower, a fungus, an insect, an amphibian. The
    /// telephoto side of the game has <see cref="SpeciesDefinition"/> (awareness, locomotion, rhythms); small subjects need
    /// far less: identity, size, where and when they show, and how they behave in front of a lens. Kept separate so
    /// neither grows the other's fields. Placed in the world by <see cref="Photography.MacroSubject"/>; photographed in
    /// macro mode (M); recorded in the journal's field notebook.
    ///
    /// Future content plugs in as data plus a model: butterflies (Flits, hide in rain, midday), dragonflies (Perches, by
    /// water), bees (Flits, on flowers), beetles (Still on logs), fireflies (Glows, dusk and night), mantises (Still,
    /// meadow), frogs (Hops, by water, after rain), salamanders (Still, under logs, after rain), mushrooms (Still, after rain).
    /// </summary>
    public class NaturalistSubject : ScriptableObject
    {
        public const int CurrentDataVersion = 1;
        public int dataVersion = CurrentDataVersion;

        [Header("Identity")]
        public string subjectId = "subject";
        public string displayName = "Subject";
        public string scientificName = "";
        public SubjectGroup group = SubjectGroup.Wildflower;
        [TextArea(3, 6)] public string fieldNotes = "";

        [Header("In front of the lens")]
        [Tooltip("Largest dimension in metres (a dandelion head ~0.04, a butterfly ~0.07, a frog ~0.08).")]
        public float size = 0.05f;
        public SubjectBehaviour behaviour = SubjectBehaviour.Still;
        [Tooltip("Frame fill (largest side of the subject / frame) for Good and Excellent.")]
        public Vector2 fillForGrade = new Vector2(0.18f, 0.35f);

        [Header("Where and when")]
        [Tooltip("Plain words for the field notebook: 'meadow edges', 'woodland floor', 'creek stones'.")]
        public string habitat = "";
        [Tooltip("Showing in each period: Night, Dawn, Morning, Midday, Golden hour, Dusk (DayPeriod order).")]
        public bool[] activePeriods = { false, true, true, true, true, true };
        [Tooltip("Out (or most visible) after rain: fungi, salamanders, frogs.")]
        public bool afterRain;
        [Tooltip("Hides while it rains: butterflies, bees.")]
        public bool hidesInRain;

        /// <summary>Whether it is out to be found now.</summary>
        public bool IsShowing(DayPeriod period, bool raining)
        {
            if (hidesInRain && raining) return false;
            int i = (int)period;
            return activePeriods == null || i < 0 || i >= activePeriods.Length || activePeriods[i];
        }
    }

    /// <summary>The first macro subjects (1.5): real CC0 plant scans already in the package.</summary>
    public static class SubjectDefaults
    {
        public static List<NaturalistSubject> CreateAll() => new List<NaturalistSubject> { Dandelion(), Periwinkle() };

        public static NaturalistSubject Dandelion()
        {
            var s = New("common_dandelion", "Common Dandelion", "Taraxacum officinale", SubjectGroup.Wildflower,
                "A golden flower head of hundreds of tiny florets that opens to the sun and closes at dusk and in rain; later a " +
                "perfect sphere of seeds. Bees and early butterflies depend on it. Get close and low: it rewards a macro lens.",
                0.05f, "meadow edges, trail verges");
            s.activePeriods = new[] { false, false, true, true, true, false }; // open in daylight
            s.hidesInRain = true; // closes in rain
            return s;
        }

        public static NaturalistSubject Periwinkle()
        {
            var s = New("lesser_periwinkle", "Lesser Periwinkle", "Vinca minor", SubjectGroup.Wildflower,
                "Glossy evergreen leaves carpeting shady ground, with five-petalled violet-blue flowers in spring. Planted at old " +
                "homesteads and spread into the woods; a sign of people long ago. Find it in the fern glade's shade.",
                0.035f, "shady woodland floor");
            return s;
        }

        private static NaturalistSubject New(string id, string display, string sci, SubjectGroup group, string notes, float size, string habitat)
        {
            var s = ScriptableObject.CreateInstance<NaturalistSubject>();
            s.name = "Subject_" + display.Replace(" ", "");
            s.subjectId = id;
            s.displayName = display;
            s.scientificName = sci;
            s.group = group;
            s.fieldNotes = notes;
            s.size = size;
            s.habitat = habitat;
            return s;
        }
    }
}
