using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using WildLens.Data;
using WildLens.Photography;

namespace WildLens.Journal
{
    /// <summary>Result for one species in a newly registered photo.</summary>
    public struct SpeciesIdentification
    {
        public SpeciesDefinition species;
        public PhotoSubject subject;
        public bool isNewSpecies;
        /// <summary>First photograph of a species that may already be known from observation.</summary>
        public bool isFirstPhoto;
        public bool isNewBest;
    }

    /// <summary>
    /// The field journal model: which species have been discovered, how many photographs of each, first
    /// sightings, best shots. Plain C# (no MonoBehaviour) so it is easy to test and to reuse from other systems.
    /// Persists to JSON in persistentDataPath.
    /// </summary>
    public class JournalService
    {
        private readonly SpeciesDatabase _database;
        private readonly string _savePath;
        private JournalSaveData _data = new JournalSaveData();
        private readonly Dictionary<string, SpeciesRecord> _byId = new Dictionary<string, SpeciesRecord>();

        public event Action Changed;

        public JournalService(SpeciesDatabase database, string savePath)
        {
            _database = database;
            _savePath = savePath;
        }

        public SpeciesDatabase Database => _database;
        public IReadOnlyList<PhotoRecord> Photos => _data.photos;
        public int TotalPhotos => _data.photos.Count;
        public int DiscoveredCount => _byId.Count;
        public int TotalSpecies => _database != null ? _database.Count : 0;

        public bool IsDiscovered(string speciesId) => _byId.ContainsKey(speciesId);

        public SpeciesRecord GetRecord(string speciesId) =>
            speciesId != null && _byId.TryGetValue(speciesId, out var r) ? r : null;

        public PhotoRecord GetPhoto(string photoId)
        {
            if (string.IsNullOrEmpty(photoId)) return null;
            foreach (var p in _data.photos) if (p.photoId == photoId) return p;
            return null;
        }

        /// <summary>
        /// Identifies a species by observation (watched steadily through the camera). Returns true if this is a
        /// new journal entry; for known species it only notes the behaviour.
        /// </summary>
        public bool RecordObservation(string speciesId, string activity, string locationLabel)
        {
            if (_database == null || _database.Get(speciesId) == null) return false;
            if (_byId.TryGetValue(speciesId, out var existing))
            {
                NoteBehavior(existing, activity, true);
                return false;
            }

            var entry = NewRecord(speciesId, IdentificationMethod.Observation, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                locationLabel, activity, null);
            NoteBehavior(entry, activity, false);
            Save();
            Changed?.Invoke();
            return true;
        }

        private SpeciesRecord NewRecord(string speciesId, string method, string whenUtc, string location, string activity, string photoId)
        {
            var entry = new SpeciesRecord
            {
                speciesId = speciesId,
                identifiedBy = method,
                firstSightedUtc = whenUtc,
                firstSightingLocation = location,
                firstSightingActivity = activity,
                firstPhotoId = photoId,
            };
            _data.species.Add(entry);
            _byId[speciesId] = entry;
            return entry;
        }

        private void NoteBehavior(SpeciesRecord entry, string activity, bool saveIfChanged)
        {
            if (string.IsNullOrEmpty(activity)) return;
            if (entry.observedBehaviors == null) entry.observedBehaviors = new List<string>();
            if (entry.observedBehaviors.Contains(activity)) return;
            entry.observedBehaviors.Add(activity);
            if (saveIfChanged) Save();
        }

        /// <summary>
        /// Adds a photograph. <paramref name="record"/>.subjects should hold at most one entry per species
        /// (the best-scoring individual), which is what <see cref="Photography.PhotoAnalyzer"/> produces.
        /// </summary>
        public List<SpeciesIdentification> RegisterPhoto(PhotoRecord record)
        {
            var results = new List<SpeciesIdentification>();
            _data.photos.Add(record);

            foreach (var subject in record.subjects)
            {
                var species = _database != null ? _database.Get(subject.speciesId) : null;
                if (species == null) continue;

                bool isNew = !_byId.TryGetValue(subject.speciesId, out var entry);
                if (isNew)
                    entry = NewRecord(subject.speciesId, IdentificationMethod.Photograph, record.takenAtUtc,
                        record.locationLabel, subject.activity, record.photoId);
                if (string.IsNullOrEmpty(entry.firstPhotoId)) entry.firstPhotoId = record.photoId;
                NoteBehavior(entry, subject.activity, false);

                entry.photoCount++;
                // Better quality class wins; within a class, the higher internal rating.
                var quality = PhotoQualityEvaluator.Parse(subject.quality);
                var bestSoFar = PhotoQualityEvaluator.Parse(entry.bestQuality);
                bool newBest = string.IsNullOrEmpty(entry.bestPhotoId) || quality > bestSoFar ||
                               (quality == bestSoFar && subject.rating > entry.bestRating);
                if (newBest)
                {
                    entry.bestQuality = subject.quality;
                    entry.bestRating = subject.rating;
                    entry.bestPhotoId = record.photoId;
                }

                results.Add(new SpeciesIdentification
                {
                    species = species, subject = subject, isNewSpecies = isNew,
                    isFirstPhoto = entry.photoCount == 1, isNewBest = newBest && entry.photoCount > 1
                });
            }

            Save();
            Changed?.Invoke();
            return results;
        }

