using System;
using UnityEngine;
using WildLens.Data;
using WildLens.Photography;
using WildLens.Wildlife;

namespace WildLens.Observation
{
    /// <summary>
    /// Identifies species by watching. In camera mode, keep an unknown animal tracked (see
    /// <see cref="FramingAssistant"/>), reasonably central and large enough to make out, for its species'
    /// <c>observationTime</c> and it is identified: the journal gains an entry even without a photograph.
    ///
    /// Brief interruptions (the animal steps behind a stem, the view drifts) are forgiven for a moment, and
    /// progress drains rather than resetting. Watching known species records the behaviours seen, as data for
    /// future behaviour photography and field notes.
    /// </summary>
    public class ObservationTracker : MonoBehaviour
    {
        [Tooltip("Subject must be at least this many times the minimum identifiable size.")]
        [SerializeField] private float fillFactor = 1.5f;
        [Tooltip("Subject centre must be within this per-axis offset of frame centre (0 centre … 1 edge).")]
        [SerializeField, Range(0.1f, 1f)] private float maxCenterOffset = 0.5f;
        [SerializeField] private float graceSeconds = 0.5f;
        [SerializeField] private float behaviourNoteInterval = 1f;

        private FramingAssistant _framing;
        private SpeciesDefinition _species;
        private float _progress;
        private float _lostFor;
        private float _behaviourTimer;

        /// <summary>Species currently being watched (null if none).</summary>
        public SpeciesDefinition CurrentSpecies => _species;
        /// <summary>0-1 progress toward identification (1 for species already in the journal).</summary>
        public float Progress => _progress;
        public bool IsObserving => _species != null && _lostFor <= 0f;

        /// <summary>Raised when a species is identified for the first time by observation.</summary>
        public event Action<SpeciesDefinition> SpeciesIdentified;

        private void Awake() => _framing = GetComponent<FramingAssistant>();

        private void Update()
        {
            var game = WildLensGame.Instance;
            if (game == null || _framing == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (!Watching(out var target))
            {
                _lostFor += dt;
                _progress = Mathf.MoveTowards(_progress, 0f, dt * 0.5f);
                if (_lostFor > graceSeconds) _species = null;
                return;
            }

            var species = target.Species;
            if (species != _species)
            {
                _species = species;
                _progress = 0f;
                _behaviourTimer = 0f;
            }
            _lostFor = 0f;

            var journal = game.Journal;
            if (journal.IsDiscovered(species.speciesId))
            {
                _progress = 1f;
                _behaviourTimer -= dt;
                if (_behaviourTimer <= 0f)
                {
                    _behaviourTimer = behaviourNoteInterval;
                    journal.RecordObservation(species.speciesId, target.CurrentActivity.ToString(), Location(target));
                }
                return;
            }

            _progress += dt / Mathf.Max(0.1f, species.observationTime);
            if (_progress < 1f) return;
            _progress = 1f;
            if (journal.RecordObservation(species.speciesId, target.CurrentActivity.ToString(), Location(target)))
                SpeciesIdentified?.Invoke(species);
        }

        private bool Watching(out Photographable target)
        {
            target = null;
            if (!_framing.HasSubject) return false;
            var s = _framing.Subject;
            if (s.target == null || s.target.Species == null || !s.identifiable) return false;
            var m = s.measurements;
            if (m.centerOffset > maxCenterOffset || m.NormalizedFill < PhotoQualityEvaluator.IdentifyFill * fillFactor) return false;
            target = s.target;
            return true;
        }

        private static string Location(Photographable target)
        {
            var trail = WildlifeWorld.Instance != null ? WildlifeWorld.Instance.Trail : null;
            return trail != null ? trail.DescribeLocation(target.transform.position) : "";
        }
    }
}
