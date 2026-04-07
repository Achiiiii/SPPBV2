using UnityEngine;

namespace SPPB.UI.Components
{
    /// <summary>
    /// Breathing Animation Component - Gives UI elements a pulsing scale effect
    /// </summary>
    public class BreathingAnimator : MonoBehaviour
    {
        [Header("Animation Settings")]
        [SerializeField] private float _breathSpeed = 1.5f;         // Breathing speed
        [SerializeField] private float _minScale = 0.95f;           // Minimum scale
        [SerializeField] private float _maxScale = 1.05f;           // Maximum scale
        [SerializeField] private bool _playOnEnable = true;         // Auto play on enable

        // Original scale
        private Vector3 _originalScale;

        // Is playing
        private bool _isPlaying = false;

        // Animation time
        private float _animTime = 0f;

        private void Awake()
        {
            _originalScale = transform.localScale;
        }

        private void OnEnable()
        {
            if (_playOnEnable)
            {
                Play();
            }
        }

        private void OnDisable()
        {
            Stop();
        }

        private void Update()
        {
            if (!_isPlaying) return;

            _animTime += Time.deltaTime * _breathSpeed;

            // Use sin function to produce 0~1 oscillation
            float t = (Mathf.Sin(_animTime * Mathf.PI * 2f) + 1f) * 0.5f;

            // Interpolate between minScale and maxScale
            float currentScale = Mathf.Lerp(_minScale, _maxScale, t);

            transform.localScale = _originalScale * currentScale;
        }

        /// <summary>
        /// Start playing breathing animation
        /// </summary>
        public void Play()
        {
            _isPlaying = true;
            _animTime = 0f;
        }

        /// <summary>
        /// Stop breathing animation and reset scale
        /// </summary>
        public void Stop()
        {
            _isPlaying = false;
            transform.localScale = _originalScale;
        }

        /// <summary>
        /// Pause playback (keep current scale)
        /// </summary>
        public void Pause()
        {
            _isPlaying = false;
        }

        /// <summary>
        /// Resume playback
        /// </summary>
        public void Resume()
        {
            _isPlaying = true;
        }

        /// <summary>
        /// Set breathing speed
        /// </summary>
        public void SetSpeed(float speed)
        {
            _breathSpeed = speed;
        }

        /// <summary>
        /// Set scale range
        /// </summary>
        public void SetScaleRange(float min, float max)
        {
            _minScale = min;
            _maxScale = max;
        }

        /// <summary>
        /// Check if animation is playing
        /// </summary>
        public bool IsPlaying => _isPlaying;
    }
}
