using System.Collections.Generic;
using UnityEngine;
using WildLens.Environment;

namespace WildLens.Data
{
    /// <summary>What kind of small subject (append only: serialized as ints).</summary>
    public enum SubjectGroup { Wildflower, Fungus, Butterfly, Dragonfly, Bee, Beetle, Firefly, Mantis, Frog, Salamander, Moth, Snail, Damselfly, Fern, Moss }

    /// <summary>Where the builder places a small subject that has a model (append only: serialized as ints).</summary>
    public enum SubjectHabitat { MeadowVerge, Flowers, WaterEdge, WoodlandFloor, DeadWood, Trunk, Rock }

    /// <summary>
    /// How a small subject behaves. 1.5 implements <see cref="Still"/> (plants, fungi, a resting beetle); the others are
    /// reserved so data can be written ahead of the behaviour (append only).
    /// </summary>
    public enum SubjectBehaviour { Still, Perches, Flits, Hops, Glows }

    /// <summary>
    /// Weather and hour rules beyond the plain period / rain flags (1.9; append only). They read only the clock and the weather
    /// (<c>NaturalConditions</c>), so they are the same on every playthrough and can be learnt from the forecast.
    /// Sunshine: dry, not overcast, in daylight (butterflies, bees). Warm: the warm part of a dry day (dragonflies).
    /// FairEvening: a dry, clear evening with no rain that afternoon, from about 18:45 (fireflies, a little later each).
    /// WetNight: evening, night or dawn within about 14 game hours of rain (gray treefrogs).
    /// </summary>
    public enum SubjectCondition { None, Sunshine, Warm, FairEvening, WetNight }

    /// <summary>
    /// A second tier of a subject's instances that is out only in some conditions (1.9; append only). SunAfterRain: sunshine within
    /// three game hours of a shower. AfterRain: within six hours of rain. AfterRainOrDusk: that, or dusk, night and dawn.
    /// RainedLately: it rained today or yesterday (the rest of that day and the next). The other instances are not affected.
    /// </summary>
    public enum SubjectExtra { None, SunAfterRain, AfterRain, AfterRainOrDusk, RainedLately }

    /// <summary>
    /// Which generated model a subject uses when it has no model of its own (append only; the name is the model key in
    /// SmallLifeShapes). FernScan is the bundled CC0 fern scan, not a generated model.
    /// </summary>
    public enum SmallLifeModel
    {
        None, Monarch, Swallowtail, CabbageWhite, LunaMoth, Whitetail, Jewelwing, BumbleBee, HoneyBee, Firefly, LadyBeetle, Mantis,
        LeopardFrog, GreenFrog, GrayTreefrog, RedEft, RedBackedSalamander, Snail, HoneyMushroom, YellowFlyAgaric, TurkeyTail,
        OxeyeDaisy, Milkweed, BlackEyedSusan, Violet, HaircapMoss, Cattail, FernScan,
    }

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
        public const int CurrentDataVersion = 2;
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
        [Tooltip("1.9: for a tall or large plant, the side in metres of the part that is graded (a frond's crown, a seed head) instead of the whole plant. " +
                 "0: the focus box is 1.4 times Size, as for everything else.")]
        [Min(0f)] public float focusPartSize;

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

        [Header("Natural conditions and generated models (1.9)")]
        [Tooltip("The model WildLens generates for this subject when it has no model of its own (a pack model, if there is one, always wins). " +
                 "None: it appears only with a model.")]
        public SmallLifeModel generatedModel = SmallLifeModel.None;
        [Tooltip("When it shows beyond the hours above: sunshine only, warm days, fair evenings, wet nights.")]
        public SubjectCondition condition = SubjectCondition.None;
        [Tooltip("A second tier of instances that appears only in these conditions (more butterflies in the sun after rain, more frogs after rain and at dusk, " +
                 "more mushrooms the day after rain).")]
        public SubjectExtra extraWhen = SubjectExtra.None;
        [Tooltip("The share of 'Count' that belongs to that second tier.")]
        [Range(0f, 0.6f)] public float extraFraction;

        /// <summary>Plants and fungi stay where they grow; only animals vanish at the wrong hour or in the wrong weather.</summary>
        public bool IsPlantOrFungus => group == SubjectGroup.Wildflower || group == SubjectGroup.Fungus || group == SubjectGroup.Fern || group == SubjectGroup.Moss;

