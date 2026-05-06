using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SPPB.Core;
using SPPB.Data;
using SPPB.UI.Components;
using System.Collections;
using UnityEngine.Events;

namespace SPPB.UI.Pages
{
    /// <summary>
    /// Test Page - Executes various tests
    /// </summary>
    public class TestPage : BasePage
    {
        [Header("Top Bar - Title Sprites (4)")]
        [SerializeField] private Sprite _aPoseCalibrationTitleSprite;
        [SerializeField] private Sprite _balanceTitleSprite;
        [SerializeField] private Sprite _sitStandTitleSprite;
        [SerializeField] private Sprite _walkTitleSprite;

        [Header("Camera Display")]
        [SerializeField] private RawImage _cameraDisplay;

        [Header("APose Detection Frame")]
        [SerializeField] private GameObject _aPoseFrame;

        [Header("Test Icons - Individual Images")]
        [SerializeField] private Image _iconImage_BalanceSideBySide;
        [SerializeField] private Image _iconImage_BalanceSemiTandem;
        [SerializeField] private Image _iconImage_BalanceTandem;
        [SerializeField] private Image _iconImage_SitStand;
        [SerializeField] private Image _iconImage_Walk;

        [Header("Timer")]
        [SerializeField] private TimerDisplay _timerDisplay;

        [Header("Counter (Sit-Stand Test)")]
        [SerializeField] private CounterDisplay _counterDisplay;

        [Header("Hint Image Settings")]
        [SerializeField] private float _hintScaleAnimationDuration = 0.2f;
        [SerializeField] private HintEffectController _hintEffectController;

        [Header("Countdown Hint Images (Common)")]
        [SerializeField] private Image _hintImage_Countdown3;
        [SerializeField] private Image _hintImage_Countdown2;
        [SerializeField] private Image _hintImage_Countdown1;
        [SerializeField] private Image _hintImage_TestStart;

        [Header("Action Feedback Hint Images")]
        [SerializeField] private Image _hintImage_ActionCorrect;

        [Header("Action Feedback Animation")]
        [SerializeField] private float _actionCorrectDisplayDuration = 1.5f;
        [SerializeField] private float _actionWrongDisplayDuration = 1.0f;
        [SerializeField] private float _actionHintScaleDuration = 0.25f;
        [SerializeField] private float _actionHintScaleOvershoot = 1.25f;
        [SerializeField] private float _actionHintFadeDuration = 0.3f;
        [SerializeField] private float _correctBreathPeriod = 1.5f;
        [SerializeField] private float _correctBreathMinAlpha = 0.4f;

        [Header("Test Complete Hint Images (Per Test)")]
        [SerializeField] private Image _hintImage_APoseSuccess;
        [SerializeField] private Image _hintImage_BalanceSideBySide_Complete;
        [SerializeField] private Image _hintImage_BalanceSemiTandem_Complete;
        [SerializeField] private Image _hintImage_BalanceTandem_Complete;
        [SerializeField] private Image _hintImage_SitStand_Complete;
        [SerializeField] private Image _hintImage_Walk_Complete;
        [SerializeField] private Image _hintImage_BalanceFailed;

        [Header("Test Settings")]
        [SerializeField] private float _countdownDuration = 3f;
        [SerializeField] private float _balanceTestDuration = 10f;
        [SerializeField] private float _balanceSafetyTimeout = 15f;
        [SerializeField] private int _sitStandTargetCount = 5;
        [SerializeField] private float _sitStandMaxDuration = 60f;
        [SerializeField] private float _walkMaxDuration = 60f;
        [SerializeField] private float _goDelayDuration = 0.5f;
        [SerializeField] private float _goodDelayDuration = 1.5f;

        [Header("GO Hint Fade Out Settings")]
        [SerializeField] private float _goDisplayDuration = 1.0f;
        [SerializeField] private float _goFadeOutDuration = 0.5f;

        [Header("Video Pose")]
        [SerializeField] public VideoPoseTest videoPoseTest;
        [SerializeField] public MotionSDKClient motionSDKClient;

        [Header("Walk Progress Bar")]
        [SerializeField] private GameObject _walkProgressBarRoot;
        [SerializeField] private Image _walkProgressBarFill;
        [SerializeField] private TextMeshProUGUI _walkDistanceText;

        // Current step
        private FlowStep _currentStep;

        // Test state
        private TestState _testState = TestState.Idle;
        private float _timer = 0f;
        private float _balanceSafetyTimer = 0f;
        private bool _balanceFailed = false;
        private int _sitStandCount = 0;
        private int _lastCountdown = -1;

        // Float comparison tolerance
        private const float TIMER_EPSILON = 0.01f;

        // Hint image animation
        private bool _isHintAnimating = false;
        private float _hintAnimTimer = 0f;
        private Image _currentHintImage = null;
        private System.Collections.Generic.Dictionary<Image, Vector3> _hintOriginalScales = new System.Collections.Generic.Dictionary<Image, Vector3>();
        private System.Collections.Generic.Dictionary<Image, Color> _hintOriginalColors = new System.Collections.Generic.Dictionary<Image, Color>();

