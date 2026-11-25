namespace WildLens.Core
{
    /// <summary>
    /// Package and environment versions, plus the release facts the trailhead board shows. Bump
    /// <see cref="Environment"/> whenever the scene builder changes what it builds, so scenes built by an older
    /// package are detected (HUD notice on Play, builder window, F3 overlay). Update the lists each release.
    /// </summary>
    public static class WildLensVersion
    {
        public const string Package = "0.7.0";
        public const string ReleaseName = "The Reserve";
        public const string ReleaseDate = "November 2026";

        /// <summary>7: reserve destinations and tracks, trailhead kiosk, trail bike, open conifer tiers (0.7.0).</summary>
        public const int Environment = 7;

        /// <summary>What a visitor can do. Shown on the trailhead board.</summary>
        public static readonly string[] Features =
        {
            "Wildlife photography",
            "Zoom lens 1\u00d7 to 16\u00d7",
            "Autofocus & keeper prints",
            "Animal awareness",
            "Identify by observation",
            "Habitat hotspots",
            "Field journal",
            "Reserve destinations",
            "Trail bike",
            "Observation blind",
        };

        /// <summary>Newest first; the board shows the first few.</summary>
        public static readonly string[] RecentChanges =
        {
            "0.7  The reserve opens: four destinations, reserve tracks, a trail bike",
            "0.6  Leaf-card trees, greener meadows, a better deer",
            "0.5  Zoom you can see; scanned skies, ground and rocks",
            "0.4  Painted terrain, lighting, depth of field",
        };
    }
}