        public IReadOnlyList<NotebookRecord> Notebook => _data.notebook ?? (_data.notebook = new List<NotebookRecord>());
        public int NotebookCount => Notebook.Count;

        /// <summary>
        /// Records a macro photograph of a small subject in the field notebook (1.5). Returns true the first time the
        /// subject is found; keeps the best-quality photo ("Poor" &lt; "Good" &lt; "Excellent").
        /// </summary>
        public bool RecordNotebook(string subjectId, string displayName, string group, string quality, string photoId)
        {
            if (string.IsNullOrEmpty(subjectId)) return false;
            if (_data.notebook == null) _data.notebook = new List<NotebookRecord>();
            var entry = _data.notebook.Find(n => n.subjectId == subjectId);
            bool isNew = entry == null;
            if (isNew)
            {
                entry = new NotebookRecord
                {
                    subjectId = subjectId, displayName = displayName, group = group,
                    firstFoundUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture), bestQuality = quality, bestPhotoId = photoId,
                };
                _data.notebook.Add(entry);
            }
            entry.photoCount++;
            if (Rank(quality) > Rank(entry.bestQuality)) { entry.bestQuality = quality; entry.bestPhotoId = photoId; }
            Save();
            Changed?.Invoke();
            return isNew;
        }

        private static int Rank(string q) => q == "Excellent" ? 2 : q == "Good" ? 1 : 0;

        public void Load()
        {
            _data = new JournalSaveData();
            _byId.Clear();
            try
            {
                if (File.Exists(_savePath))
                {
                    var loaded = JsonUtility.FromJson<JournalSaveData>(File.ReadAllText(_savePath));
                    if (loaded != null) _data = Migrate(loaded);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WildLens] Could not read journal at {_savePath}: {e.Message}. Starting a new journal.");
                _data = new JournalSaveData();
            }

            foreach (var s in _data.species)
            {
                if (s == null || string.IsNullOrEmpty(s.speciesId)) continue;
                if (s.observedBehaviors == null) s.observedBehaviors = new List<string>();
                if (string.IsNullOrEmpty(s.identifiedBy)) s.identifiedBy = IdentificationMethod.Photograph;
                _byId[s.speciesId] = s;
            }
            if (_data.notebook == null) _data.notebook = new List<NotebookRecord>();
            Changed?.Invoke();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_savePath));
                File.WriteAllText(_savePath, JsonUtility.ToJson(_data, true));
            }
            catch (Exception e)
            {
                Debug.LogError($"[WildLens] Failed to save journal: {e.Message}");
            }
        }

        /// <summary>Clears all entries (photo files on disk are left alone).</summary>
        public void ResetJournal()
        {
            _data = new JournalSaveData();
            _byId.Clear();
            Save();
            Changed?.Invoke();
        }

        private static JournalSaveData Migrate(JournalSaveData data)
        {
            if (data.species == null) data.species = new List<SpeciesRecord>();
            if (data.photos == null) data.photos = new List<PhotoRecord>();

            if (data.version < 2)
            {
                // v1 stored 0-100 scores with labels Excellent/Good/Fair/Distant.
                foreach (var photo in data.photos)
                foreach (var subject in photo.subjects)
                {
                    subject.rating = subject.score / 100f;
                    subject.quality = PhotoQualityEvaluator.Label(PhotoQualityEvaluator.Parse(subject.quality));
                    subject.score = 0;
                }
                foreach (var record in data.species)
                {
                    record.bestRating = record.bestScore / 100f;
                    var best = data.photos.Find(p => p.photoId == record.bestPhotoId);
                    var subject = best?.subjects.Find(x => x.speciesId == record.speciesId);
                    record.bestQuality = subject != null ? subject.quality : PhotoQualityEvaluator.Label(PhotoQuality.Poor);
                    record.bestScore = 0;
                }
            }
            data.version = JournalSaveData.CurrentVersion;
            return data;
        }

        public static string FormatTimestamp(string isoUtc)
        {
            if (DateTime.TryParse(isoUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                return dt.ToLocalTime().ToString("MMM d, yyyy  h:mm tt", CultureInfo.InvariantCulture);
            return isoUtc ?? "";
        }
    }
}