        // GO hint fade out coroutine
        private Coroutine _goFadeOutCoroutine;

        // Action feedback coroutine
        private Coroutine _actionFeedbackCoroutine;
        private Coroutine _balanceHintCoroutine;

        private bool _isCalibrate = true;
        private UnityAction<bool> _nuwaCompleteCallback = null;
        private string _nuwaText = "";

        // Walk progress bar
        private static readonly int WalkFillAmountId = Shader.PropertyToID("_FillAmount");
        private const float WALK_MAX_DISTANCE = 300f; // diff 範圍 0~300 (diff_raw/10, PDF p.9)

        /// <summary>
        /// Test state enumeration
        /// </summary>
        private enum TestState
        {
            Idle,
            Countdown,
            Testing,
            Completed
        }

        public override void Initialize()
        {
            base.Initialize();

            // Cache original scales of all hint images
            CacheHintImageOriginalScale(_hintImage_Countdown3);
            CacheHintImageOriginalScale(_hintImage_Countdown2);
            CacheHintImageOriginalScale(_hintImage_Countdown1);
            CacheHintImageOriginalScale(_hintImage_TestStart);
            CacheHintImageOriginalScale(_hintImage_ActionCorrect);
            CacheHintImageOriginalScale(_hintImage_APoseSuccess);
            CacheHintImageOriginalScale(_hintImage_BalanceSideBySide_Complete);
            CacheHintImageOriginalScale(_hintImage_BalanceSemiTandem_Complete);
            CacheHintImageOriginalScale(_hintImage_BalanceTandem_Complete);
            CacheHintImageOriginalScale(_hintImage_SitStand_Complete);
            CacheHintImageOriginalScale(_hintImage_Walk_Complete);
            CacheHintImageOriginalScale(_hintImage_BalanceFailed);

            // Ensure all hint images are initially hidden
            HideAllHintImages();
        }

        /// <summary>
        /// Cache the original scale of a hint image
        /// </summary>
        private void CacheHintImageOriginalScale(Image image)
        {
            if (image != null && !_hintOriginalScales.ContainsKey(image))
            {
                _hintOriginalScales[image] = image.transform.localScale;
            }

            // Also cache original color
            if (image != null && !_hintOriginalColors.ContainsKey(image))
            {
                _hintOriginalColors[image] = image.color;
            }
        }

        public override void Configure(FlowStep step)
        {
            _currentStep = step;
            ConfigureUIForStep(step);
            ConfigureTTSForStep(step);
            NuwaManager.Instance.NuwaTTS(_nuwaText, _nuwaCompleteCallback);
        }

        protected override void OnPageEnter()
        {
            base.OnPageEnter();

            if (_currentStep != FlowStep.APoseCalibration)
            {
                StartCountdown();
            }
        }

        protected override void OnPageExit()
        {
            base.OnPageExit();
            ResetTestState();
        }

        private void OnCalibrateAction(bool value)
        {
            _isCalibrate = false;
        }

        private void Update()
        {
            // Hint image scale animation
            if (_isHintAnimating && _currentHintImage != null)
            {
                _hintAnimTimer += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(_hintAnimTimer / _hintScaleAnimationDuration);
                float currentScale = Mathf.Lerp(0f, 1f, progress);

                Vector3 originalScale = GetHintOriginalScale(_currentHintImage);
                _currentHintImage.transform.localScale = originalScale * currentScale;

                if (progress >= 1f)
                {
                    _isHintAnimating = false;
                }
            }

            // APose calibration: press Space to skip (calibration success)
            // if (_currentStep == FlowStep.APoseCalibration && Input.GetKeyDown(KeyCode.Space))
            // {
            //     OnAPoseCalibrationComplete();
            //     return;
            // }
            if (!_isCalibrate && _currentStep == FlowStep.APoseCalibration && videoPoseTest.videoPoseData != "[]" && videoPoseTest.videoPoseData != "")
            {
                OnAPoseCalibrationComplete();
                return;
            }

            // Test feature: press P to increment sit-stand count
            if (_currentStep == FlowStep.SitStand_Test && _testState == TestState.Testing && Input.GetKeyDown(KeyCode.P))
            {
                IncrementSitStandCount();
            }

            // Handle test timing
            switch (_testState)
            {
                case TestState.Countdown:
                    UpdateCountdown();
                    break;
                case TestState.Testing:
                    UpdateTesting();
                    break;
            }
        }

        #region UI Configuration

        private void ConfigureTTSForStep(FlowStep step)
        {
            _nuwaText = "";
            _nuwaCompleteCallback = null;
            switch (step)
            {
                case FlowStep.APoseCalibration:
                    _nuwaText = "請將身體對準畫面中的人型圖示，定位成功後會提醒您。";
                    _nuwaCompleteCallback = OnCalibrateAction;
                    break;

                case FlowStep.BalanceSideBySide_Test:
                    break;

                case FlowStep.BalanceSemiTandem_Test:
                    break;

                case FlowStep.BalanceTandem_Test:
                    break;

                case FlowStep.SitStand_Test:
                    break;

                case FlowStep.Walk_Test:
                    break;
            }
        }