        /// <summary>
        /// 1.9: fills in the fields version 1 assets do not have, from the current defaults, and leaves every tuned value alone.
        /// <paramref name="oldDefaultCount"/> is what version 1 shipped as the count: a count still at that value moves to the new default.
        /// </summary>
        public void UpgradeToV2(NaturalistSubject fresh, int oldDefaultCount)
        {
            if (fresh == null) return;
            generatedModel = fresh.generatedModel;
            condition = fresh.condition;
            extraWhen = fresh.extraWhen;
            extraFraction = fresh.extraFraction;
            if (oldDefaultCount > 0 && count == oldDefaultCount) count = fresh.count;
            // 1.9 reworked the grading thresholds for how close a standing or crouching photographer can really get (the camera is 1.65 m
            // up standing and 0.95 m crouched, so a subject on the ground is never nearer than about a metre): a fill still at one of the two
            // values version 1 shipped moves to the new default; a tuned one is left alone.
            bool v1Fill = (Mathf.Approximately(fillForGrade.x, 0.18f) && Mathf.Approximately(fillForGrade.y, 0.35f)) ||
                          (Mathf.Approximately(fillForGrade.x, 0.10f) && Mathf.Approximately(fillForGrade.y, 0.22f));
            if (v1Fill) fillForGrade = fresh.fillForGrade;
            // The generated dragonfly sits on its own perch, the luna moth on trunks and the ladybird on flowers (where it is within reach of a lens):
            // only for defaults that were never changed, and only when no pack model is in use.
            if (prefab == null)
            {
                if (subjectId == "common_whitetail" && Mathf.Approximately(hoverHeight, 0.4f)) hoverHeight = fresh.hoverHeight;
                if (subjectId == "luna_moth" && where == SubjectHabitat.DeadWood) where = fresh.where;
                if (subjectId == "ladybird_beetle" && where == SubjectHabitat.MeadowVerge) where = fresh.where;
            }
            dataVersion = CurrentDataVersion;
        }

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

    /// <summary>
    /// The macro subjects: the two CC0 plant scans of 1.5, the twelve small-life subjects of 1.7, and in 1.9 fifteen more with generated
    /// models (butterflies, a damselfly, a bee, frogs, a salamander, fungi, wildflowers, a moss, a cattail) and a fern scan.
    /// Existing ids and their order never change; new subjects are appended.
    /// </summary>
    public static class SubjectDefaults
    {
        public static List<NaturalistSubject> CreateAll() => new List<NaturalistSubject>
        {
            Dandelion(), Periwinkle(),
            Monarch(), LunaMoth(), Whitetail(), BumbleBee(), LadyBeetle(), Mantis(), LeopardFrog(), Newt(), Snail(), Firefly(), Mushroom(), OxeyeDaisy(),
            // 1.9
            Swallowtail(), CabbageWhite(), Jewelwing(), HoneyBee(), GreenFrog(), GrayTreefrog(), RedBackedSalamander(), YellowFlyAgaric(), TurkeyTail(),
            WoodlandFern(), Milkweed(), BlackEyedSusan(), BlueViolet(), HaircapMoss(), Cattail(),
        };

        /// <summary>What version 1 shipped as each subject's count (see <see cref="NaturalistSubject.UpgradeToV2"/>); 0 for subjects new in 1.9.</summary>
        public static int V1Count(string subjectId)
        {
            switch (subjectId)
            {
                case "monarch_butterfly": case "bumble_bee": case "ladybird_beetle": case "firefly": case "oxeye_daisy": return 6;
                case "luna_moth": case "praying_mantis": case "red_spotted_newt": case "woodland_snail": return 4;
                case "common_whitetail": case "northern_leopard_frog": return 5;
                case "woodland_mushroom": return 8;
                default: return 0;
            }
        }

        // DayPeriod order: Night, Dawn, Morning, Midday, GoldenHour, Dusk.
        private static readonly bool[] Daytime = { false, false, true, true, true, false };
        private static readonly bool[] Always = { true, true, true, true, true, true };
        private static readonly bool[] Light = { false, true, true, true, true, true };   // dawn to dusk, for plants and ground life
        private static readonly bool[] Evening = { true, true, false, false, false, true }; // night, dawn, dusk

