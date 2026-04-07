using UnityEngine;
using UnityEngine.UI;

namespace SPPB.UI.Components
{
    /// <summary>
    /// Hint Image Effect Controller
    /// Manages Shader Material animation effects, spring oscillation, fade out
    /// </summary>
    public class HintEffectController : MonoBehaviour
    {
        public enum EffectMode
        {
            Countdown,  // Countdown hint (3, 2, 1, GO)
            Complete    // Complete hint (test complete)
        }

        [Header("Material Settings")]
        [SerializeField] private Material _hintEffectMaterial;

        [Header("Animation Settings")]
        [SerializeField] private float _scaleAnimationDuration = 0.25f;

        [Header("Spring Animation Settings")]
        [SerializeField] private float _springDuration = 1.0f;
        [SerializeField] private float _springFrequency = 8f;
        [SerializeField] private float _springDamping = 3f;
        [SerializeField] private float _springAmplitude = 0.2f;

        [Header("Fade Out Settings")]
        [SerializeField] private float _fadeOutDuration = 0.3f;

        [Header("Countdown Mode Parameters")]
        [SerializeField] private float _countdownPulseIntensity = 0.2f;

        [Header("Complete Mode Parameters")]
        [SerializeField] private float _completePulseIntensity = 0.35f;

        // Shader property IDs
        private static readonly int PropEffectProgress = Shader.PropertyToID("_EffectProgress");
        private static readonly int PropIntensityMode = Shader.PropertyToID("_IntensityMode");
        private static readonly int PropPulseIntensity = Shader.PropertyToID("_PulseIntensity");

        // Runtime state
        private Material _materialInstance;
        private Image _targetImage;
        private CanvasGroup _canvasGroup;
        private bool _isAnimating;
        private float _animationTimer;
        private EffectMode _currentMode;
        private Vector3 _originalScale;
        private Material _originalMaterial;
        private float _originalAlpha;

        // Animation phases
        private enum AnimationPhase
        {
            ScaleUp,
            Spring,
            FadeOut,
            Complete
        }
        private AnimationPhase _currentPhase;

        private void Awake()
        {
            if (_hintEffectMaterial != null)
            {
                _materialInstance = new Material(_hintEffectMaterial);
            }
        }

        private void OnDestroy()
        {
            if (_materialInstance != null)
            {
                Destroy(_materialInstance);
                _materialInstance = null;
            }
        }

        private void Update()
        {
            if (!_isAnimating || _targetImage == null) return;

            _animationTimer += Time.unscaledDeltaTime;

            switch (_currentPhase)
            {
                case AnimationPhase.ScaleUp:
                    UpdateScaleUpPhase();
                    break;
                case AnimationPhase.Spring:
                    UpdateSpringPhase();
                    break;
                case AnimationPhase.FadeOut:
                    UpdateFadeOutPhase();
                    break;
                case AnimationPhase.Complete:
                    break;
            }
        }

        private void UpdateScaleUpPhase()
        {
            float t = Mathf.Clamp01(_animationTimer / _scaleAnimationDuration);
            float easedScale = EaseOutBack(t);
            _targetImage.transform.localScale = _originalScale * easedScale;

            // Shader effect fade in
            if (_materialInstance != null)
            {
                _materialInstance.SetFloat(PropEffectProgress, t);
            }

            if (t >= 1.0f)
            {
                _currentPhase = AnimationPhase.Spring;
                _targetImage.transform.localScale = _originalScale;
            }
        }

        private void UpdateSpringPhase()
        {
            float springTime = _animationTimer - _scaleAnimationDuration;
            float springT = springTime / _springDuration;

            if (springT >= 1.0f)
            {
                // Spring animation complete, enter fade out phase
                _currentPhase = AnimationPhase.FadeOut;
                _targetImage.transform.localScale = _originalScale;
                return;
            }

            // Damped oscillation formula: A * e^(-d*t) * sin(f*t*2π)
            float damping = Mathf.Exp(-_springDamping * springT);
            float oscillation = Mathf.Sin(springT * _springFrequency * Mathf.PI * 2f);
            float scaleOffset = oscillation * damping * _springAmplitude;

            _targetImage.transform.localScale = _originalScale * (1f + scaleOffset);
        }

