using System.Collections.Generic;
using UnityEngine;
using WildLens.Environment;

namespace WildLens.Data
{
    /// <summary>What kind of small subject (append only: serialized as ints).</summary>
    public enum SubjectGroup { Wildflower, Fungus, Butterfly, Dragonfly, Bee, Beetle, Firefly, Mantis, Frog, Salamander, Moth, Snail }

    /// <summary>Where the builder places a small subject that has a model (append only: serialized as ints).</summary>
    public enum SubjectHabitat { MeadowVerge, Flowers, WaterEdge, WoodlandFloor, DeadWood }

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

        [Header("In the reserve (1.7)")]
        [Tooltip("A model from your own project (a butterfly, a frog, a mushroom). Empty: not placed. Filled in by WildLens ▸ Macro Subjects From Packs.")]
        public GameObject prefab;
        [Tooltip("More models for the same subject (e.g. several mushrooms). The builder picks among prefab and variants.")]
        public GameObject[] variants;
        [Tooltip("Where the builder places it.")]
        public SubjectHabitat where = SubjectHabitat.MeadowVerge;
        [Tooltip("How many the builder places.")]
        [Min(0)] public int count = 6;
        [Tooltip("Metres above the ground (or flower) for things that perch or hover; 0 sits on the surface.")]
        [Min(0f)] public float hoverHeight;

        /// <summary>Whether it is out to be found now (ignores <see cref="afterRain"/>: kept for older callers).</summary>
        public bool IsShowing(DayPeriod period, bool raining) => IsShowing(period, raining, true);

