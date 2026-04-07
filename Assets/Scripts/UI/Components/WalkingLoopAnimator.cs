using UnityEngine;

namespace SPPB.UI.Components
{
    /// <summary>
    /// Walking Loop Animation Controller
    /// Used for walking test intro person icon and arrow icon animations
    /// </summary>
    public class WalkingLoopAnimator : MonoBehaviour
    {
        [Header("Animation Targets")]
        [SerializeField] private RectTransform _personIcon;
        [SerializeField] private RectTransform _arrowIcon;

        [Header("Movement Settings")]
        [SerializeField] private float _startX = 0f;
        [SerializeField] private float _distance = 200f;
        [SerializeField] private float _moveDuration = 2f;

        [Header("Stagger Settings")]
        [SerializeField] private float _arrowDelay = 0.3f;

        private bool _isPlaying;
        private float _personTime;
        private float _arrowTime;
        private Vector2 _personOriginalPos;
        private Vector2 _arrowOriginalPos;

        private void Awake()
        {
            // Save original positions
            if (_personIcon != null)
            {
                _personOriginalPos = _personIcon.anchoredPosition;
            }
            if (_arrowIcon != null)
            {
                _arrowOriginalPos = _arrowIcon.anchoredPosition;
            }
        }

        private void Update()
        {
            if (!_isPlaying) return;

            // Update person icon animation
            if (_personIcon != null)
            {
                _personTime += Time.unscaledDeltaTime;
                UpdateIconPosition(_personIcon, _personTime, _personOriginalPos);
            }

            // Update arrow icon animation (delayed start)
            if (_arrowIcon != null)
            {
                _arrowTime += Time.unscaledDeltaTime;
                float effectiveArrowTime = _arrowTime - _arrowDelay;
                if (effectiveArrowTime >= 0)
                {
                    UpdateIconPosition(_arrowIcon, effectiveArrowTime, _arrowOriginalPos);
                }
            }
        }

        private void UpdateIconPosition(RectTransform icon, float time, Vector2 originalPos)
        {
            // Calculate cycle time
            float cycleTime = time % _moveDuration;
            float t = cycleTime / _moveDuration;

            // Use ease in/out
            float easedT = EaseInOutQuad(t);

            // Calculate X position
            float newX = _startX + easedT * _distance;

            // Apply new position (only change X, keep current Y)
            Vector2 newPos = icon.anchoredPosition;
            newPos.x = newX;
            icon.anchoredPosition = newPos;
        }

        /// <summary>
        /// Start loop playback
        /// </summary>
        public void Play()
        {
            _isPlaying = true;
            _personTime = 0f;
            _arrowTime = 0f;

            // Set initial positions (only change X, keep current Y)
            if (_personIcon != null)
            {
                Vector2 pos = _personIcon.anchoredPosition;
                pos.x = _startX;
                _personIcon.anchoredPosition = pos;
            }

            if (_arrowIcon != null)
            {
                Vector2 pos = _arrowIcon.anchoredPosition;
                pos.x = _startX;
                _arrowIcon.anchoredPosition = pos;
            }
        }

        /// <summary>
        /// Stop and reset to start position
        /// </summary>
        public void Stop()
        {
            _isPlaying = false;
            _personTime = 0f;
            _arrowTime = 0f;

            // Reset to start position (only change X, keep current Y)
            if (_personIcon != null)
            {
                Vector2 pos = _personIcon.anchoredPosition;
                pos.x = _startX;
                _personIcon.anchoredPosition = pos;
            }

            if (_arrowIcon != null)
            {
                Vector2 pos = _arrowIcon.anchoredPosition;
                pos.x = _startX;
                _arrowIcon.anchoredPosition = pos;
            }
        }

        /// <summary>
        /// Reset to original position (position saved during Awake)
        /// </summary>
        public void ResetToOriginal()
        {
            _isPlaying = false;
            _personTime = 0f;
            _arrowTime = 0f;

            if (_personIcon != null)
            {
                _personIcon.anchoredPosition = _personOriginalPos;
            }

            if (_arrowIcon != null)
            {
                _arrowIcon.anchoredPosition = _arrowOriginalPos;
            }
        }

        /// <summary>
        /// EaseInOutQuad easing function - ease in/out
        /// </summary>
        private float EaseInOutQuad(float t)
        {
            return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
        }

        /// <summary>
        /// Check if animation is playing
        /// </summary>
        public bool IsPlaying => _isPlaying;

        /// <summary>
        /// Set starting X coordinate
        /// </summary>
        public void SetStartX(float x)
        {
            _startX = x;
        }

        /// <summary>
        /// Set movement distance
        /// </summary>
        public void SetDistance(float distance)
        {
            _distance = distance;
        }
    }
}