        private void UpdateFadeOutPhase()
        {
            float fadeTime = _animationTimer - _scaleAnimationDuration - _springDuration;
            float fadeT = Mathf.Clamp01(fadeTime / _fadeOutDuration);

            // Fade out (using CanvasGroup or Image color)
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = Mathf.Lerp(_originalAlpha, 0f, fadeT);
            }
            else
            {
                Color c = _targetImage.color;
                c.a = Mathf.Lerp(1f, 0f, fadeT);
                _targetImage.color = c;
            }

            // Shader effect fade out
            if (_materialInstance != null)
            {
                _materialInstance.SetFloat(PropEffectProgress, 1f - fadeT);
            }

            if (fadeT >= 1.0f)
            {
                _currentPhase = AnimationPhase.Complete;
                _isAnimating = false;

                // Hide object
                _targetImage.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Play effect on the specified Image
        /// </summary>
        public void PlayEffect(Image targetImage, EffectMode mode)
        {
            if (targetImage == null || _materialInstance == null) return;

            _targetImage = targetImage;
            _currentMode = mode;

            // Save original material
            _originalMaterial = targetImage.material;

            // Save original scale - if 0 or near 0, use Vector3.one
            Vector3 currentScale = targetImage.transform.localScale;
            if (currentScale.magnitude < 0.01f)
            {
                _originalScale = Vector3.one;
            }
            else
            {
                _originalScale = currentScale;
            }

            // Get CanvasGroup (if available)
            _canvasGroup = targetImage.GetComponent<CanvasGroup>();
            _originalAlpha = _canvasGroup != null ? _canvasGroup.alpha : 1f;

            // Ensure object is visible
            targetImage.gameObject.SetActive(true);

            // Apply effect material
            targetImage.material = _materialInstance;

            // Configure Shader parameters
            ConfigureShaderForMode(mode);

            // Start animation
            _animationTimer = 0f;
            _isAnimating = true;
            _currentPhase = AnimationPhase.ScaleUp;

            // Initial state: scale to 0
            _materialInstance.SetFloat(PropEffectProgress, 0f);
            targetImage.transform.localScale = Vector3.zero;

            // Ensure fully visible
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
            }
            else
            {
                Color c = targetImage.color;
                c.a = 1f;
                targetImage.color = c;
            }
        }

        /// <summary>
        /// Stop effect and restore original state
        /// </summary>
        public void StopEffect()
        {
            if (_targetImage != null)
            {
                _targetImage.material = _originalMaterial;
                _targetImage.transform.localScale = _originalScale;

                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = _originalAlpha;
                }
                else
                {
                    Color c = _targetImage.color;
                    c.a = 1f;
                    _targetImage.color = c;
                }
            }

            _isAnimating = false;
            _currentPhase = AnimationPhase.Complete;
            _targetImage = null;
            _canvasGroup = null;
        }

        /// <summary>
        /// Configure Shader parameters based on mode
        /// </summary>
        private void ConfigureShaderForMode(EffectMode mode)
        {
            if (_materialInstance == null) return;

            if (mode == EffectMode.Countdown)
            {
                _materialInstance.SetFloat(PropIntensityMode, 0f);
                _materialInstance.SetFloat(PropPulseIntensity, _countdownPulseIntensity);
            }
            else // Complete
            {
                _materialInstance.SetFloat(PropIntensityMode, 1f);
                _materialInstance.SetFloat(PropPulseIntensity, _completePulseIntensity);
            }
        }

        /// <summary>
        /// EaseOutBack easing function - elastic effect
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
        public bool IsAnimating => _isAnimating;

        /// <summary>
        /// Set effect Material
        /// </summary>
        public void SetEffectMaterial(Material material)
        {
            if (_materialInstance != null)
            {
                Destroy(_materialInstance);
            }

            _hintEffectMaterial = material;
            _materialInstance = material != null ? new Material(material) : null;
        }
    }
}