        // Frame fill is the largest side of the projected bounds over the frame (MacroAnalyzer). At the macro lens's 1.7x step the frame is
        // 2 * d * tan(12 deg) / 1.7 = 0.25 d tall, and the focus box is 1.4 times the subject's size (MacroSubject.focusSize), so seen
        // head-on fill = 5.6 * size / d (a box seen from above or aslant projects larger). Excellent needs d <= 5.6 * size / fill.y.
        // But d cannot be small: the camera is 1.65 m up standing and 0.95 m crouched, so a subject on the ground is never nearer than about
        // a metre, and the thresholds below are set so that Excellent is reachable crouched at roughly that distance (fill.y <= 5.6 * size
        // for ground subjects, and a little less for the hoppers, which are not approached closer than 0.9 m along the ground, i.e. 1.3 m
        // away crouched or 1.8 m standing). Subjects on flowers, trunks and logs can be nearer. See the table in the 1.9 notes of Systems.md.
        private static readonly Vector2 BeeFill = new Vector2(0.08f, 0.15f);       // a 2 cm bee: Excellent within 0.75 m (flowers at 0.4 m or more)
        private static readonly Vector2 FrogFill = new Vector2(0.10f, 0.20f);      // 8 cm frogs: Excellent out to 2.2 m, reachable from 1.3 m
        private static readonly Vector2 FireflyFill = new Vector2(0.08f, 0.16f);   // judged on the flash and halo (a 4 cm focus box): Excellent within 1 m

        public static NaturalistSubject Monarch()
        {
            var s = New("monarch_butterfly", "Monarch Butterfly", "Danaus plexippus", SubjectGroup.Butterfly,
                "Orange wings veined in black, a warning to birds: as caterpillars they fed on milkweed and taste foul. Look for one sipping nectar on " +
                "milkweed in the sun, and wait for the wings to open. More come out in the sunshine just after a shower.",
                0.09f, "sunny flowers, milkweed", SubjectBehaviour.Flits, SubjectHabitat.Flowers, Daytime, 0f, 8);
            s.hidesInRain = true;
            s.generatedModel = SmallLifeModel.Monarch;
            s.condition = SubjectCondition.Sunshine;
            s.extraWhen = SubjectExtra.SunAfterRain;
            s.extraFraction = 0.4f;
            return s;
        }

        public static NaturalistSubject LunaMoth()
        {
            var s = New("luna_moth", "Luna Moth", "Actias luna", SubjectGroup.Moth,
                "A pale green moth with long tails and an eyespot on each wing. The adult has no mouth and lives about a week, flying by night. " +
                "At dawn and dusk look for one clinging to a trunk, wings spread flat against the bark.",
                0.10f, "tree trunks in the woods", SubjectBehaviour.Still, SubjectHabitat.Trunk, new[] { true, true, false, false, false, true }, 0f, 4);
            s.generatedModel = SmallLifeModel.LunaMoth;
            return s;
        }

        public static NaturalistSubject Whitetail()
        {
            var s = New("common_whitetail", "Common Whitetail", "Plathemis lydia", SubjectGroup.Dragonfly,
                "A stout dragonfly; the male has a chalky white abdomen and a broad dark band across each wing. It perches on a bare stem " +
                "by the water on warm, dry days, darts out after a passing insect, and comes back to the very same perch.",
                0.06f, "water's edge, bare stems", SubjectBehaviour.Perches, SubjectHabitat.WaterEdge, Daytime, 0f, 5);
            s.hidesInRain = true;
            s.generatedModel = SmallLifeModel.Whitetail;
            s.condition = SubjectCondition.Warm;
            return s;
        }

        public static NaturalistSubject BumbleBee()
        {
            var s = New("bumble_bee", "Common Eastern Bumble Bee", "Bombus impatiens", SubjectGroup.Bee,
                "Round, furry and unhurried, working flower to flower and carrying pollen on its hind legs. A feeding bee pays no attention " +
                "to a patient photographer; stay low and still, and move in close. It shelters in rain and works harder when the sun returns.",
                0.02f, "sunny flowers", SubjectBehaviour.Flits, SubjectHabitat.Flowers, Daytime, 0f, 8);
            s.hidesInRain = true;
            s.fillForGrade = BeeFill;
            s.generatedModel = SmallLifeModel.BumbleBee;
            s.condition = SubjectCondition.Sunshine;
            s.extraWhen = SubjectExtra.SunAfterRain;
            s.extraFraction = 0.33f;
            return s;
        }

