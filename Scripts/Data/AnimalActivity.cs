namespace WildLens.Data
{
    /// <summary>
    /// Observable behaviours. Recorded with each photograph so the journal can note what the animal was doing;
    /// future milestones can score or catalogue behaviours from this.
    /// </summary>
    public enum AnimalActivity
    {
        Wandering,
        Grazing,
        Foraging,
        Sniffing,
        LookingAround,
        Grooming,
        Resting,
        Alert,
        Fleeing,
        Climbing,
        Clinging,
        Soaring,
        Gliding,
        Perched,

        // Milestone 2 (appended: enum values are serialized as integers, never reorder)
        Walking,
        Running,
        Hopping,
        Sitting,
        Flying,
        Landing,
        TakingOff,
    }

    public static class AnimalActivityText
    {
        /// <summary>Field-note phrasing, e.g. "observed grazing".</summary>
        public static string Describe(AnimalActivity activity)
        {
            switch (activity)
            {
                case AnimalActivity.Wandering: return "moving through the area";
                case AnimalActivity.Grazing: return "grazing";
                case AnimalActivity.Foraging: return "foraging on the ground";
                case AnimalActivity.Sniffing: return "investigating a scent";
                case AnimalActivity.LookingAround: return "scanning its surroundings";
                case AnimalActivity.Grooming: return "grooming";
                case AnimalActivity.Resting: return "resting";
                case AnimalActivity.Alert: return "alert and watchful";
                case AnimalActivity.Fleeing: return "fleeing";
                case AnimalActivity.Climbing: return "climbing a tree";
                case AnimalActivity.Clinging: return "clinging to a tree trunk";
                case AnimalActivity.Soaring: return "soaring on thermals";
                case AnimalActivity.Gliding: return "gliding";
                case AnimalActivity.Perched: return "perched";
                case AnimalActivity.Walking: return "walking";
                case AnimalActivity.Running: return "running";
                case AnimalActivity.Hopping: return "hopping";
                case AnimalActivity.Sitting: return "sitting up";
                case AnimalActivity.Flying: return "in flight";
                case AnimalActivity.Landing: return "coming in to land";
                case AnimalActivity.TakingOff: return "taking flight";
                default: return activity.ToString().ToLowerInvariant();
            }
        }
    }
}
