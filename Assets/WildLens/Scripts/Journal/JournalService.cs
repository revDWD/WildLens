using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using WildLens.Data;

namespace WildLens.Journal
{
    /// <summary>Result for one species in a newly registered photo.</summary>
    public struct SpeciesIdentification
    {
        public SpeciesDefinition species;
        public PhotoSubject subject;
        public bool isNewSpecies;
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
                {
                    entry = new SpeciesRecord
                    {
                        speciesId = subject.speciesId,
                        firstSightedUtc = record.takenAtUtc,
                        firstSightingLocation = record.locationLabel,
                        firstSightingActivity = subject.activity,
                        firstPhotoId = record.photoId,
                    };
                    _data.species.Add(entry);
                    _byId[entry.speciesId] = entry;
                }

                entry.photoCount++;
                bool newBest = subject.score > entry.bestScore || string.IsNullOrEmpty(entry.bestPhotoId);
                if (newBest)
                {
                    entry.bestScore = subject.score;
                    entry.bestPhotoId = record.photoId;
                }

                results.Add(new SpeciesIdentification
                {
                    species = species, subject = subject, isNewSpecies = isNew, isNewBest = newBest && !isNew
                });
            }

            Save();
            Changed?.Invoke();
            return results;
        }

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
                if (s != null && !string.IsNullOrEmpty(s.speciesId)) _byId[s.speciesId] = s;
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
            // Version 1 is the first format; future migrations go here.
            if (data.species == null) data.species = new List<SpeciesRecord>();
            if (data.photos == null) data.photos = new List<PhotoRecord>();
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