        public static NaturalistSubject LadyBeetle()
        {
            var s = New("ladybird_beetle", "Spotted Lady Beetle", "Coleomegilla maculata", SubjectGroup.Beetle,
                "A pink-red beetle with black spots, hunting aphids and eating pollen along meadow stems. Tiny, so come close, " +
                "slowly, and let the lens do the rest.",
                0.012f, "meadow flowers and stems", SubjectBehaviour.Still, SubjectHabitat.Flowers, Daytime, 0f, 6);
            s.fillForGrade = BeeFill;
            s.generatedModel = SmallLifeModel.LadyBeetle;
            return s;
        }

        public static NaturalistSubject Mantis()
        {
            var s = New("praying_mantis", "Carolina Mantis", "Stagmomantis carolina", SubjectGroup.Mantis,
                "Small, brown or grey, and almost impossible to see among dry grass stems. It waits with its forelegs folded until something comes " +
                "within reach, and it turns its head to watch you, the only insect that will.",
                0.06f, "meadow grass", SubjectBehaviour.Still, SubjectHabitat.MeadowVerge, new[] { false, false, true, true, true, true }, 0f, 4);
            s.fillForGrade = new Vector2(0.12f, 0.25f);
            s.generatedModel = SmallLifeModel.Mantis;
            return s;
        }

        public static NaturalistSubject LeopardFrog()
        {
            var s = New("northern_leopard_frog", "Northern Leopard Frog", "Lithobates pipiens", SubjectGroup.Frog,
                "Green or brown with pale-edged dark spots and a pale ridge down each side. It sits half-hidden at the water's edge and leaps " +
                "with a splash if startled; approach slowly and it will stay. Many more are out after rain and at dusk.",
                0.08f, "pond and creek edges", SubjectBehaviour.Hops, SubjectHabitat.WaterEdge, Always, 0f, 8);
            s.fillForGrade = FrogFill;
            s.generatedModel = SmallLifeModel.LeopardFrog;
            s.extraWhen = SubjectExtra.AfterRainOrDusk;
            s.extraFraction = 0.4f;
            return s;
        }

        public static NaturalistSubject Newt()
        {
            var s = New("red_spotted_newt", "Eastern Newt (red eft)", "Notophthalmus viridescens", SubjectGroup.Salamander,
                "The land stage of the eastern newt is bright orange-red with a row of black-ringed red spots, a warning that it is toxic. " +
                "Efts wander the forest floor after rain and are rarely seen otherwise.",
                0.07f, "woodland floor after rain", SubjectBehaviour.Still, SubjectHabitat.WoodlandFloor, Daytime, 0f, 4);
            s.afterRain = true;
            s.fillForGrade = new Vector2(0.14f, 0.28f);
            s.generatedModel = SmallLifeModel.RedEft;
            return s;
        }

        public static NaturalistSubject Snail()
        {
            var s = New("woodland_snail", "Whitelip Snail", "Neohelix albolabris", SubjectGroup.Snail,
                "A large woodland snail whose shell has a flared white lip. It glides over damp logs and leaf litter on a film of slime, " +
                "mostly at night and on wet mornings, and seals itself in when the ground dries.",
                0.03f, "damp logs and stumps after rain", SubjectBehaviour.Still, SubjectHabitat.DeadWood, new[] { true, true, true, false, false, true }, 0f, 4);
            s.afterRain = true;
            s.fillForGrade = new Vector2(0.12f, 0.25f);
            s.generatedModel = SmallLifeModel.Snail;
            return s;
        }

        public static NaturalistSubject Firefly()
        {
            var s = New("firefly", "Common Eastern Firefly", "Photinus pyralis", SubjectGroup.Firefly,
                "At dusk the males rise from the meadow grass flashing a yellow-green light in a J-shaped swoop, and the females answer " +
                "from the grass. The light is nearly heatless. They come only on calm, clear, dry evenings, after a fair afternoon: wait for the flash.",
                0.012f, "meadow edges and the pond, fair evenings", SubjectBehaviour.Glows, SubjectHabitat.MeadowVerge, new[] { true, false, false, false, false, true }, 0.8f, 16);
            s.fillForGrade = FireflyFill;
            s.generatedModel = SmallLifeModel.Firefly;
            s.condition = SubjectCondition.FairEvening;
            return s;
        }