        /// <summary>Whether it is out to be found now. <paramref name="recentlyRained"/> is true while it rains and for a few hours after.</summary>
        public bool IsShowing(DayPeriod period, bool raining, bool recentlyRained)
        {
            if (hidesInRain && raining) return false;
            if (afterRain && !recentlyRained) return false;
            int i = (int)period;
            return activePeriods == null || i < 0 || i >= activePeriods.Length || activePeriods[i];
        }
    }

    /// <summary>The first macro subjects (1.5): real CC0 plant scans already in the package.</summary>
    public static class SubjectDefaults
    {
        public static List<NaturalistSubject> CreateAll() => new List<NaturalistSubject>
        {
            Dandelion(), Periwinkle(),
            Monarch(), LunaMoth(), Whitetail(), BumbleBee(), LadyBeetle(), Mantis(), LeopardFrog(), Newt(), Snail(), Firefly(), Mushroom(), OxeyeDaisy(),
        };

        // DayPeriod order: Night, Dawn, Morning, Midday, GoldenHour, Dusk.
        private static readonly bool[] Daytime = { false, false, true, true, true, false };
        private static readonly bool[] Always = { true, true, true, true, true, true };

        // Frame fill is the largest side of the projected bounds over the frame (MacroAnalyzer). At the macro lens's 1.7x step
        // the frame is 2 * d * tan(12 deg) / 1.7 = 0.25 d tall, so fill = size / (0.25 d) = 4 * size / d. A 0.02 m bee reaches
        // 0.22 (Excellent) at d = 0.36 m and 0.10 (Good) at 0.8 m; a 0.012 m beetle or firefly reaches 0.22 at 0.22 m and 0.10
        // at 0.48 m, both well beyond the 8 cm minimum focus. (The default 0.35 would need 0.14 m for the beetle.)
        private static readonly Vector2 TinyFill = new Vector2(0.10f, 0.22f);

        public static NaturalistSubject Monarch()
        {
            var s = New("monarch_butterfly", "Monarch Butterfly", "Danaus plexippus", SubjectGroup.Butterfly,
                "Orange wings veined in black, a warning to birds: as caterpillars they fed on milkweed and taste foul. It sips nectar on sunny " +
                "flowers and flutters off and back; wait for the wings to open. It shelters when rain comes.",
                0.09f, "sunny flowers", SubjectBehaviour.Flits, SubjectHabitat.Flowers, Daytime, 0f, 6);
            s.hidesInRain = true;
            return s;
        }

        public static NaturalistSubject LunaMoth()
        {
            var s = New("luna_moth", "Luna Moth", "Actias luna", SubjectGroup.Moth,
                "A pale green moth with long tails and an eyespot on each wing. The adult has no mouth and lives about a week, flying by night. " +
                "At dawn and dusk look for one clinging to a trunk or old log, wings folded.",
                0.10f, "trunks and logs in the woods", SubjectBehaviour.Still, SubjectHabitat.DeadWood, new[] { true, true, false, false, false, true }, 0f, 4);
            return s;
        }

        public static NaturalistSubject Whitetail()
        {
            var s = New("common_whitetail", "Common Whitetail", "Plathemis lydia", SubjectGroup.Dragonfly,
                "A stout dragonfly; the male has a chalky white abdomen and a broad dark band across each wing. It perches on a bare stem " +
                "by the water, darts out after a passing insect, and comes back to the very same perch.",
                0.06f, "water's edge, bare stems", SubjectBehaviour.Perches, SubjectHabitat.WaterEdge, Daytime, 0.4f, 5);
            s.hidesInRain = true;
            return s;
        }

        public static NaturalistSubject BumbleBee()
        {
            var s = New("bumble_bee", "Common Eastern Bumble Bee", "Bombus impatiens", SubjectGroup.Bee,
                "Round, furry and unhurried, working flower to flower and carrying pollen on its hind legs. A feeding bee pays no attention " +
                "to a patient photographer; stay low and still, and move in close.",
                0.02f, "sunny flowers", SubjectBehaviour.Flits, SubjectHabitat.Flowers, Daytime, 0f, 6);
            s.hidesInRain = true;
            s.fillForGrade = TinyFill;
            return s;
        }

        public static NaturalistSubject LadyBeetle()
        {
            var s = New("ladybird_beetle", "Spotted Lady Beetle", "Coleomegilla maculata", SubjectGroup.Beetle,
                "A pink-red beetle with black spots, hunting aphids and eating pollen along meadow stems. Tiny, so come close, " +
                "slowly, and let the lens do the rest.",
                0.012f, "meadow stems and verges", SubjectBehaviour.Still, SubjectHabitat.MeadowVerge, Daytime, 0f, 6);
            s.fillForGrade = TinyFill;
            return s;
        }

        public static NaturalistSubject Mantis()
        {
            var s = New("praying_mantis", "Carolina Mantis", "Stagmomantis carolina", SubjectGroup.Mantis,
                "Small, brown or grey, and almost impossible to see among dry grass stems. It waits with its forelegs folded until something comes " +
                "within reach, and it turns its head to watch you, the only insect that will.",
                0.06f, "meadow grass", SubjectBehaviour.Still, SubjectHabitat.MeadowVerge, new[] { false, false, true, true, true, true }, 0f, 4);
            return s;
        }

        public static NaturalistSubject LeopardFrog()
        {
            var s = New("northern_leopard_frog", "Northern Leopard Frog", "Lithobates pipiens", SubjectGroup.Frog,
                "Green or brown with pale-edged dark spots and a pale ridge down each side. It sits half-hidden at the water's edge and leaps " +
                "with a splash if startled; approach slowly and it will stay.",
                0.08f, "pond and creek edges", SubjectBehaviour.Hops, SubjectHabitat.WaterEdge, Always, 0f, 5);
            return s;
        }

        public static NaturalistSubject Newt()
        {
            var s = New("red_spotted_newt", "Eastern Newt (red eft)", "Notophthalmus viridescens", SubjectGroup.Salamander,
                "The land stage of the eastern newt is bright orange-red with a row of black-ringed red spots, a warning that it is toxic. " +
                "Efts wander the forest floor after rain and are rarely seen otherwise.",
                0.07f, "woodland floor after rain", SubjectBehaviour.Still, SubjectHabitat.WoodlandFloor, Daytime, 0f, 4);
            s.afterRain = true;
            return s;
        }

        public static NaturalistSubject Snail()
        {
            var s = New("woodland_snail", "Whitelip Snail", "Neohelix albolabris", SubjectGroup.Snail,
                "A large woodland snail whose shell has a flared white lip. It glides over damp logs and leaf litter on a film of slime, " +
                "mostly at night and on wet mornings, and seals itself in when the ground dries.",
                0.03f, "damp logs and stumps after rain", SubjectBehaviour.Still, SubjectHabitat.DeadWood, new[] { true, true, true, false, false, true }, 0f, 4);
            s.afterRain = true;
            return s;
        }

        public static NaturalistSubject Firefly()
        {
            var s = New("firefly", "Common Eastern Firefly", "Photinus pyralis", SubjectGroup.Firefly,
                "At dusk the males rise from the meadow grass flashing a yellow-green light in a J-shaped swoop, and the females answer " +
                "from the grass. The light is nearly heatless. Wait for the flash.",
                0.012f, "meadow edges at dusk", SubjectBehaviour.Glows, SubjectHabitat.MeadowVerge, new[] { true, false, false, false, false, true }, 0.8f, 6);
            s.fillForGrade = TinyFill;
            return s;
        }

        public static NaturalistSubject Mushroom()
        {
            var s = New("woodland_mushroom", "Honey Mushroom", "Armillaria mellea", SubjectGroup.Fungus,
                "Honey-coloured caps in clusters at the foot of stumps and dead wood. The fungus itself is a vast network of black threads " +
                "running under the forest, among the largest living things there are.",
                0.07f, "stumps and dead wood", SubjectBehaviour.Still, SubjectHabitat.DeadWood, Always, 0f, 8);
            return s;
        }

        public static NaturalistSubject OxeyeDaisy()
        {
            var s = New("oxeye_daisy", "Oxeye Daisy", "Leucanthemum vulgare", SubjectGroup.Wildflower,
                "White petals around a yellow disc, in meadows and along verges from early summer. Its old name, day's eye, is for the way " +
                "it faces the sun. A good subject for bees.",
                0.05f, "meadow edges", SubjectBehaviour.Still, SubjectHabitat.MeadowVerge, Daytime, 0f, 6);
            return s;
        }

        private static NaturalistSubject New(string id, string display, string sci, SubjectGroup group, string notes, float size, string habitat,
            SubjectBehaviour behaviour, SubjectHabitat where, bool[] periods, float hover, int count)
        {
            var s = New(id, display, sci, group, notes, size, habitat);
            s.behaviour = behaviour;
            s.where = where;
            s.activePeriods = (bool[])periods.Clone();
            s.hoverHeight = hover;
            s.count = count;
            return s;
        }

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