        /// <summary>
        /// Configure UI elements based on step
        /// </summary>
        private void ConfigureUIForStep(FlowStep step)
        {
            HideAllDynamicElements();

            switch (step)
            {
                case FlowStep.APoseCalibration:
                    ShowAPoseFrame();
                    break;

                case FlowStep.BalanceSideBySide_Test:
                    ShowTestIcon(_iconImage_BalanceSideBySide);
                    ShowTimer();
                    break;

                case FlowStep.BalanceSemiTandem_Test:
                    ShowTestIcon(_iconImage_BalanceSemiTandem);
                    ShowTimer();
                    break;

                case FlowStep.BalanceTandem_Test:
                    ShowTestIcon(_iconImage_BalanceTandem);
                    ShowTimer();
                    break;

                case FlowStep.SitStand_Test:
                    ShowTestIcon(_iconImage_SitStand);
                    ShowTimer();
                    ShowCounter();
                    break;

                case FlowStep.Walk_Test:
                    ShowTestIcon(_iconImage_Walk);
                    ShowWalkProgressBar();
                    break;
            }
        }

        /// <summary>
        /// Hide all dynamic elements
        /// </summary>
        private void HideAllDynamicElements()
        {
            HideAPoseFrame();
            HideAllTestIcons();
            HideTimer();
            HideCounter();
            HideWalkProgressBar();
            HideAllHintImages();
        }

        private void ShowTestIcon(Image iconImage)
        {
            if (iconImage != null)
            {
                iconImage.gameObject.SetActive(true);
            }
        }

        private void HideAllTestIcons()
        {
            SetImageActive(_iconImage_BalanceSideBySide, false);
            SetImageActive(_iconImage_BalanceSemiTandem, false);
            SetImageActive(_iconImage_BalanceTandem, false);
            SetImageActive(_iconImage_SitStand, false);
            SetImageActive(_iconImage_Walk, false);
        }

        private void ShowAPoseFrame()
        {
            if (_aPoseFrame != null)
            {
                _aPoseFrame.SetActive(true);
            }
        }

        private void HideAPoseFrame()
        {
            if (_aPoseFrame != null)
            {
                _aPoseFrame.SetActive(false);
            }
        }

        private void ShowTimer()
        {
            if (_timerDisplay != null)
            {
                _timerDisplay.Show();
            }
        }

        private void HideTimer()
        {
            if (_timerDisplay != null)
            {
                _timerDisplay.Hide();
            }
        }

        private void ShowCounter()
        {
            if (_counterDisplay != null)
            {
                _counterDisplay.Initialize(_sitStandTargetCount);
                _counterDisplay.Show();
            }
        }

        private void HideCounter()
        {
            if (_counterDisplay != null)
            {
                _counterDisplay.Hide();
            }
        }

        private void SetWalkBarFill(float fill)
        {
            // 用 CanvasRenderer.GetMaterial() 才是 Image 當下 render 用的 material instance
            if (_walkProgressBarFill == null || _walkProgressBarFill.canvasRenderer == null) return;
            var mat = _walkProgressBarFill.canvasRenderer.GetMaterial();
            if (mat != null) mat.SetFloat(WalkFillAmountId, fill);
        }

        private void ShowWalkProgressBar()
        {
            if (_walkProgressBarRoot != null) _walkProgressBarRoot.SetActive(true);
            SetWalkBarFill(0f);

            if (_walkDistanceText != null)
            {
                _walkDistanceText.gameObject.SetActive(true);
                _walkDistanceText.text = "0.00";
            }
        }

        private void HideWalkProgressBar()
        {
            if (_walkProgressBarRoot != null) _walkProgressBarRoot.SetActive(false);

            if (_walkDistanceText != null)
                _walkDistanceText.gameObject.SetActive(false);
        }

        public void UpdateWalkProgress(float diff)
        {
            if (_testState != TestState.Testing || _currentStep != FlowStep.Walk_Test) return;

            float fill = Mathf.Clamp01(diff / WALK_MAX_DISTANCE);
            SetWalkBarFill(fill);

            if (_walkDistanceText != null)
            {
                float meters = Mathf.Clamp(diff / 100f, 0f, 3f);
                _walkDistanceText.text = meters.ToString("F2");
            }
        }

        /// <summary>
        /// Show hint image with scale animation and effects
        /// </summary>
        private void ShowHintImage(Image hintImage)
        {
            if (hintImage == null) return;

            // If different image, hide current and restore its original scale
            if (_currentHintImage != null && _currentHintImage != hintImage)
            {
                RestoreHintImageScale(_currentHintImage);
                _currentHintImage.gameObject.SetActive(false);
            }

            // Set new hint image
            bool imageChanged = _currentHintImage != hintImage;
            _currentHintImage = hintImage;
            hintImage.gameObject.SetActive(true);

            if (imageChanged)
            {
                // Check if it's a completion hint
                bool isCompleteHint = IsCompleteHint(hintImage);

                // Use effect controller if available
                if (_hintEffectController != null)
                {
                    var effectMode = isCompleteHint
                        ? HintEffectController.EffectMode.Complete
                        : HintEffectController.EffectMode.Countdown;
                    _hintEffectController.PlayEffect(hintImage, effectMode);
                }
                else
                {
                    // Use original animation when no effect controller
                    _isHintAnimating = true;
                    _hintAnimTimer = 0f;
                    hintImage.transform.localScale = Vector3.zero;
                }
            }
        }