        public static NaturalistSubject Mushroom()
        {
            var s = New("woodland_mushroom", "Honey Mushroom", "Armillaria mellea", SubjectGroup.Fungus,
                "Honey-coloured caps in clusters at the foot of stumps and dead wood. The fungus itself is a vast network of black threads " +
                "running under the forest, among the largest living things there are. More fruit bodies push up for a day or two after rain.",
                0.07f, "stumps and dead wood", SubjectBehaviour.Still, SubjectHabitat.DeadWood, Always, 0f, 14);
            s.fillForGrade = new Vector2(0.15f, 0.30f);
            s.generatedModel = SmallLifeModel.HoneyMushroom;
            s.extraWhen = SubjectExtra.RainedLately;
            s.extraFraction = 0.4f;
            return s;
        }

        public static NaturalistSubject OxeyeDaisy()
        {
            var s = New("oxeye_daisy", "Oxeye Daisy", "Leucanthemum vulgare", SubjectGroup.Wildflower,
                "White petals around a yellow disc, in meadows and along verges from early summer. Its old name, day's eye, is for the way " +
                "it faces the sun. A good subject for bees.",
                0.05f, "meadow edges", SubjectBehaviour.Still, SubjectHabitat.MeadowVerge, Daytime, 0f, 6);
            s.generatedModel = SmallLifeModel.OxeyeDaisy;
            return s;
        }

        // ------------------------------------------------------------------ 1.9

        public static NaturalistSubject Swallowtail()
        {
            var s = New("eastern_tiger_swallowtail", "Eastern Tiger Swallowtail", "Papilio glaucus", SubjectGroup.Butterfly,
                "A big yellow butterfly with black tiger stripes and a tail on each hindwing; the blue and orange spots near the tails are the giveaway. " +
                "It sails over the meadow on sunny days and stops to sip at milkweed and black-eyed Susans.",
                0.10f, "sunny flowers", SubjectBehaviour.Flits, SubjectHabitat.Flowers, Daytime, 0f, 5);
            s.hidesInRain = true;
            s.generatedModel = SmallLifeModel.Swallowtail;
            s.condition = SubjectCondition.Sunshine;
            s.extraWhen = SubjectExtra.SunAfterRain;
            s.extraFraction = 0.4f;
            return s;
        }

        public static NaturalistSubject CabbageWhite()
        {
            var s = New("cabbage_white", "Cabbage White", "Pieris rapae", SubjectGroup.Butterfly,
                "Small and plain white with a smudge of grey at each wingtip, one of the first butterflies up in spring and the last to go. " +
                "It drifts low over the meadow flowers whenever the sun is out.",
                0.045f, "sunny meadow flowers", SubjectBehaviour.Flits, SubjectHabitat.Flowers, Daytime, 0f, 6);
            s.hidesInRain = true;
            s.fillForGrade = new Vector2(0.14f, 0.28f);
            s.generatedModel = SmallLifeModel.CabbageWhite;
            s.condition = SubjectCondition.Sunshine;
            s.extraWhen = SubjectExtra.SunAfterRain;
            s.extraFraction = 0.4f;
            return s;
        }

        public static NaturalistSubject Jewelwing()
        {
            var s = New("ebony_jewelwing", "Ebony Jewelwing", "Calopteryx maculata", SubjectGroup.Damselfly,
                "A slim damselfly with a metallic green body and jet-black wings that it flutters open and shut like a butterfly. It keeps to " +
                "shaded streams, perched on a stem over the water, on warm, dry days.",
                0.055f, "shaded creek banks", SubjectBehaviour.Perches, SubjectHabitat.WaterEdge, Daytime, 0f, 5);
            s.hidesInRain = true;
            s.generatedModel = SmallLifeModel.Jewelwing;
            s.condition = SubjectCondition.Warm;
            return s;
        }

        public static NaturalistSubject HoneyBee()
        {
            var s = New("honey_bee", "Western Honey Bee", "Apis mellifera", SubjectGroup.Bee,
                "Slimmer than a bumble bee, amber and brown, working the sunny flowers in tidy rounds and carrying pollen back to the hive on its hind legs. " +
                "It stays home in the rain and returns the moment the sun does.",
                0.013f, "sunny flowers", SubjectBehaviour.Flits, SubjectHabitat.Flowers, Daytime, 0f, 6);
            s.hidesInRain = true;
            s.fillForGrade = new Vector2(0.07f, 0.13f);
            s.generatedModel = SmallLifeModel.HoneyBee;
            s.condition = SubjectCondition.Sunshine;
            s.extraWhen = SubjectExtra.SunAfterRain;
            s.extraFraction = 0.33f;
            return s;
        }

