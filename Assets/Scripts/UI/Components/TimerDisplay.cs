using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SPPB.UI.Components
{
    /// <summary>
    /// Timer Display Component
    /// Contains: frame, progress bar (shader-controlled fill), seconds text, unit image, test time image, Icon
    /// </summary>
    public class TimerDisplay : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private Image _frame;                  // Frame
        [SerializeField] private Image _barFillImage;           // Progress bar Image (using BarFillShader)
        [SerializeField] private TextMeshProUGUI _timerText;    // Seconds text (single digit: 10, 9, 8...)
        [SerializeField] private Image _unitImage;              // Unit image
        [SerializeField] private Image _titleImage;             // Test time image
        [SerializeField] private Image _icon;                   // Icon

        [Header("Settings")]
        [SerializeField] private bool _countDown = true;        // true: countdown, false: count up
        [SerializeField] private bool _fillFromRight = false;   // true: fill from right to left, false: fill from left to right
        [SerializeField] private bool _reverseFill = false;     // true: bar from full to empty (1→0), false: bar from empty to full (0→1)

        // Maximum duration
        private float _maxDuration = 10f;

        // Current time
        private float _currentTime = 0f;

        // Progress bar Material
        private Material _barMaterial;

        // Shader property IDs
        private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");
        private static readonly int FillDirectionId = Shader.PropertyToID("_FillDirection");

        private void Awake()
        {
            InitializeMaterial();
        }

        /// <summary>
        /// Initialize Material
        /// </summary>
        private void InitializeMaterial()
        {
            if (_barFillImage == null)
            {
                Debug.LogWarning("[TimerDisplay] _barFillImage not set!");
                return;
            }

            // Check if Image has Material
            Material sourceMaterial = _barFillImage.material;

            // Unity UI Image uses Default UI Material by default, need to check for custom Material
            if (sourceMaterial == null || sourceMaterial.shader.name == "UI/Default")
            {
                Debug.LogWarning($"[TimerDisplay] _barFillImage needs a Material using BarFillShader! Current shader: {(sourceMaterial != null ? sourceMaterial.shader.name : "null")}");
                return;
            }

            // Create Material instance to avoid affecting other objects using the same shader
            _barMaterial = Instantiate(sourceMaterial);
            _barFillImage.material = _barMaterial;

            // Set fill direction
            _barMaterial.SetFloat(FillDirectionId, _fillFromRight ? 1f : 0f);

            // Set initial fill amount
            _barMaterial.SetFloat(FillAmountId, 0f);
        }

        private void OnDestroy()
        {
            // Clean up Material instance
            if (_barMaterial != null)
            {
                Destroy(_barMaterial);
            }
        }

        /// <summary>
        /// Set maximum duration
        /// </summary>
        public void SetMaxDuration(float duration)
        {
            _maxDuration = duration;
        }

        /// <summary>
        /// Set timer mode
        /// </summary>
        /// <param name="countDown">true: countdown, false: count up</param>
        public void SetCountDownMode(bool countDown)
        {
            _countDown = countDown;
        }

        /// <summary>
        /// Set reverse fill mode
        /// </summary>
        /// <param name="reverse">true: bar from full to empty (1→0), false: bar from empty to full (0→1)</param>
        public void SetReverseFillMode(bool reverse)
        {
            _reverseFill = reverse;
        }

        /// <summary>
        /// Set current time and update display
        /// </summary>
        public void SetTime(float time)
        {
            // Ensure Material is initialized
            if (_barMaterial == null)
            {
                InitializeMaterial();
            }

            _currentTime = time;
            UpdateDisplay();
        }

        /// <summary>
        /// Reset timer
        /// </summary>
        public void Reset()
        {
            _currentTime = _countDown ? _maxDuration : 0f;

            // Set initial fill based on reverse fill mode
            if (_reverseFill)
            {
                SetBarFill(1f);  // Start from full
            }
            else
            {
                SetBarFill(0f);  // Start from empty
            }

            UpdateDisplay();
        }

        /// <summary>
        /// Initialize timer
        /// </summary>
        /// <param name="maxDuration">Maximum duration</param>
        /// <param name="countDown">Whether to countdown</param>
        /// <param name="reverseFill">Whether to reverse fill (bar from full to empty)</param>
        public void Initialize(float maxDuration, bool countDown, bool reverseFill = false)
        {
            // Ensure Material is initialized
            if (_barMaterial == null)
            {
                InitializeMaterial();
            }

            _maxDuration = maxDuration;
            _countDown = countDown;
            _reverseFill = reverseFill;

            Reset();
        }

        /// <summary>
        /// Update display
        /// </summary>
        private void UpdateDisplay()
        {
            // Update seconds text (single digit)
            UpdateTimerText();

            // Update progress bar fill
            UpdateBarFill();
        }

        /// <summary>
        /// Update seconds text
        /// </summary>
        private void UpdateTimerText()
        {
            if (_timerText == null) return;

            if (_countDown)
            {
                // Countdown: show remaining seconds (ceiling)
                _timerText.text = Mathf.CeilToInt(_currentTime).ToString();
            }
            else
            {
                // Count up: show elapsed seconds (floor)
                _timerText.text = Mathf.FloorToInt(_currentTime).ToString();
            }
        }

        /// <summary>
        /// Update progress bar fill
        /// </summary>
        private void UpdateBarFill()
        {
            if (_barMaterial == null || _maxDuration <= 0) return;

            float progress;

            if (_countDown)
            {
                // Countdown: calculate elapsed time ratio
                float elapsed = _maxDuration - _currentTime;
                progress = elapsed / _maxDuration;
            }
            else
            {
                // Count up: calculate current time ratio
                progress = _currentTime / _maxDuration;
            }

            progress = Mathf.Clamp01(progress);

            // Adjust based on reverse fill mode
            if (_reverseFill)
            {
                // Reverse: from full to empty (1 - progress)
                SetBarFill(1f - progress);
            }
            else
            {
                // Normal: from empty to full
                SetBarFill(progress);
            }
        }

        /// <summary>
        /// Set progress bar fill amount (0-1)
        /// </summary>
        private void SetBarFill(float fillAmount)
        {
            if (_barMaterial != null)
            {
                _barMaterial.SetFloat(FillAmountId, fillAmount);
                // Debug.Log($"[TimerDisplay] SetBarFill: {fillAmount:F3}");
            }
            else
            {
                Debug.LogWarning("[TimerDisplay] SetBarFill failed: _barMaterial is null");
            }
        }

        /// <summary>
        /// Set Icon image
        /// </summary>
        public void SetIcon(Sprite sprite)
        {
            if (_icon != null && sprite != null)
            {
                _icon.sprite = sprite;
            }
        }

        /// <summary>
        /// Set title image
        /// </summary>
        public void SetTitleImage(Sprite sprite)
        {
            if (_titleImage != null && sprite != null)
            {
                _titleImage.sprite = sprite;
            }
        }

        /// <summary>
        /// Set unit image
        /// </summary>
        public void SetUnitImage(Sprite sprite)
        {
            if (_unitImage != null && sprite != null)
            {
                _unitImage.sprite = sprite;
            }
        }

        /// <summary>
        /// Show timer
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Hide timer
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Set fill direction
        /// </summary>
        /// <param name="fromRight">true: fill from right to left, false: fill from left to right</param>
        public void SetFillDirection(bool fromRight)
        {
            _fillFromRight = fromRight;
            if (_barMaterial != null)
            {
                _barMaterial.SetFloat(FillDirectionId, fromRight ? 1f : 0f);
            }
        }

        /// <summary>
        /// Get currently displayed seconds
        /// </summary>
        public int GetDisplayedSeconds()
        {
            if (_countDown)
            {
                return Mathf.CeilToInt(_currentTime);
            }
            else
            {
                return Mathf.FloorToInt(_currentTime);
            }
        }

        /// <summary>
        /// Get progress (0-1)
        /// </summary>
        public float GetProgress()
        {
            if (_maxDuration <= 0) return 0f;

            if (_countDown)
            {
                float elapsed = _maxDuration - _currentTime;
                return Mathf.Clamp01(elapsed / _maxDuration);
            }
            else
            {
                return Mathf.Clamp01(_currentTime / _maxDuration);
            }
        }

        /// <summary>
        /// Get current time
        /// </summary>
        public float GetCurrentTime()
        {
            return _currentTime;
        }

        /// <summary>
        /// Get maximum duration
        /// </summary>
        public float GetMaxDuration()
        {
            return _maxDuration;
        }

        /// <summary>
        /// Get progress bar Material (for external shader parameter adjustment)
        /// </summary>
        public Material GetBarMaterial()
        {
            return _barMaterial;
        }
    }
}
