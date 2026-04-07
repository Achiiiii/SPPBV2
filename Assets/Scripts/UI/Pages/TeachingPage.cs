using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using SPPB.Core;
using SPPB.Data;
using SPPB.UI.Components;
using System.Collections;
using UnityEngine.Events;

namespace SPPB.UI.Pages
{
    /// <summary>
    /// Teaching Page - Displays teaching videos and instructions
    /// </summary>
    public class TeachingPage : BasePage
    {
        [Header("Top Bar - Title Images (5)")]
        [SerializeField] private Sprite _testIntroTitleSprite;      // Test intro title
        [SerializeField] private Sprite _aPoseCalibrationTitleSprite; // A-Pose calibration title
        [SerializeField] private Sprite _balanceTitleSprite;        // Balance test title
        [SerializeField] private Sprite _sitStandTitleSprite;       // Sit-stand test title
        [SerializeField] private Sprite _walkTitleSprite;           // Walk test title

        [Header("Background")]
        [SerializeField] private Image _backgroundImage;

        [Header("Intro Phase Icon Containers")]
        [SerializeField] private GameObject _iconContainer_TestIntro;              // Test intro
        [SerializeField] private GameObject _iconContainer_BalanceIntro;           // Balance test intro
        [SerializeField] private GameObject _iconContainer_SitStandIntro;          // Sit-stand test intro
        [SerializeField] private GameObject _iconContainer_WalkIntro;              // Walk test intro

        [Header("Intro Phase Animation Controllers")]
        [SerializeField] private SequentialPopAnimator _balanceIntroAnimator;      // Balance test intro animation
        [SerializeField] private SequentialPopAnimator _sitStandIntroAnimator;     // Sit-stand test intro animation
        [SerializeField] private WalkingLoopAnimator _walkIntroAnimator;           // Walk test intro animation

        [Header("Teaching Phase Small Icons - Individual Images")]
        [SerializeField] private Image _iconImage_BalanceSideBySide;               // Side-by-side stance
        [SerializeField] private Image _iconImage_BalanceSemiTandem;               // Semi-tandem stance
        [SerializeField] private Image _iconImage_BalanceTandem;                   // Tandem stance
        [SerializeField] private Image _iconImage_SitStand;                        // Sit-stand test
        [SerializeField] private Image _iconImage_Walk;                            // Walk test

        [Header("Video Section")]
        [SerializeField] private GameObject _videoContainer;
        [SerializeField] private VideoPlayer _videoPlayer;
        [SerializeField] private RawImage _videoDisplay;

        [Header("Teaching Videos (6)")]
        [SerializeField] private VideoClip _video_APoseCalibration;    // A-Pose calibration
        [SerializeField] private VideoClip _video_BalanceSideBySide;   // Side-by-side stance
        [SerializeField] private VideoClip _video_BalanceSemiTandem;   // Semi-tandem stance
        [SerializeField] private VideoClip _video_BalanceTandem;       // Tandem stance
        [SerializeField] private VideoClip _video_SitStand;            // Sit-stand test
        [SerializeField] private VideoClip _video_Walk;                // Walk test

        [Header("Text Section")]
        [SerializeField] private TextMeshProUGUI _instructionText;

        [Header("Buttons")]
        [SerializeField] private Button _startButton;

        [Header("Others")]
        [SerializeField] private Image _logoImage;

        [Header("Video Pop Animation Settings")]
        [SerializeField] private float _videoPopDuration = 0.3f;          // Pop animation duration
        [SerializeField] private float _videoPopMaxScale = 1.0f;          // Final scale (1.0 = original size)

        // Current step
        private FlowStep _currentStep;

        // Video pop animation coroutine
        private Coroutine _videoPopCoroutine;

        // Breathing animation component
        private BreathingAnimator _startButtonBreathing;

        private UnityAction<bool> _nuwaCompleteCallback = null;
        private string _nuwaText = "";