        public static NaturalistSubject GreenFrog()
        {
            var s = New("green_frog", "Green Frog", "Lithobates clamitans", SubjectGroup.Frog,
                "Green to bronze, with a bright green upper lip and a ridge down each side of the back that stops short of the hip. It sits at the " +
                "water's edge by day and calls with a twang like a loose banjo string; many more are out after rain and at dusk.",
                0.085f, "creek and pond edges", SubjectBehaviour.Hops, SubjectHabitat.WaterEdge, Always, 0f, 8);
            s.fillForGrade = FrogFill;
            s.generatedModel = SmallLifeModel.GreenFrog;
            s.extraWhen = SubjectExtra.AfterRainOrDusk;
            s.extraFraction = 0.4f;
            return s;
        }

        public static NaturalistSubject GrayTreefrog()
        {
            var s = New("gray_treefrog", "Gray Treefrog", "Dryophytes versicolor", SubjectGroup.Frog,
                "Mottled grey and green, it can turn paler or darker to match the bark; its toes end in sticky pads and a bright yellow-orange hides under " +
                "the thighs. On warm, wet evenings and after rain it climbs the trunks and trills from the trees.",
                0.05f, "tree trunks, wet evenings", SubjectBehaviour.Still, SubjectHabitat.Trunk, Evening, 0f, 6);
            s.fillForGrade = new Vector2(0.12f, 0.24f);
            s.generatedModel = SmallLifeModel.GrayTreefrog;
            s.condition = SubjectCondition.WetNight;
            return s;
        }

        public static NaturalistSubject RedBackedSalamander()
        {
            var s = New("red_backed_salamander", "Eastern Red-backed Salamander", "Plethodon cinereus", SubjectGroup.Salamander,
                "A slender, dark woodland salamander with a red-brown stripe down its back. It has no lungs and breathes through its skin, so it needs " +
                "damp ground: after rain, look on the forest floor and along rotting logs.",
                0.09f, "damp woodland floor after rain", SubjectBehaviour.Still, SubjectHabitat.WoodlandFloor, Light, 0f, 4);
            s.afterRain = true;
            s.generatedModel = SmallLifeModel.RedBackedSalamander;
            return s;
        }

        public static NaturalistSubject YellowFlyAgaric()
        {
            var s = New("yellow_fly_agaric", "Yellow Fly Agaric", "Amanita muscaria var. guessowii", SubjectGroup.Fungus,
                "The eastern form of the fly agaric has a yellow-orange cap dotted with pale warts. It grows from the soil near birch and conifer roots " +
                "and is poisonous: look, never taste. More come up in the day or two after rain.",
                0.10f, "woodland floor, under the old trees", SubjectBehaviour.Still, SubjectHabitat.WoodlandFloor, Always, 0f, 8);
            s.generatedModel = SmallLifeModel.YellowFlyAgaric;
            s.extraWhen = SubjectExtra.RainedLately;
            s.extraFraction = 0.4f;
            return s;
        }

        public static NaturalistSubject TurkeyTail()
        {
            var s = New("turkey_tail", "Turkey Tail", "Trametes versicolor", SubjectGroup.Fungus,
                "Thin, banded brackets in browns, greys and rust, like the tail of a turkey, layered along dead logs all year. It is a decomposer; " +
                "the pores on the pale underside are too small to see.",
                0.08f, "fallen logs and stumps", SubjectBehaviour.Still, SubjectHabitat.DeadWood, Always, 0f, 8);
            s.generatedModel = SmallLifeModel.TurkeyTail;
            return s;
        }

        public static NaturalistSubject WoodlandFern()
        {
            var s = New("woodland_fern", "Woodland Fern", "Dryopteris sp.", SubjectGroup.Fern,
                "A fern of the shade: fronds arch out of the leaf litter, each leaflet cut into smaller lobes. Ferns have no flowers or seeds; they spread " +
                "by spores. Look in the fern glade and under the old trees.",
                0.5f, "shady woodland floor", SubjectBehaviour.Still, SubjectHabitat.WoodlandFloor, Light, 0f, 6);
            // A 0.5 m plant graded whole is Excellent from anywhere (its 0.7 m focus box fills the frame, and the fill is capped at 1, out to the
            // 2.5 m limit: 4 * 0.7 / 2.5 = 1.1), and no threshold can change that. So the grade is taken on one 12 cm patch at the focus point
            // (the middle of the crown) instead: fill = 0.12 / (0.25 d) = 0.48 / d at the 1.7x step, so Good needs d <= 2.4 m and Excellent
            // d <= 1.4 m (a metre and a half off the ground-level crown is a crouch and a few steps, as for the other ground subjects).
            s.focusPartSize = 0.12f;
            s.fillForGrade = new Vector2(0.20f, 0.35f);
            s.generatedModel = SmallLifeModel.FernScan;
            return s;
        }