        /// <summary>
        /// Check if the image is a completion hint
        /// </summary>
        private bool IsCompleteHint(Image hintImage)
        {
            return hintImage == _hintImage_APoseSuccess ||
                   hintImage == _hintImage_BalanceSideBySide_Complete ||
                   hintImage == _hintImage_BalanceSemiTandem_Complete ||
                   hintImage == _hintImage_BalanceTandem_Complete ||
                   hintImage == _hintImage_SitStand_Complete ||
                   hintImage == _hintImage_Walk_Complete ||
                   hintImage == _hintImage_BalanceFailed;
        }

        /// <summary>
        /// Hide all hint images
        /// </summary>
        private void HideAllHintImages()
        {
            // Stop effects
            if (_hintEffectController != null)
            {
                _hintEffectController.StopEffect();
            }

            HideAndRestoreHintImage(_hintImage_Countdown3);
            HideAndRestoreHintImage(_hintImage_Countdown2);
            HideAndRestoreHintImage(_hintImage_Countdown1);
            HideAndRestoreHintImage(_hintImage_TestStart);
            if (_actionFeedbackCoroutine != null)
            {
                StopCoroutine(_actionFeedbackCoroutine);
                _actionFeedbackCoroutine = null;
            }
            if (_balanceHintCoroutine != null)
            {
                StopCoroutine(_balanceHintCoroutine);
                _balanceHintCoroutine = null;
            }
            HideAndRestoreHintImage(_hintImage_ActionCorrect);
            HideAndRestoreHintImage(_hintImage_APoseSuccess);
            HideAndRestoreHintImage(_hintImage_BalanceSideBySide_Complete);
            HideAndRestoreHintImage(_hintImage_BalanceSemiTandem_Complete);
            HideAndRestoreHintImage(_hintImage_BalanceTandem_Complete);
            HideAndRestoreHintImage(_hintImage_SitStand_Complete);
            HideAndRestoreHintImage(_hintImage_Walk_Complete);
            HideAndRestoreHintImage(_hintImage_BalanceFailed);

            _currentHintImage = null;
            _isHintAnimating = false;
        }

