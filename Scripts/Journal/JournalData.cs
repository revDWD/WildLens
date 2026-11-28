using System;
using System.Collections.Generic;
using UnityEngine;

namespace WildLens.Journal
{
    /// <summary>One identified animal in a photograph.</summary>
    [Serializable]
    public class PhotoSubject
    {
        public string speciesId;
        /// <summary>"Poor", "Good" or "Excellent" (see PhotoQualityEvaluator).</summary>
        public string quality;
        /// <summary>What held the photo back, e.g. "off-centre". Empty for Excellent.</summary>
        public string hint;
        /// <summary>Internal 0-1 ranking used only to choose a species' best photo. Never displayed.</summary>
        public float rating;
        public string activity;
        public float distance;
        public float frameCoverage;
        public float visibility;

        [Tooltip("Legacy (save version 1): 0-100 score. Migrated to rating/quality on load.")]
        public int score;
    }

    /// <summary>A saved photograph and what was identified in it.</summary>
    [Serializable]
    public class PhotoRecord
    {
        public string photoId;
        public string takenAtUtc;     // ISO 8601 round-trip
        public string imagePath;      // absolute path of the saved image
        public Vector3 cameraPosition;
        /// <summary>35 mm-equivalent focal length the photo was taken at.</summary>
        public float focalLength;
        public string locationLabel;
        public List<PhotoSubject> subjects = new List<PhotoSubject>();
    }

    /// <summary>How a species first entered the journal.</summary>
    public static class IdentificationMethod
    {
        public const string Observation = "observation";
        public const string Photograph = "photograph";
    }

    /// <summary>
    /// Per-species journal entry. Exists once a species has been identified, either by observing it through
    /// the camera or by photographing it.
    /// </summary>
    [Serializable]
    public class SpeciesRecord
    {
        public string speciesId;
        public int photoCount;
        /// <summary><see cref="IdentificationMethod"/> value.</summary>
        public string identifiedBy;
        /// <summary>
        /// Distinct behaviours (AnimalActivity names) seen through the camera or in photos. Recorded now so that
        /// future behaviour photography and field notes have history to work with; not displayed yet.
        /// </summary>
        public List<string> observedBehaviors = new List<string>();

        // First sighting = first identification (observation or photograph).
        public string firstSightedUtc;
        public string firstSightingLocation;
        public string firstSightingActivity;
        public string firstPhotoId;

        public string bestQuality;
        public float bestRating;
        public string bestPhotoId;

        [Tooltip("Legacy (save version 1). Migrated to bestRating/bestQuality on load.")]
        public int bestScore;
    }

    /// <summary>
    /// A small subject in the field notebook (1.5 macro groundwork): first found by a macro photograph, with its best
    /// photo. Older saves load with an empty notebook (JsonUtility keeps the field's default), so no migration is needed.
    /// </summary>
    [Serializable]
    public class NotebookRecord
    {
        public string subjectId;
        public string displayName;
        public string group;
        public int photoCount;
        public string firstFoundUtc;
        public string bestQuality;
        public string bestPhotoId;
    }

    /// <summary>Root save object. Bump <see cref="CurrentVersion"/> and migrate in JournalService when the shape changes.</summary>
    [Serializable]
    public class JournalSaveData
    {
        public const int CurrentVersion = 3;
        public int version = CurrentVersion;
        public List<SpeciesRecord> species = new List<SpeciesRecord>();
        public List<PhotoRecord> photos = new List<PhotoRecord>();
        /// <summary>Small subjects photographed in macro (1.5).</summary>
        public List<NotebookRecord> notebook = new List<NotebookRecord>();
    }
}
