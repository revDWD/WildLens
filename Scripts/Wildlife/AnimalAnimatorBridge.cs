using UnityEngine;

namespace WildLens.Wildlife
{
    /// <summary>
    /// Feeds agent state into a Mecanim Animator on authored prefabs. Expected (optional) parameters:
    /// float "Speed" (m/s), int "Activity" (<see cref="Data.AnimalActivity"/> value), float "GaitPhase" (0-1).
    /// Missing parameters are skipped, so controllers can adopt them gradually.
    /// </summary>
    [RequireComponent(typeof(AnimalAgent))]
    public class AnimalAnimatorBridge : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int ActivityId = Animator.StringToHash("Activity");
        private static readonly int PhaseId = Animator.StringToHash("GaitPhase");

        private AnimalAgent _agent;
        private Animator _animator;
        private bool _hasSpeed, _hasActivity, _hasPhase;

        private void Start()
        {
            _agent = GetComponent<AnimalAgent>();
            _animator = GetComponentInChildren<Animator>();
            if (_animator == null) { enabled = false; return; }
            // 1.10: pack clips can carry animation events meant for the pack's own scripts (Living Birds' 'ResetHopInt'),
            // which WildLens strips from the model. With nothing to receive them Unity logs an error each time one fires.
            _animator.fireEvents = false;
            foreach (var p in _animator.parameters)
            {
                if (p.nameHash == SpeedId) _hasSpeed = true;
                else if (p.nameHash == ActivityId) _hasActivity = true;
                else if (p.nameHash == PhaseId) _hasPhase = true;
            }
        }

        private void Update()
        {
            if (_hasSpeed) _animator.SetFloat(SpeedId, _agent.Speed);
            if (_hasActivity) _animator.SetInteger(ActivityId, (int)_agent.Activity);
            if (_hasPhase) _animator.SetFloat(PhaseId, _agent.GaitPhase / (Mathf.PI * 2f));
        }
    }
}
