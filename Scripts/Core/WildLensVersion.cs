namespace WildLens.Core
{
    /// <summary>
    /// Package and environment versions, plus the release facts the trailhead board shows. Bump
    /// <see cref="Environment"/> whenever the scene builder changes what it builds, so scenes built by an older
    /// package are detected (HUD notice on Play, builder window, F3 overlay). Update the lists each release.
    /// </summary>
    public static class WildLensVersion
    {
        public const string Package = "1.9.0";
        public const string ReleaseName = "After the Rain";
        public const string ReleaseDate = "November 2026";

        /// <summary>7: reserve destinations and tracks, trailhead kiosk, trail bike, open conifer tiers (0.7.0).
        /// 8: wider blind doorway, smaller headlight (0.7.1).
        /// 9: Willow Creek, ecosystem density zones, habitat identity, storytelling, pull-offs, rebuilt blind (0.8.0).
        /// 10: day cycle, mist, schedule anchors, Reserve Road and footpaths, structured trees, sign standard (0.9.0).
        /// 11: wider graded Reserve Road, road posts, sight lines, planked decks, destination signatures, young trees,
        /// pine form, water hotspots (1.0.0).
        /// 12: fifteen species (heron, raccoon, turkey, barred owl, groundhog, cardinal, blue jay, red-winged
        /// blackbird, opossum, coyote) with residents at each destination (1.1 in progress).
        /// 13: scanned rock-face outcrops on slopes and the ridge; Environment Kit (free packs by ecological role) (1.2).
        /// 14: readable signs and trailhead board, scanned wood on woodwork, flowing tea-coloured water, sedge banks,
        /// rounder broadleaf crowns, trail register (1.3).
        /// 15: sign lettering un-mirrored, landforms v2 (regenerated own terrain), forest floor only under trees, lower ovate
        /// crowns, weather, Creek Ford, kingfisher and sandpiper (1.4).
        /// 16: Willow Falls, hidden places, macro subjects, grounded posts, pond reflections that follow the sky, Trail
        /// 125-style bike, eastern phoebe (1.5).
        /// 17: three-sided Reserve Board and trailhead bike bay, larger sign lines and panels, bedded log seats (1.6).
        /// 18: bundled CC0 animals (wolf as coyote, eagle as hawk), automatic animal and Environment Kit setup (1.6.1).
        /// 19: small life from the user's packs: butterflies, bees, dragonflies, frogs, newts, snails, fireflies, mushrooms
        /// and daisies placed in their habitats when a model is found (1.7).
        /// 20: six new species with residents at the trailhead, a road pull-off, the Fern Glade, the grove, the blind meadow, the pond
        /// brush, the Creek Alcove and the deep woods (crow, robin, chickadee, goldfinch, song sparrow, black bear) (1.8).
        /// 21: Hemlock Ravine with Hemlock Run and its cascade, four more hidden places (Cascade Overlook, Hidden Pond, Rock Shelter,
        /// Old Cabin Ruins), residents for them (bluebird, titmouse, waterthrush), generated small life with its emergence rules,
        /// trunk and rock habitats, one insect per flower (1.9).</summary>
        public const int Environment = 21;

        /// <summary>What a visitor can do. Shown on the trailhead board.</summary>
        public static readonly string[] Features =
        {
            "Wildlife photography",
            "Zoom 1\u00d7\u201316\u00d7 & autofocus",
            "Animal awareness",
            "Field journal",
            "Macro lens & small life",
            "Habitat hotspots",
            "Dawn to dusk, weather & rhythms",
            "Willow Creek & bridges",
            "Reserve Road, bike & pull-offs",
            "Observation blind",
            "Trail signs & nature sounds",
        };

        /// <summary>Newest first; the board shows the first few.</summary>
        public static readonly string[] RecentChanges =
        {
            "1.9  After the rain: wildlife that reads the weather, a ravine and its cascade, small life without packs",
            "1.8  New neighbours: six new species, true animal sizes, pack models that render right",
            "1.7  Small life from your packs for the macro lens; an audit of what you imported",
            "1.6  Trailhead: one Reserve Board, a bike bay, bigger sign text, real animal models",
            "1.5  Falls & field notes: Willow Falls, hidden places, a macro lens, a new trail bike",
            "1.4  Changing skies: weather, hills and ridges, the Creek Ford, signs you can read",
            "1.3  Trailhead: signs you can read, real wood, a flowing creek, fuller trees",
            "1.2  Habitat kit: rock outcrops; free Unity trees, grass and ground by habitat",
            "1.1  More life: herons, owls, turkeys, songbirds and seven more species",
            "1.0  Field season: a smoother Reserve Road, finished destinations, richer woodland edges",
            "0.9  Time of day: dawn mist, golden hour, wildlife rhythms, the Reserve Road, new trees",
            "0.8  Living landscape: Willow Creek, richer habitats, a proper blind, pull-offs",
            "0.7  The reserve opens: four destinations, reserve tracks, a trail bike",
            "0.6  Leaf-card trees, greener meadows, a better deer",
            "0.5  Zoom you can see; scanned skies, ground and rocks",
            "0.4  Painted terrain, lighting, depth of field",
        };
    }
}
