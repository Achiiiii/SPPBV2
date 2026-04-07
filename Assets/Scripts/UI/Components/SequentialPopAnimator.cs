using UnityEngine;

namespace SPPB.UI.Components
{
    /// <summary>
    /// Sequential Pop Animation Controller
    /// Used for balance test intro and sit-stand test intro icon animations
    /// </summary>
    public class SequentialPopAnimator : MonoBehaviour
    {
        [Header("Animation Targets")]
        [SerializeField] private RectTransform[] _targets;

        [Header("Animation Settings")]
        [SerializeField] private float _delayBetween = 0.3f;
        [SerializeField] private float _popDuration = 0.25f;
        [SerializeField] private float _springDuration = 0.6f;
        [SerializeField] private float _springFrequency = 8f;
        [SerializeField] private float _springDamping = 3f;
        [SerializeField] private float _springAmplitude = 0.15f;

        private enum AnimationState
        {
            Idle,
            Playing,
            Complete
        }

        private class TargetState
        {
            public Vector3 OriginalScale;
            public float StartTime;
            public bool Started;
            public bool Completed;
        }

        private AnimationState _state = AnimationState.Idle;
        private TargetState[] _targetStates;
        private float _animationTime;

        private void Awake()
        {
            InitializeTargetStates();
        }

        private void InitializeTargetStates()
        {
            if (_targets == null || _targets.Length == 0) return;

            _targetStates = new TargetState[_targets.Length];
            for (int i = 0; i < _targets.Length; i++)
            {
                _targetStates[i] = new TargetState
                {
                    OriginalScale = _targets[i] != null ? _targets[i].localScale : Vector3.one,
                    StartTime = i * _delayBetween,
                    Started = false,
                    Completed = false
                };
            }
        }

        private void Update()
        {
            if (_state != AnimationState.Playing) return;

            _animationTime += Time.unscaledDeltaTime;

            bool allComplete = true;

            for (int i = 0; i < _targets.Length; i++)
            {
                if (_targets[i] == null || _targetStates[i] == null) continue;

                var targetState = _targetStates[i];

                if (targetState.Completed)
                    continue;

                allComplete = false;

                if (_animationTime < targetState.StartTime)
                    continue;

                if (!targetState.Started)
                {
                    targetState.Started = true;
                    _targets[i].gameObject.SetActive(true);
                }

                float localTime = _animationTime - targetState.StartTime;
                UpdateTargetAnimation(i, localTime);
            }

            if (allComplete)
            {
                _state = AnimationState.Complete;
            }
        }

        private void UpdateTargetAnimation(int index, float localTime)
        {
            var target = _targets[index];
            var targetState = _targetStates[index];

            if (localTime < _popDuration)
            {
                // ScaleUp phase - EaseOutBack
                float t = localTime / _popDuration;
                float easedScale = EaseOutBack(t);
                target.localScale = targetState.OriginalScale * easedScale;
            }
            else if (localTime < _popDuration + _springDuration)
            {
                // Spring phase - damped oscillation
                float springTime = localTime - _popDuration;
                float springT = springTime / _springDuration;

                float damping = Mathf.Exp(-_springDamping * springT);
                float oscillation = Mathf.Sin(springT * _springFrequency * Mathf.PI * 2f);
                float scaleOffset = oscillation * damping * _springAmplitude;

                target.localScale = targetState.OriginalScale * (1f + scaleOffset);
            }
            else
            {
                // Animation complete for this target
                target.localScale = targetState.OriginalScale;
                targetState.Completed = true;
            }
        }

        /// <summary>
        /// Start playing animation
        /// </summary>
        public void Play()
        {
            if (_targets == null || _targets.Length == 0) return;

            // Reinitialize state
            InitializeTargetStates();

            // Hide all targets and set initial scale to 0
            for (int i = 0; i < _targets.Length; i++)
            {
                if (_targets[i] != null)
                {
                    _targets[i].localScale = Vector3.zero;
                    _targets[i].gameObject.SetActive(false);
                }
            }

            _animationTime = 0f;
            _state = AnimationState.Playing;
        }

        /// <summary>
        /// Reset to initial state
        /// </summary>
        public void Reset()
        {
            _state = AnimationState.Idle;
            _animationTime = 0f;

            if (_targets == null || _targetStates == null) return;

            for (int i = 0; i < _targets.Length; i++)
            {
                if (_targets[i] != null && _targetStates[i] != null)
                {
                    _targets[i].localScale = _targetStates[i].OriginalScale;
                    _targets[i].gameObject.SetActive(true);
                    _targetStates[i].Started = false;
                    _targetStates[i].Completed = false;
                }
            }
        }

        /// <summary>
        /// EaseOutBack easing function
        /// </summary>
        private float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        /// <summary>
        /// Check if animation is playing
        /// </summary>
        public bool IsPlaying => _state == AnimationState.Playing;

        /// <summary>
        /// Check if animation is complete
        /// </summary>
        public bool IsComplete => _state == AnimationState.Complete;
    }
}
