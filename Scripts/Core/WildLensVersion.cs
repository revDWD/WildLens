namespace WildLens.Core
{
    /// <summary>
    /// Package and environment versions. Bump <see cref="Environment"/> whenever the scene builder changes what it
    /// builds, so scenes built by an older package are detected (HUD notice on Play, builder window, F3 overlay).
    /// </summary>
    public static class WildLensVersion
    {
        public const string Package = "0.6.0";

        /// <summary>6: leaf-card canopies, green terrain tints (0.6.0).</summary>
        public const int Environment = 6;
    }
}
