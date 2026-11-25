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
        [Tooltip("0-100 photo quality for this subject.")]
        public int score;
        public string quality;
        public string activity;
        public float distance;
        public float frameCoverage;
        public float visibility;
    }

    /// <summary>A saved photograph and what was identified in it.</summary>
    [Serializable]
    public class PhotoRecord
    {
        public string photoId;
        public string takenAtUtc;     // ISO 8601 round-trip
        public string imagePath;      // absolute path of the saved image
        public Vector3 cameraPosition;
        public string locationLabel;
        public List<PhotoSubject> subjects = new List<PhotoSubject>();
    }

    /// <summary>Per-species journal entry. Exists only once a species has been photographed.</summary>
    [Serializable]
    public class SpeciesRecord
    {
        public string speciesId;
        public int photoCount;

        // First sighting = first photograph that identified this species.
        public string firstSightedUtc;
        public string firstSightingLocation;
        public string firstSightingActivity;
        public string firstPhotoId;

        public int bestScore;
        public string bestPhotoId;
    }

    /// <summary>Root save object. Bump <see cref="CurrentVersion"/> and migrate in JournalService when the shape changes.</summary>
    [Serializable]
    public class JournalSaveData
    {
        public const int CurrentVersion = 1;
        public int version = CurrentVersion;
        public List<SpeciesRecord> species = new List<SpeciesRecord>();
        public List<PhotoRecord> photos = new List<PhotoRecord>();
    }
}