        public static NaturalistSubject Milkweed()
        {
            var s = New("common_milkweed", "Common Milkweed", "Asclepias syriaca", SubjectGroup.Wildflower,
                "Tall, with big oval leaves and round clusters of dusty-pink flowers that smell sweet. Monarch caterpillars feed only on milkweeds, " +
                "so the butterflies are never far away: watch the clusters on sunny days.",
                0.09f, "meadow edges", SubjectBehaviour.Still, SubjectHabitat.MeadowVerge, Daytime, 0f, 12);
            s.generatedModel = SmallLifeModel.Milkweed;
            return s;
        }

        public static NaturalistSubject BlackEyedSusan()
        {
            var s = New("black_eyed_susan", "Black-eyed Susan", "Rudbeckia hirta", SubjectGroup.Wildflower,
                "Golden rays around a dark brown cone, on a bristly stem, blooming from midsummer. Bees and swallowtails come for the nectar; " +
                "photograph it from low down against the sky.",
                0.07f, "meadow edges and verges", SubjectBehaviour.Still, SubjectHabitat.MeadowVerge, Daytime, 0f, 12);
            s.generatedModel = SmallLifeModel.BlackEyedSusan;
            return s;
        }

        public static NaturalistSubject BlueViolet()
        {
            var s = New("common_blue_violet", "Common Blue Violet", "Viola sororia", SubjectGroup.Wildflower,
                "A low plant of heart-shaped leaves with violet-blue flowers in spring; the lower petal has a pale throat with dark veins that guide " +
                "bees in. Look in the shade near the fern glade.",
                0.025f, "shady woodland floor", SubjectBehaviour.Still, SubjectHabitat.WoodlandFloor, Daytime, 0f, 6);
            s.fillForGrade = new Vector2(0.07f, 0.12f);
            s.generatedModel = SmallLifeModel.Violet;
            return s;
        }

        public static NaturalistSubject HaircapMoss()
        {
            var s = New("haircap_moss", "Haircap Moss", "Polytrichum commune", SubjectGroup.Moss,
                "Dense green cushions of tiny star-shaped shoots on stones and logs; the thin stalks of its spore capsules carry pale, hairy caps. " +
                "Moss holds water like a sponge and swells after rain.",
                0.08f, "boulders and old logs", SubjectBehaviour.Still, SubjectHabitat.Rock, Light, 0f, 8);
            s.fillForGrade = new Vector2(0.18f, 0.35f);
            s.generatedModel = SmallLifeModel.HaircapMoss;
            return s;
        }

        public static NaturalistSubject Cattail()
        {
            var s = New("common_cattail", "Common Cattail", "Typha latifolia", SubjectGroup.Wildflower,
                "The brown sausage is a spike of thousands of tiny female flowers, with a slim pollen-bearing spike above it. Red-winged blackbirds " +
                "nest among cattails; look along the pond's edge.",
                0.14f, "the pond's edge", SubjectBehaviour.Still, SubjectHabitat.WaterEdge, Light, 0f, 5);
            // The default fill (0.18, 0.35) made its 0.196 m box Excellent out to 4 * 0.196 / 0.35 = 2.2 m, nearly the whole 2.5 m range. Graded
            // on the 10 cm seed head (the model's focus point is at the head): fill = 0.10 / (0.25 d) = 0.4 / d, so with (0.20, 0.32) Good
            // needs d <= 2.0 m and Excellent d <= 1.25 m (a head about a metre up on a stalk, from the bank).
            s.focusPartSize = 0.10f;
            s.fillForGrade = new Vector2(0.20f, 0.32f);
            s.generatedModel = SmallLifeModel.Cattail;
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
            s.name = "Subject_" + display.Replace(" ", ""); // unchanged since 1.5: it is the asset file name
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