        /// <summary>
        /// Hide hint image and restore original scale and color
        /// </summary>
        private void HideAndRestoreHintImage(Image image)
        {
            if (image != null)
            {
                RestoreHintImageScale(image);
                RestoreHintImageColor(image);
                image.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Restore hint image original color
        /// </summary>
        private void RestoreHintImageColor(Image image)
        {
            if (image != null && _hintOriginalColors.TryGetValue(image, out Color originalColor))
            {
                image.color = originalColor;
            }
        }

        /// <summary>
        /// Restore hint image original scale
        /// </summary>
        private void RestoreHintImageScale(Image image)
        {
            if (image != null && _hintOriginalScales.TryGetValue(image, out Vector3 originalScale))
            {
                image.transform.localScale = originalScale;
            }
        }

        /// <summary>
        /// Get hint image original scale
        /// </summary>
        private Vector3 GetHintOriginalScale(Image image)
        {
            if (image != null && _hintOriginalScales.TryGetValue(image, out Vector3 originalScale))
            {
                return originalScale;
            }
            return Vector3.one;
        }

        /// <summary>
        /// Set Image active state
        /// </summary>
        private void SetImageActive(Image image, bool active)
        {
            if (image != null)
            {
                image.gameObject.SetActive(active);
            }
        }

        /// <summary>
        /// Get countdown hint image by number
        /// </summary>
        private Image GetCountdownImage(int countdown)
        {
            switch (countdown)
            {
                case 3: return _hintImage_Countdown3;
                case 2: return _hintImage_Countdown2;
                case 1: return _hintImage_Countdown1;
                default: return null;
            }
        }

        /// <summary>
        /// Get test complete hint image for current step
        /// </summary>
        private Image GetTestCompleteImage()
        {
            switch (_currentStep)
            {
                case FlowStep.BalanceSideBySide_Test:
                    return _hintImage_BalanceSideBySide_Complete;
                case FlowStep.BalanceSemiTandem_Test:
                    return _hintImage_BalanceSemiTandem_Complete;
                case FlowStep.BalanceTandem_Test:
                    return _hintImage_BalanceTandem_Complete;
                case FlowStep.SitStand_Test:
                    return _hintImage_SitStand_Complete;
                case FlowStep.Walk_Test:
                    return _hintImage_Walk_Complete;
                default:
                    return null;
            }
        }

        /// 依照測驗結果回傳對應 hint（平衡測試失敗時顯示失敗圖）
        private Image GetResultHintImage()
        {
            bool isBalance = _currentStep == FlowStep.BalanceSideBySide_Test
                          || _currentStep == FlowStep.BalanceSemiTandem_Test
                          || _currentStep == FlowStep.BalanceTandem_Test;

            if (isBalance && _balanceFailed)
                return _hintImage_BalanceFailed;

            return GetTestCompleteImage();
        }

        private string GetCompleteTestText()
        {
            switch (_currentStep)
            {
                case FlowStep.BalanceSideBySide_Test:
                    return "已完成第一種姿勢測試";
                case FlowStep.BalanceSemiTandem_Test:
                    return "已完成第二種姿勢測試"; ;
                case FlowStep.BalanceTandem_Test:
                    return "已完成平衡測試，接下來進行坐站測試";
                case FlowStep.SitStand_Test:
                    return "已完成坐站測試，最後進行步態速度測試";
                case FlowStep.Walk_Test:
                    return "恭喜您已完成所有測試";
                default:
                    return null;
            }
        }

        #endregion

        #region Test Flow Control

        /// <summary>
        /// Start countdown
        /// </summary>
        private void StartCountdown()
        {
            _testState = TestState.Countdown;
            _timer = _countdownDuration;

            InitializeTimerDisplay();

            // Show first countdown image immediately
            int countdown = Mathf.CeilToInt(_timer);
            Image countdownImage = GetCountdownImage(countdown);
            if (countdownImage != null)
            {
                ShowHintImage(countdownImage);
            }
        }

        /// <summary>
        /// Initialize timer display
        /// </summary>
        private void InitializeTimerDisplay()
        {
            if (_timerDisplay == null) return;

            switch (_currentStep)
            {
                case FlowStep.BalanceSideBySide_Test:
                case FlowStep.BalanceSemiTandem_Test:
                case FlowStep.BalanceTandem_Test:
                    // Balance test: count up (0→10s), bar fills from empty to full
                    _timerDisplay.Initialize(_balanceTestDuration, false, false);
                    break;

                case FlowStep.SitStand_Test:
                    // Sit-stand test: countdown, bar empties from full to empty
                    _timerDisplay.Initialize(_sitStandMaxDuration, true, true);
                    break;

                case FlowStep.Walk_Test:
                    // Walk test: countdown, bar empties from full to empty
                    _timerDisplay.Initialize(_walkMaxDuration, true, true);
                    break;
            }
        }

        /// <summary>
        /// Update countdown
        /// </summary>
        private void UpdateCountdown()
        {
            _timer -= Time.deltaTime;

            if (_timer <= TIMER_EPSILON)
            {
                _timer = 0f;
                _testState = TestState.Idle;
                _lastCountdown = -1;
                ShowHintImage(_hintImage_TestStart);
                NuwaManager.Instance.NuwaTTS("開始");

                // Start GO hint fade out sequence
                if (_goFadeOutCoroutine != null)
                {
                    StopCoroutine(_goFadeOutCoroutine);
                }
                _goFadeOutCoroutine = StartCoroutine(GoHintFadeOutSequence());
                return;
            }

            int countdown = Mathf.CeilToInt(_timer);

            // Only update hint image when countdown number changes
            if (countdown != _lastCountdown)
            {
                _lastCountdown = countdown;
                Image countdownImage = GetCountdownImage(countdown);
                NuwaManager.Instance.NuwaTTS(countdown.ToString());
                if (countdownImage != null)
                {
                    ShowHintImage(countdownImage);
                }
            }
        }

        /// <summary>
        /// GO hint fade out sequence
        /// </summary>
        private IEnumerator GoHintFadeOutSequence()
        {
            // Display for a duration first
            yield return new WaitForSeconds(_goDisplayDuration);

            // Start fade out
            if (_hintImage_TestStart != null)
            {
                Color originalColor = GetHintOriginalColor(_hintImage_TestStart);
                float elapsed = 0f;

                while (elapsed < _goFadeOutDuration)
                {
                    elapsed += Time.deltaTime;
                    float progress = Mathf.Clamp01(elapsed / _goFadeOutDuration);
                    float alpha = Mathf.Lerp(originalColor.a, 0f, progress);
                    _hintImage_TestStart.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
                    yield return null;
                }

                // Ensure fully transparent
                _hintImage_TestStart.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);
            }

            // Start testing after fade out completes
            StartTesting();
        }

        /// <summary>
        /// Get hint image original color
        /// </summary>
        private Color GetHintOriginalColor(Image image)
        {
            if (image != null && _hintOriginalColors.TryGetValue(image, out Color originalColor))
            {
                return originalColor;
            }
            return Color.white;
        }

        /// <summary>
        /// Start testing
        /// </summary>
        private void StartTesting()
        {
            _testState = TestState.Testing;
            _sitStandCount = 0;
            HideAllHintImages();

            switch (_currentStep)
            {
                case FlowStep.BalanceSideBySide_Test:
                    motionSDKClient.start1_1_single = true;
                    _timer = 0f;
                    _balanceSafetyTimer = 0f;
                    _balanceFailed = false;
                    break;

                case FlowStep.BalanceSemiTandem_Test:
                    motionSDKClient.start1_2_single = true;
                    _timer = 0f;
                    _balanceSafetyTimer = 0f;
                    _balanceFailed = false;
                    break;

                case FlowStep.BalanceTandem_Test:
                    motionSDKClient.start1_3_single = true;
                    _timer = 0f;
                    _balanceSafetyTimer = 0f;
                    _balanceFailed = false;
                    break;

                case FlowStep.SitStand_Test:
                    motionSDKClient.start3_single = true;
                    _timer = _sitStandMaxDuration;
                    if (_counterDisplay != null)
                    {
                        _counterDisplay.Reset();
                    }
                    break;

                case FlowStep.Walk_Test:
                    motionSDKClient.start2_single = true;
                    _timer = _walkMaxDuration;
                    break;
            }

            // Update timer display
            if (_timerDisplay != null)
            {
                _timerDisplay.SetTime(_timer);
            }
        }

        /// <summary>
        /// Update testing state
        /// </summary>
        private void UpdateTesting()
        {
            switch (_currentStep)
            {
                case FlowStep.BalanceSideBySide_Test:
                case FlowStep.BalanceSemiTandem_Test:
                case FlowStep.BalanceTandem_Test:
                    // Balance: Unity _timer 改為累計時數 (0 → 10s 顯示)；結束由 SDK state=0 觸發 OnBalanceTestComplete。
                    // _balanceSafetyTimer 是防呆：若 SDK 異常未回應就強制結束。
                    _timer += Time.deltaTime;
                    if (_timer > _balanceTestDuration) _timer = _balanceTestDuration;

                    _balanceSafetyTimer += Time.deltaTime;
                    if (_balanceSafetyTimer >= _balanceSafetyTimeout)
                    {
                        Debug.LogWarning($"[Balance] Safety timeout ({_balanceSafetyTimeout}s) — 強制結束");
                        _balanceFailed = true;
                        CompleteTest();
                    }
                    break;

                case FlowStep.SitStand_Test:
                    // Sit-stand test: countdown
                    _timer -= Time.deltaTime;
                    if (_timer <= TIMER_EPSILON)
                    {
                        _timer = 0f;
                        CompleteTest();
                    }
                    break;

                case FlowStep.Walk_Test:
                    // Walk test: countdown
                    _timer -= Time.deltaTime;
                    if (_timer <= TIMER_EPSILON)
                    {
                        _timer = 0f;
                        CompleteTest();
                    }
                    break;
            }

            // Update timer display
            if (_timerDisplay != null)
            {
                _timerDisplay.SetTime(_timer);
            }
        }

        /// <summary>
        /// Complete test
        /// </summary>
        private void CompleteTest()
        {
            motionSDKClient.end = true;
            _testState = TestState.Completed;
            HideWalkProgressBar();

            // 清除動作提示
            if (_actionFeedbackCoroutine != null)
            {
                StopCoroutine(_actionFeedbackCoroutine);
                _actionFeedbackCoroutine = null;
            }
            if (_balanceHintCoroutine != null)
            {
                StopCoroutine(_balanceHintCoroutine);
                _balanceHintCoroutine = null;
            }
            HideAndRestoreHintImage(_hintImage_ActionCorrect);

            ShowHintImage(GetResultHintImage());

            // Save score to ScoreManager
            SaveScoreToManager();
            NuwaManager.Instance.NuwaTTS(GetCompleteTestText(), (v) =>
                {
                    Invoke(nameof(GoToNextStep), _goodDelayDuration);
                }
            );
        }

        /// <summary>
        /// Save score to ScoreManager
        /// Note: All scores are set directly by SDK, this method is reserved for future use
        /// </summary>
        private void SaveScoreToManager()
        {
            // All scoring logic is handled by SDK
            // SDK will directly call ScoreManager methods to set scores
        }

        /// <summary>
        /// Go to next step
        /// </summary>
        private void GoToNextStep()
        {
            UIManager.Instance.NextStep();
        }

        /// <summary>
        /// Reset test state
        /// </summary>
        private void ResetTestState()
        {
            _testState = TestState.Idle;
            _timer = 0f;
            _sitStandCount = 0;
            _currentHintImage = null;
            _isHintAnimating = false;
            _isCalibrate = true;
            CancelInvoke(nameof(GoToNextStep));
            CancelInvoke(nameof(StartTesting));

            // Cancel fade out coroutine
            if (_goFadeOutCoroutine != null)
            {
                StopCoroutine(_goFadeOutCoroutine);
                _goFadeOutCoroutine = null;
            }

            // Cancel action feedback coroutine
            if (_actionFeedbackCoroutine != null)
            {
                StopCoroutine(_actionFeedbackCoroutine);
                _actionFeedbackCoroutine = null;
            }
            if (_balanceHintCoroutine != null)
            {
                StopCoroutine(_balanceHintCoroutine);
                _balanceHintCoroutine = null;
            }
        }

        /// <summary>
        /// Increment sit-stand count (called by external AI detection)
        /// </summary>
        public void IncrementSitStandCount(int count = -1)
        {
            if (_testState != TestState.Testing || _currentStep != FlowStep.SitStand_Test || _sitStandCount == count)
                return;

            if (count == -1)
                _sitStandCount++;
            else
                _sitStandCount = count;

            // Update counter display
            if (_counterDisplay != null)
            {
                _counterDisplay.SetCount(_sitStandCount);
            }

            if (_sitStandCount >= _sitStandTargetCount)
            {
                CompleteTest();
            }
        }

        /// <summary>
        /// 立即隱藏正確提示（由 MotionSDKClient 在姿勢離開正確狀態時呼叫）
        /// </summary>
        public void HideCorrectHint()
        {
            if (_actionFeedbackCoroutine != null)
            {
                StopCoroutine(_actionFeedbackCoroutine);
                _actionFeedbackCoroutine = null;
            }
            if (_balanceHintCoroutine != null)
            {
                StopCoroutine(_balanceHintCoroutine);
                _balanceHintCoroutine = null;
            }
            HideAndRestoreHintImage(_hintImage_ActionCorrect);
        }

        /// <summary>
        /// 動作提示（由 MotionSDKClient 呼叫）
        /// </summary>
        public void OnActionFeedback(bool isCorrect, float score)
        {
            if (_testState != TestState.Testing) return;
            if (!isCorrect) return;

            if (_actionFeedbackCoroutine != null)
                StopCoroutine(_actionFeedbackCoroutine);
            if (_balanceHintCoroutine != null)
                StopCoroutine(_balanceHintCoroutine);

            _actionFeedbackCoroutine = StartCoroutine(ActionFeedbackCoroutine(_hintImage_ActionCorrect, true));
        }

        private IEnumerator ActionFeedbackCoroutine(Image hintImage, bool isCorrect)
        {
            if (hintImage == null) yield break;


            // 若有 HintFill shader，設定 FillAmount 初始值
            Material mat = hintImage.material;
            if (mat != null && mat.HasProperty("_FillAmount"))
            {
                if (!hintImage.material.name.Contains("(Instance)"))
                    hintImage.material = new Material(mat);
                mat = hintImage.material;
                mat.SetFloat("_FillAmount", isCorrect ? 1f : 0f);
            }
            else mat = null;

            // 初始化
            hintImage.transform.localScale = Vector3.zero;
            CanvasGroup cg = hintImage.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;
            RestoreHintImageColor(hintImage);
            hintImage.gameObject.SetActive(true);

            Vector3 originalScale = GetHintOriginalScale(hintImage);
            Vector3 overshootScale = originalScale * _actionHintScaleOvershoot;

            // Phase 1：縮放上升至 overshoot（easeOutQuad）
            float elapsed = 0f;
            float upDur = _actionHintScaleDuration * 0.4f;
            while (elapsed < upDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / upDur), 2f);
                hintImage.transform.localScale = Vector3.Lerp(Vector3.zero, overshootScale, t);
                yield return null;
            }

