using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace SPPB.UI.Components
{
    /// <summary>
    /// Counter Display Component (for sit-stand test)
    /// Contains: frame, score text, score slots (with bottom-to-top fill animation)
    /// </summary>
    public class CounterDisplay : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private Image _frame;                  // Frame
        [SerializeField] private TextMeshProUGUI _counterText;  // Score calculation text (4/5, 2/5...)
        [SerializeField] private Image[] _scoreSlots;           // Score slots (5 Images)

        [Header("Score Slot Sprites")]
        [SerializeField] private Sprite _slotEmpty;             // Empty score slot
        [SerializeField] private Sprite _slotFull;              // Full score slot

        [Header("Animation Settings")]
        [SerializeField] private float _fillAnimationDuration = 0.3f;  // Fill animation duration
        [SerializeField] private float _scaleAnimationDuration = 0.25f; // Scale bounce animation duration
        [SerializeField] private float _scaleOvershoot = 1.3f;         // Scale maximum (1.3 = 30% larger)

        [Header("+1 Pop Effect")]
        [SerializeField] private Image _plusOneImage;                  // +1 image (must be pre-placed in scene)
        [SerializeField] private float _plusOneScaleDuration = 0.25f;  // Scale up animation duration
        [SerializeField] private float _plusOneSpringDuration = 0.6f;  // Spring animation duration
        [SerializeField] private float _plusOneSpringFrequency = 8f;   // Spring frequency
        [SerializeField] private float _plusOneSpringDamping = 3f;     // Spring damping
        [SerializeField] private float _plusOneSpringAmplitude = 0.15f; // Spring amplitude
        [SerializeField] private float _plusOneFadeDuration = 0.3f;    // Fade out animation duration
        [SerializeField] private Vector2 _plusOneOffset = new Vector2(50f, 30f); // Offset relative to score slot

        [Header("Settings")]
        [SerializeField] private int _targetCount = 5;          // Target count

        // Current count
        private int _currentCount = 0;

        // Previous count (for determining which slots need animation)
        private int _previousCount = 0;

        // Material instance for each score slot
        private Material[] _slotMaterials;

        // Shader property ID
        private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");

        // Animation coroutines
        private Coroutine[] _fillCoroutines;
        private Coroutine[] _scaleCoroutines;

        // Original scales
        private Vector3[] _originalScales;

        // +1 animation coroutine
        private Coroutine _plusOneCoroutine;

        // +1 original color
        private Color _plusOneOriginalColor;

        private void Awake()
        {
            InitializeMaterials();
            CacheOriginalScales();
            InitializePlusOneImage();
            Reset();  // 確保初始狀態是空的
        }

        private void OnDestroy()
        {
            // Clean up Material instances
            if (_slotMaterials != null)
            {
                foreach (var mat in _slotMaterials)
                {
                    if (mat != null)
                    {
                        Destroy(mat);
                    }
                }
            }
        }

        /// <summary>
        /// Initialize Materials
        /// </summary>
        private void InitializeMaterials()
        {
            if (_scoreSlots == null || _scoreSlots.Length == 0) return;

            _slotMaterials = new Material[_scoreSlots.Length];
            _fillCoroutines = new Coroutine[_scoreSlots.Length];
            _scaleCoroutines = new Coroutine[_scoreSlots.Length];

            for (int i = 0; i < _scoreSlots.Length; i++)
            {
                if (_scoreSlots[i] == null) continue;

                Material sourceMaterial = _scoreSlots[i].material;

                // Check if using SlotFillShader
                if (sourceMaterial != null && sourceMaterial.shader.name == "SPPB/SlotFill")
                {
                    // Create Material instance
                    _slotMaterials[i] = Instantiate(sourceMaterial);
                    _scoreSlots[i].material = _slotMaterials[i];

                    // Initially set to empty (fill amount = 0)
                    _slotMaterials[i].SetFloat(FillAmountId, 0f);
                }
            }
        }

        /// <summary>
        /// Cache original scales
        /// </summary>
        private void CacheOriginalScales()
        {
            if (_scoreSlots == null || _scoreSlots.Length == 0) return;

            _originalScales = new Vector3[_scoreSlots.Length];

            for (int i = 0; i < _scoreSlots.Length; i++)
            {
                if (_scoreSlots[i] != null)
                {
                    _originalScales[i] = _scoreSlots[i].transform.localScale;
                }
                else
                {
                    _originalScales[i] = Vector3.one;
                }
            }
        }

        /// <summary>
        /// Initialize +1 image
        /// </summary>
        private void InitializePlusOneImage()
        {
            if (_plusOneImage != null)
            {
                _plusOneOriginalColor = _plusOneImage.color;
                // Initially hidden
                _plusOneImage.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Set target count
        /// </summary>
        public void SetTargetCount(int target)
        {
            _targetCount = target;
        }

        /// <summary>
        /// Initialize counter
        /// </summary>
        public void Initialize(int targetCount = 5)
        {
            _targetCount = targetCount;
            Reset();
        }

        /// <summary>
        /// Reset counter
        /// </summary>
        public void Reset()
        {
            // Stop all animations
            StopAllAnimations();

            _previousCount = 0;
            _currentCount = 0;

            // Reset all score slots to empty
            if (_scoreSlots != null)
            {
                for (int i = 0; i < _scoreSlots.Length; i++)
                {
                    if (_scoreSlots[i] == null) continue;

                    // Set to empty slot sprite
                    if (_slotEmpty != null)
                    {
                        _scoreSlots[i].sprite = _slotEmpty;
                    }

                    // Reset fill amount to empty
                    if (_slotMaterials != null && i < _slotMaterials.Length && _slotMaterials[i] != null)
                    {
                        _slotMaterials[i].SetFloat(FillAmountId, 0f);
                    }

                    // Reset scale
                    if (_originalScales != null && i < _originalScales.Length)
                    {
                        _scoreSlots[i].transform.localScale = _originalScales[i];
                    }
                }
            }

            UpdateCounterText();
        }

        /// <summary>
        /// Set current count
        /// </summary>
        public void SetCount(int count)
        {
            int oldCount = _currentCount;
            _currentCount = Mathf.Clamp(count, 0, _targetCount);

            // Update text
            UpdateCounterText();

            // Update score slots (with animation)
            UpdateScoreSlotsAnimated(oldCount, _currentCount);
        }

        /// <summary>
        /// Increment count
        /// </summary>
        /// <returns>Whether target is reached</returns>
        public bool Increment()
        {
            if (_currentCount < _targetCount)
            {
                int oldCount = _currentCount;
                _currentCount++;

                // Update text
                UpdateCounterText();

                // Only animate newly added slots
                UpdateScoreSlotsAnimated(oldCount, _currentCount);
            }

            return _currentCount >= _targetCount;
        }

        /// <summary>
        /// Update score text
        /// </summary>
        private void UpdateCounterText()
        {
            if (_counterText != null)
            {
                _counterText.text = $"{_currentCount}/{_targetCount}";
            }
        }

        /// <summary>
        /// Update score slots display (with animation)
        /// </summary>
        private void UpdateScoreSlotsAnimated(int oldCount, int newCount)
        {
            if (_scoreSlots == null) return;

            for (int i = 0; i < _scoreSlots.Length; i++)
            {
                if (_scoreSlots[i] == null) continue;

                bool shouldBeFull = i < newCount;
                bool wasFull = i < oldCount;

                if (shouldBeFull && !wasFull)
                {
                    // This slot needs to change from empty to full, play fill animation
                    PlayFillAnimation(i);
                }
                else if (!shouldBeFull && wasFull)
                {
                    // This slot needs to change from full to empty, set directly
                    SetSlotEmpty(i);
                }
            }
        }

        /// <summary>
        /// Play fill animation
        /// </summary>
        private void PlayFillAnimation(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _scoreSlots.Length) return;
            if (_scoreSlots[slotIndex] == null) return;

            // Stop existing animations for this slot
            StopSlotAnimations(slotIndex);

            // Switch to full slot sprite first
            if (_slotFull != null)
            {
                _scoreSlots[slotIndex].sprite = _slotFull;
            }

            // If Material exists, play fill animation
            if (_slotMaterials != null && slotIndex < _slotMaterials.Length && _slotMaterials[slotIndex] != null)
            {
                _fillCoroutines[slotIndex] = StartCoroutine(FillAnimationCoroutine(slotIndex));
            }

            // Play scale bounce animation
            _scaleCoroutines[slotIndex] = StartCoroutine(ScaleAnimationCoroutine(slotIndex));

            // Play +1 pop animation
            PlayPlusOneAnimation(slotIndex);
        }

        /// <summary>
        /// Stop all animations for specified slot
        /// </summary>
        private void StopSlotAnimations(int slotIndex)
        {
            if (_fillCoroutines != null && slotIndex < _fillCoroutines.Length && _fillCoroutines[slotIndex] != null)
            {
                StopCoroutine(_fillCoroutines[slotIndex]);
                _fillCoroutines[slotIndex] = null;
            }

            if (_scaleCoroutines != null && slotIndex < _scaleCoroutines.Length && _scaleCoroutines[slotIndex] != null)
            {
                StopCoroutine(_scaleCoroutines[slotIndex]);
                _scaleCoroutines[slotIndex] = null;

                // Reset scale
                if (_originalScales != null && slotIndex < _originalScales.Length && _scoreSlots[slotIndex] != null)
                {
                    _scoreSlots[slotIndex].transform.localScale = _originalScales[slotIndex];
                }
            }
        }

        /// <summary>
        /// Fill animation coroutine
        /// </summary>
        private IEnumerator FillAnimationCoroutine(int slotIndex)
        {
            Material mat = _slotMaterials[slotIndex];

            // Fill from 0 to 1
            float elapsed = 0f;
            mat.SetFloat(FillAmountId, 0f);

            while (elapsed < _fillAnimationDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / _fillAnimationDuration);

                // Use easeOutBack effect for more elastic animation
                float easedProgress = EaseOutBack(progress);
                mat.SetFloat(FillAmountId, easedProgress);

                yield return null;
            }

            // Ensure final value is 1
            mat.SetFloat(FillAmountId, 1f);
            _fillCoroutines[slotIndex] = null;
        }

        /// <summary>
        /// Scale bounce animation coroutine
        /// </summary>
        private IEnumerator ScaleAnimationCoroutine(int slotIndex)
        {
            if (_originalScales == null || slotIndex >= _originalScales.Length) yield break;

            Transform slotTransform = _scoreSlots[slotIndex].transform;
            Vector3 originalScale = _originalScales[slotIndex];
            Vector3 targetScale = originalScale * _scaleOvershoot;

            float elapsed = 0f;
            float halfDuration = _scaleAnimationDuration * 0.4f; // Scale up is 40%
            float secondHalfDuration = _scaleAnimationDuration * 0.6f; // Scale back is 60%

            // Phase 1: Quick scale up
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / halfDuration);

                // Use easeOutQuad for more impactful scale up
                float easedProgress = 1f - (1f - progress) * (1f - progress);
                slotTransform.localScale = Vector3.Lerp(originalScale, targetScale, easedProgress);

                yield return null;
            }

            // Phase 2: Elastic scale back
            elapsed = 0f;
            while (elapsed < secondHalfDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / secondHalfDuration);

                // Use easeOutElastic for elastic scale back
                float easedProgress = EaseOutElastic(progress);
                slotTransform.localScale = Vector3.Lerp(targetScale, originalScale, easedProgress);

                yield return null;
            }

            // Ensure final value
            slotTransform.localScale = originalScale;
            _scaleCoroutines[slotIndex] = null;
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
        /// EaseOutElastic easing function
        /// </summary>
        private float EaseOutElastic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;

            const float c4 = (2f * Mathf.PI) / 3f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
        }

        /// <summary>
        /// Play +1 pop animation
        /// </summary>
        private void PlayPlusOneAnimation(int slotIndex)
        {
            if (_plusOneImage == null) return;
            if (_scoreSlots == null || slotIndex >= _scoreSlots.Length || _scoreSlots[slotIndex] == null) return;

            // Stop existing animation
            if (_plusOneCoroutine != null)
            {
                StopCoroutine(_plusOneCoroutine);
            }

            _plusOneCoroutine = StartCoroutine(PlusOneAnimationCoroutine(slotIndex));
        }

        /// <summary>
        /// +1 pop animation coroutine (with spring effect)
        /// </summary>
        private IEnumerator PlusOneAnimationCoroutine(int slotIndex)
        {
            // Position next to score slot
            RectTransform slotRect = _scoreSlots[slotIndex].GetComponent<RectTransform>();
            RectTransform plusOneRect = _plusOneImage.GetComponent<RectTransform>();

            if (slotRect != null && plusOneRect != null)
            {
                // Move +1 to score slot position + offset
                plusOneRect.position = slotRect.position + new Vector3(_plusOneOffset.x, _plusOneOffset.y, 0);
            }

            // Initial state: scale to 0 and fully opaque
            _plusOneImage.transform.localScale = Vector3.zero;
            Color startColor = _plusOneOriginalColor;
            startColor.a = 1f;
            _plusOneImage.color = startColor;

            // Show
            _plusOneImage.gameObject.SetActive(true);

            // Phase 1: Scale from 0 to 1 (EaseOutBack elastic effect)
            float elapsed = 0f;
            while (elapsed < _plusOneScaleDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / _plusOneScaleDuration);

                // Use EaseOutBack for elastic scale up
                float easedProgress = EaseOutBack(progress);
                _plusOneImage.transform.localScale = Vector3.one * easedProgress;

                yield return null;
            }

            // Ensure scale is 1
            _plusOneImage.transform.localScale = Vector3.one;

            // Phase 2: Spring animation (damped oscillation)
            elapsed = 0f;
            while (elapsed < _plusOneSpringDuration)
            {
                elapsed += Time.deltaTime;
                float springT = elapsed / _plusOneSpringDuration;

                // Damped oscillation formula: A * e^(-d*t) * sin(f*t*2π)
                float damping = Mathf.Exp(-_plusOneSpringDamping * springT);
                float oscillation = Mathf.Sin(springT * _plusOneSpringFrequency * Mathf.PI * 2f);
                float scaleOffset = oscillation * damping * _plusOneSpringAmplitude;

                _plusOneImage.transform.localScale = Vector3.one * (1f + scaleOffset);

                yield return null;
            }

            // Ensure final scale is 1
            _plusOneImage.transform.localScale = Vector3.one;

            // Phase 3: Fade out
            elapsed = 0f;
            while (elapsed < _plusOneFadeDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / _plusOneFadeDuration);

                // Use EaseInQuad for more natural fade out
                float easedProgress = progress * progress;

                Color fadeColor = _plusOneOriginalColor;
                fadeColor.a = 1f - easedProgress;
                _plusOneImage.color = fadeColor;

                yield return null;
            }

            // Hide
            _plusOneImage.gameObject.SetActive(false);

            // Reset state
            _plusOneImage.color = _plusOneOriginalColor;
            _plusOneImage.transform.localScale = Vector3.one;

            _plusOneCoroutine = null;
        }

        /// <summary>
        /// Set slot to empty
        /// </summary>
        private void SetSlotEmpty(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _scoreSlots.Length) return;
            if (_scoreSlots[slotIndex] == null) return;

            // Stop all animations for this slot
            StopSlotAnimations(slotIndex);

            // Set to empty slot sprite
            if (_slotEmpty != null)
            {
                _scoreSlots[slotIndex].sprite = _slotEmpty;
            }

            // Reset fill amount to empty
            if (_slotMaterials != null && slotIndex < _slotMaterials.Length && _slotMaterials[slotIndex] != null)
            {
                _slotMaterials[slotIndex].SetFloat(FillAmountId, 0f);
            }
        }

        /// <summary>
        /// Stop all animations
        /// </summary>
        private void StopAllAnimations()
        {
            if (_scoreSlots == null) return;

            for (int i = 0; i < _scoreSlots.Length; i++)
            {
                StopSlotAnimations(i);
            }
        }

        /// <summary>
        /// Show counter
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Hide counter
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Get current count
        /// </summary>
        public int GetCurrentCount()
        {
            return _currentCount;
        }

        /// <summary>
        /// Get target count
        /// </summary>
        public int GetTargetCount()
        {
            return _targetCount;
        }

        /// <summary>
        /// Check if target is reached
        /// </summary>
        public bool IsComplete()
        {
            return _currentCount >= _targetCount;
        }

        /// <summary>
        /// Get progress (0-1)
        /// </summary>
        public float GetProgress()
        {
            if (_targetCount <= 0) return 0f;
            return (float)_currentCount / _targetCount;
        }

        /// <summary>
        /// Set score slot sprites
        /// </summary>
        public void SetSlotSprites(Sprite empty, Sprite full)
        {
            _slotEmpty = empty;
            _slotFull = full;
        }

        /// <summary>
        /// Set frame color
        /// </summary>
        public void SetFrameColor(Color color)
        {
            if (_frame != null)
            {
                _frame.color = color;
            }
        }

        /// <summary>
        /// Set text color
        /// </summary>
        public void SetTextColor(Color color)
        {
            if (_counterText != null)
            {
                _counterText.color = color;
            }
        }
    }
}