        public override void Initialize()
        {
            base.Initialize();

            if (_startButton != null)
            {
                _startButton.onClick.AddListener(OnStartClicked);

                // Auto-add breathing animation component
                _startButtonBreathing = _startButton.GetComponent<BreathingAnimator>();
                if (_startButtonBreathing == null)
                {
                    _startButtonBreathing = _startButton.gameObject.AddComponent<BreathingAnimator>();
                }
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

            // If video exists and video container is visible, start playing
            if (_videoPlayer != null && _videoContainer != null && _videoContainer.activeSelf)
            {
                _videoPlayer.Play();
            }
        }

        protected override void OnPageExit()
        {
            base.OnPageExit();

            if (_videoPlayer != null)
            {
                _videoPlayer.Stop();
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe events to prevent memory leaks
            if (_startButton != null)
            {
                _startButton.onClick.RemoveListener(OnStartClicked);
            }
        }

        private void ConfigureTTSForStep(FlowStep step)
        {
            _nuwaText = "";
            _nuwaCompleteCallback = null;
            switch (step)
            {
                // ===== Intro Phase (show icon containers) =====
                case FlowStep.TestIntro:
                    _nuwaText = "我們將進行3項測試，不用緊張，請依照語音指示進行操作。";
                    _nuwaCompleteCallback = OnStartClickedAction;
                    break;

                case FlowStep.BalanceIntro:
                    _nuwaText = "第一項是平衡測試，請您嘗試三種不同的站姿，每一種都要維持10秒鐘";
                    _nuwaCompleteCallback = OnStartClickedAction;
                    break;

                case FlowStep.SitStandIntro:
                    _nuwaText = "接下來是坐站測試，請坐在椅子上，雙手交叉抱胸，進行起立坐下動作。請在沒有扶手的情況下，連續站立並坐下5次";
                    _nuwaCompleteCallback = OnStartClickedAction;
                    break;

                case FlowStep.WalkIntro:
                    _nuwaText = "最後是步態速度測試，請您以平常走路的速度，向後方行走3公尺，當我說開始時請出發，直到走到地上的標示終點為止。";
                    _nuwaCompleteCallback = OnStartClickedAction;
                    break;

                // ===== A-Pose Calibration Teaching (video only, no small icon) =====
                case FlowStep.APoseCalibration_Teaching:
                    _nuwaText = "點擊開始後，將進行定位校準流程，以利後續測驗順利進行。";
                    break;

                // ===== Teaching Phase (show video + small icon) =====
                case FlowStep.BalanceSideBySide_Teaching:
                    _nuwaText = "點擊開始後，將進行影片中的測驗內容，請依照內容執行動作!";
                    break;

                case FlowStep.BalanceSemiTandem_Teaching:
                    _nuwaText = "第二種姿勢是半步站立，請將一隻腳稍微往前放，兩腳前後站立。點擊開始後，將進行影片中的測驗內容，請依照內容執行動作!";
                    break;

                case FlowStep.BalanceTandem_Teaching:
                    _nuwaText = "第三種姿勢，腳跟對角尖，請將一隻腳完全放在另一隻腳前面。點擊開始後，將進行影片中的測驗內容，請依照內容執行動作!";
                    break;

                case FlowStep.SitStand_Teaching:
                    _nuwaText = "點擊開始後，將進行影片中的測驗內容，請依照內容執行動作!";
                    break;

                case FlowStep.Walk_Teaching:
                    _nuwaText = "點擊開始後，將進行影片中的測驗內容，請依照內容執行動作!";
                    break;
            }
        }

        /// <summary>
        /// Configure UI elements based on step
        /// </summary>
        private void ConfigureUIForStep(FlowStep step)
        {
            // Hide all dynamic elements first
            HideAllDynamicElements();

            // Show elements based on step
            switch (step)
            {
                // ===== Intro Phase (show icon containers) =====
                case FlowStep.TestIntro:
                    ShowIntroContainer(_iconContainer_TestIntro);
                    break;

                case FlowStep.BalanceIntro:
                    ShowIntroContainer(_iconContainer_BalanceIntro);
                    PlayBalanceIntroAnimation();
                    break;

                case FlowStep.SitStandIntro:
                    ShowIntroContainer(_iconContainer_SitStandIntro);
                    PlaySitStandIntroAnimation();
                    break;

                case FlowStep.WalkIntro:
                    ShowIntroContainer(_iconContainer_WalkIntro);
                    PlayWalkIntroAnimation();
                    break;

                // ===== A-Pose Calibration Teaching (video only, no small icon) =====
                case FlowStep.APoseCalibration_Teaching:
                    ShowVideoOnly(_video_APoseCalibration);
                    break;

                // ===== Teaching Phase (show video + small icon) =====
                case FlowStep.BalanceSideBySide_Teaching:
                    ShowTeaching(_video_BalanceSideBySide, _iconImage_BalanceSideBySide);
                    break;

                case FlowStep.BalanceSemiTandem_Teaching:
                    ShowTeaching(_video_BalanceSemiTandem, _iconImage_BalanceSemiTandem);
                    break;

                case FlowStep.BalanceTandem_Teaching:
                    ShowTeaching(_video_BalanceTandem, _iconImage_BalanceTandem);
                    break;

                case FlowStep.SitStand_Teaching:
                    ShowTeaching(_video_SitStand, _iconImage_SitStand);
                    break;

                case FlowStep.Walk_Teaching:
                    ShowTeaching(_video_Walk, _iconImage_Walk);
                    break;
            }

            // Show start button for all phases
            ShowStartButton();
        }

        /// <summary>
        /// Hide all dynamic elements
        /// </summary>
        private void HideAllDynamicElements()
        {
            // Stop all animations
            StopAllIntroAnimations();

            // Hide intro phase icon containers
            HideAllIntroContainers();

            // Hide teaching phase small icons
            HideAllTeachingIcons();

            // Hide video
            HideVideo();

            // Hide button
            HideStartButton();
        }

        /// <summary>
        /// Stop all intro animations
        /// </summary>
        private void StopAllIntroAnimations()
        {
            if (_balanceIntroAnimator != null)
            {
                _balanceIntroAnimator.Reset();
            }

            if (_sitStandIntroAnimator != null)
            {
                _sitStandIntroAnimator.Reset();
            }

            if (_walkIntroAnimator != null)
            {
                _walkIntroAnimator.Stop();
            }
        }

        /// <summary>
        /// Play balance test intro animation
        /// </summary>
        private void PlayBalanceIntroAnimation()
        {
            if (_balanceIntroAnimator != null)
            {
                _balanceIntroAnimator.Play();
            }
        }

        /// <summary>
        /// Play sit-stand test intro animation
        /// </summary>
        private void PlaySitStandIntroAnimation()
        {
            if (_sitStandIntroAnimator != null)
            {
                _sitStandIntroAnimator.Play();
            }
        }

        /// <summary>
        /// Play walk test intro animation
        /// </summary>
        private void PlayWalkIntroAnimation()
        {
            if (_walkIntroAnimator != null)
            {
                _walkIntroAnimator.Play();
            }
        }

        /// <summary>
        /// Hide all intro phase icon containers
        /// </summary>
        private void HideAllIntroContainers()
        {
            SetGameObjectActive(_iconContainer_TestIntro, false);
            SetGameObjectActive(_iconContainer_BalanceIntro, false);
            SetGameObjectActive(_iconContainer_SitStandIntro, false);
            SetGameObjectActive(_iconContainer_WalkIntro, false);
        }

        /// <summary>
        /// Show intro phase container
        /// </summary>
        private void ShowIntroContainer(GameObject container)
        {
            SetGameObjectActive(container, true);
        }

        /// <summary>
        /// Show teaching phase (video + small icon)
        /// </summary>
        private void ShowTeaching(VideoClip clip, Image iconImage)
        {
            ShowVideo(clip);
            ShowTeachingIcon(iconImage);
        }

        /// <summary>
        /// Show video only (no small icon, for A-Pose calibration teaching)
        /// </summary>
        private void ShowVideoOnly(VideoClip clip)
        {
            ShowVideo(clip);
        }

        /// <summary>
        /// Show teaching phase small icon
        /// </summary>
        private void ShowTeachingIcon(Image iconImage)
        {
            if (iconImage != null)
            {
                iconImage.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Hide all teaching phase small icons
        /// </summary>
        private void HideAllTeachingIcons()
        {
            SetImageActive(_iconImage_BalanceSideBySide, false);
            SetImageActive(_iconImage_BalanceSemiTandem, false);
            SetImageActive(_iconImage_BalanceTandem, false);
            SetImageActive(_iconImage_SitStand, false);
            SetImageActive(_iconImage_Walk, false);
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
        /// Set GameObject active state
        /// </summary>
        private void SetGameObjectActive(GameObject obj, bool active)
        {
            if (obj != null)
            {
                obj.SetActive(active);
            }
        }

        /// <summary>
        /// Show video and set clip (with pop animation)
        /// </summary>
        private void ShowVideo(VideoClip clip)
        {
            if (_videoContainer != null)
            {
                _videoContainer.SetActive(true);

                // Stop previous animation
                if (_videoPopCoroutine != null)
                {
                    StopCoroutine(_videoPopCoroutine);
                }

                // Play pop animation
                _videoPopCoroutine = StartCoroutine(VideoPopAnimation());
            }

            if (_videoDisplay != null)
            {
                _videoDisplay.gameObject.SetActive(true);
            }

            if (_videoPlayer != null && clip != null)
            {
                _videoPlayer.clip = clip;
            }
        }

        /// <summary>
        /// Video pop animation coroutine
        /// </summary>
        private IEnumerator VideoPopAnimation()
        {
            if (_videoContainer == null) yield break;

            Transform videoTransform = _videoContainer.transform;

            // Set scale to 0 first (ensure animation starts from beginning)
            videoTransform.localScale = Vector3.zero;

            // Wait one frame to ensure scale is applied
            yield return null;

            // Scale from 0 to target scale (EaseOutQuad easing)
            float elapsed = 0f;
            while (elapsed < _videoPopDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / _videoPopDuration);

                // Use EaseOutQuad easing
                float easedProgress = EaseOutQuad(progress);
                float currentScale = easedProgress * _videoPopMaxScale;
                videoTransform.localScale = Vector3.one * currentScale;

                yield return null;
            }

            // Ensure scale is at target value
            videoTransform.localScale = Vector3.one * _videoPopMaxScale;
        }

        /// <summary>
        /// EaseOutQuad easing function
        /// </summary>
        private float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }

        /// <summary>
        /// Hide video
        /// </summary>
        private void HideVideo()
        {
            // Stop pop animation
            if (_videoPopCoroutine != null)
            {
                StopCoroutine(_videoPopCoroutine);
                _videoPopCoroutine = null;
            }

            if (_videoContainer != null)
            {
                // Reset scale to target value
                _videoContainer.transform.localScale = Vector3.one * _videoPopMaxScale;
                _videoContainer.SetActive(false);
            }

            if (_videoDisplay != null)
            {
                _videoDisplay.gameObject.SetActive(false);
            }

            if (_videoPlayer != null)
            {
                _videoPlayer.Stop();
                _videoPlayer.clip = null;
            }
        }

        /// <summary>
        /// Hide start button
        /// </summary>
        private void HideStartButton()
        {
            if (_startButton != null)
            {
                _startButton.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Show start button
        /// </summary>
        private void ShowStartButton()
        {
            if (_startButton != null)
            {
                _startButton.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Start button clicked
        /// </summary>
        private void OnStartClicked()
        {
            UIManager.Instance.NextStep();
        }

        private void OnStartClickedAction(bool value)
        {
            OnStartClicked();
        }

        /// <summary>
        /// Configure top bar
        /// TeachingPage: Title frame + Title + Dialog + Exit
        /// </summary>
        protected override void ConfigureTopBar()
        {
            TopBarManager.Instance.HideAll();
            TopBarManager.Instance.ShowTitleFrame(true);
            TopBarManager.Instance.ShowTitle(true);
            TopBarManager.Instance.ShowDialog(true);
            TopBarManager.Instance.ShowExitButton(true);
            TopBarManager.Instance.SetDefaultFrame();

            // Set title and dialog based on current step
            Sprite titleSprite = GetTitleSpriteForStep(_currentStep);
            string dialogText = GetDialogTextForStep(_currentStep);

            TopBarManager.Instance.SetTitleImage(titleSprite);
            TopBarManager.Instance.SetDialogTextWithAnimation(dialogText);
        }

        /// <summary>
        /// Get title sprite for current step
        /// </summary>
        private Sprite GetTitleSpriteForStep(FlowStep step)
        {
            switch (step)
            {
                case FlowStep.TestIntro:
                    return _testIntroTitleSprite;

                case FlowStep.APoseCalibration_Teaching:
                    return _aPoseCalibrationTitleSprite;

                case FlowStep.BalanceIntro:
                case FlowStep.BalanceSideBySide_Teaching:
                case FlowStep.BalanceSemiTandem_Teaching:
                case FlowStep.BalanceTandem_Teaching:
                    return _balanceTitleSprite;

                case FlowStep.SitStandIntro:
                case FlowStep.SitStand_Teaching:
                    return _sitStandTitleSprite;

                case FlowStep.WalkIntro:
                case FlowStep.Walk_Teaching:
                    return _walkTitleSprite;

                default:
                    return null;
            }
        }

        /// <summary>
        /// Get dialog text for current step
        /// </summary>
        private string GetDialogTextForStep(FlowStep step)
        {
            switch (step)
            {
                case FlowStep.TestIntro:
                    return "我們將進行3項測試，不用緊張，請依照語音指示進行操作";

                case FlowStep.APoseCalibration_Teaching:
                    return "稍後將進行定位校準，請依指示進行操作";

                case FlowStep.BalanceIntro:
                    return "第一項是平衡測驗，請您嘗試三種不同的站姿，並維持10秒鐘";

                case FlowStep.BalanceSideBySide_Teaching:
                    return "雙腳併攏站立，請站好並保持平衡，當我說開始後請維持不動";

                case FlowStep.BalanceSemiTandem_Teaching:
                    return "第二種姿勢，半步站立，請將一隻腳稍微往前放，兩腳前後站立";

                case FlowStep.BalanceTandem_Teaching:
                    return "第三種姿勢，腳跟對角尖，請將一隻腳完全放在另一隻腳前面";

                case FlowStep.SitStandIntro:
                    return "請坐在椅子上，雙手交叉抱胸，連續站立並坐下5次";

                case FlowStep.SitStand_Teaching:
                    return "請坐在椅子上，雙手交叉抱胸，連續站立並坐下5次";

                case FlowStep.WalkIntro:
                    return "請以平常走路的速度，向後方行走3公尺";

                case FlowStep.Walk_Teaching:
                    return "請以平常走路的速度，向後方行走3公尺";

                default:
                    return "";
            }
        }
    }
}