            // Phase 2：彈回原始大小（easeOutElastic）
            elapsed = 0f;
            float downDur = _actionHintScaleDuration * 0.6f;
            while (elapsed < downDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = ActionEaseOutElastic(Mathf.Clamp01(elapsed / downDur));
                hintImage.transform.localScale = Vector3.Lerp(overshootScale, originalScale, t);
                yield return null;
            }
            hintImage.transform.localScale = originalScale;

            if (isCorrect)
            {
                // 正確提示：FillAmount 從 1 倒數到 0（對應 10 秒測驗），同時呼吸閃爍效果
                Color baseColor = GetHintOriginalColor(hintImage);
                float fillElapsed = 0f;
                float breathTime = 0f;

                while (true)
                {
                    fillElapsed += Time.unscaledDeltaTime;
                    breathTime += Time.unscaledDeltaTime;

                    // FillAmount：1 → 0，持續 _balanceTestDuration 秒
                    if (mat != null)
                        mat.SetFloat("_FillAmount", Mathf.Clamp01(1f - fillElapsed / _balanceTestDuration));

                    // 呼吸閃爍：alpha 在 _correctBreathMinAlpha ~ 1 之間 sin 波動
                    float breathAlpha = Mathf.Lerp(_correctBreathMinAlpha, 1f,
                        (Mathf.Sin(2f * Mathf.PI * breathTime / _correctBreathPeriod) + 1f) * 0.5f);
                    if (cg != null)
                        cg.alpha = breathAlpha;
                    else
                        hintImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * breathAlpha);

                    yield return null;
                }
            }
            else
            {
                // 錯誤提示：等待後淡出消失
                yield return new WaitForSecondsRealtime(_actionWrongDisplayDuration);

                elapsed = 0f;
                while (elapsed < _actionHintFadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / _actionHintFadeDuration);
                    if (cg != null) cg.alpha = 1f - t;
                    else hintImage.color = new Color(hintImage.color.r, hintImage.color.g, hintImage.color.b, 1f - t);
                    yield return null;
                }

                hintImage.gameObject.SetActive(false);
                if (cg != null) cg.alpha = 1f;
                RestoreHintImageColor(hintImage);
            }

            _actionFeedbackCoroutine = null;
        }

        private float ActionEaseOutElastic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            const float c4 = (2f * Mathf.PI) / 3f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
        }

        /// <summary>
        /// APose calibration complete (called by external AI detection)
        /// </summary>
        public void OnAPoseCalibrationComplete()
        {
            if (_currentStep != FlowStep.APoseCalibration)
                return;

            _isCalibrate = true;
            ShowHintImage(_hintImage_APoseSuccess);
            Invoke(nameof(GoToNextStep), 1f);
        }

        /// <summary>
        /// Walk test complete (called by external AI detection)
        /// </summary>
        public void OnWalkTestComplete(float sdkScore = 0f)
        {
            if (_testState != TestState.Testing || _currentStep != FlowStep.Walk_Test)
                return;

            ScoreManager.Instance.SetWalkScore(Mathf.RoundToInt(sdkScore));
            CompleteTest();
        }

        /// Balance test complete (由 SDK state=0 觸發)
        public void OnBalanceTestComplete(float sdkScore, float sdkElapsed)
        {
            if (_testState != TestState.Testing) return;
            if (_currentStep != FlowStep.BalanceSideBySide_Test &&
                _currentStep != FlowStep.BalanceSemiTandem_Test &&
                _currentStep != FlowStep.BalanceTandem_Test)
                return;

            // 給 1 秒容差避免 elapsed 抓到 9.97 等邊界值被誤判失敗
            _balanceFailed = sdkScore <= 0f && sdkElapsed < (_balanceTestDuration - 1f);
            Debug.Log($"[Balance] complete — sdkScore={sdkScore}, sdkElapsed={sdkElapsed:F2}, failed={_balanceFailed}");

            // 依 elapsed 計算 SPPB 平衡分數並存入 ScoreManager
            switch (_currentStep)
            {
                case FlowStep.BalanceSideBySide_Test:
                    ScoreManager.Instance.SetBalanceSideBySideScore(sdkElapsed >= _balanceTestDuration - 1f ? 1 : 0);
                    break;
                case FlowStep.BalanceSemiTandem_Test:
                    ScoreManager.Instance.SetBalanceSemiTandemScore(sdkElapsed >= _balanceTestDuration - 1f ? 1 : 0);
                    break;
                case FlowStep.BalanceTandem_Test:
                    int tandemScore = sdkElapsed >= _balanceTestDuration - 1f ? 2 : (sdkElapsed >= 3f ? 1 : 0);
                    ScoreManager.Instance.SetBalanceTandemScore(tandemScore);
                    break;
            }

            CompleteTest();
        }

        #endregion

        #region Top Bar Configuration

        /// <summary>
        /// Configure top bar
        /// </summary>
        protected override void ConfigureTopBar()
        {
            TopBarManager.Instance.HideAll();
            TopBarManager.Instance.ShowTitleFrame(true);
            TopBarManager.Instance.ShowTitle(true);
            TopBarManager.Instance.ShowDialog(true);
            TopBarManager.Instance.ShowExitButton(true);

            // Test phase uses test-specific frame
            TopBarManager.Instance.SetTestFrame();

            Sprite titleSprite = GetTitleSpriteForStep(_currentStep);
            string dialogText = GetDialogTextForStep(_currentStep);

            TopBarManager.Instance.SetTitleImage(titleSprite);
            TopBarManager.Instance.SetDialogTextWithAnimation(dialogText);
        }

        private Sprite GetTitleSpriteForStep(FlowStep step)
        {
            switch (step)
            {
                case FlowStep.APoseCalibration:
                    return _aPoseCalibrationTitleSprite;

                case FlowStep.BalanceSideBySide_Test:
                case FlowStep.BalanceSemiTandem_Test:
                case FlowStep.BalanceTandem_Test:
                    return _balanceTitleSprite;

                case FlowStep.SitStand_Test:
                    return _sitStandTitleSprite;

                case FlowStep.Walk_Test:
                    return _walkTitleSprite;

                default:
                    return null;
            }
        }

        private string GetDialogTextForStep(FlowStep step)
        {
            switch (step)
            {
                case FlowStep.APoseCalibration:
                    return "請將身體對準畫面中的人型進行姿勢校準";

                case FlowStep.BalanceSideBySide_Test:
                    return "雙腳併攏站立，請站好並保持平衡";

                case FlowStep.BalanceSemiTandem_Test:
                    return "半步站立，請將一隻腳稍微往前放";

                case FlowStep.BalanceTandem_Test:
                    return "腳跟對腳尖，請將一隻腳放在另一隻腳前面";

                case FlowStep.SitStand_Test:
                    return "請坐在椅子上，雙手交叉抱胸，連續站立並坐下5次";

                case FlowStep.Walk_Test:
                    return "請以平常走路的速度行走";

                default:
                    return "";
            }
        }

        #endregion
    }
}
