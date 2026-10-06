using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SPPB.Core;
using SPPB.Core.Detection;
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
        [Tooltip("僅在未接 SppbTestRunner 時作為後備；實際以 SppbThresholds.balanceHoldDuration 為準")]
        [SerializeField] private float _balanceTestDuration = 10f;
        /// <summary>平衡撐滿秒數：統一取判定門檻，避免場景值(曾設 12)與判定器不一致。</summary>
        private float BalanceDuration => motionSDKClient != null ? motionSDKClient.BalanceHoldDuration : _balanceTestDuration;
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
        [SerializeField] public SppbTestRunner motionSDKClient;

        [Header("Walk Progress Bar")]
        [SerializeField] private GameObject _walkProgressBarRoot;
        [SerializeField] private Image _walkProgressBarFill;
        [SerializeField] private TextMeshProUGUI _walkDistanceText;

        // Current step
        private FlowStep _currentStep;

        [Header("重新定位 (測驗中人物離開畫面)")]
        [Tooltip("連續多久偵測不到人視為離開畫面")]
        [SerializeField] private float _personLostTimeout = 0.8f;
        [Tooltip("回到畫面後需連續穩定多久才算重新定位完成")]
        [SerializeField] private float _repositionStableTime = 1.0f;
        [Tooltip("重新定位成功圖顯示秒數，之後 3-2-1 倒數續測")]
        [SerializeField] private float _repositionSuccessDisplay = 1.5f;
        [Tooltip("暫停中人一直沒回來時，每隔幾秒語音提醒一次")]
        [SerializeField] private float _repositionRemindInterval = 8f;
        private float _repositionRemindTimer = 0f;
        private Coroutine _repositionCoroutine;
        [Tooltip("外框高度至少要達校準時身形的多少比例才算真人（排除 VideoPose 抓到的小型類人物件）")]
        [SerializeField, Range(0.1f, 1f)] private float _personMinSizeRatio = 0.5f;
        [Tooltip("keyPoints 平均信心值下限")]
        [SerializeField, Range(0f, 1f)] private float _personMinConfidence = 0.4f;

        [Header("A-pose 校準")]
        [Tooltip("VideoPose 抓到 A-pose(開始吐骨架)後，需穩定收到多少幀才算成功")]
        [SerializeField] private int _aposeMinPoseFrames = 5;
        [Tooltip("需連續維持 A-pose 多久才算校準成功")]
        [SerializeField] private float _aposeHoldTime = 2.0f;
        [Tooltip("用骨架角度檢查 A-pose。VideoPose 原生偵測太寬鬆(手沒張開也會過)，預設開啟")]
        [SerializeField] private bool _useStrictAPoseCheck = true;
        [Tooltip("上臂張開角度下限(相對身體往下方向)。實測手垂下約 6~21°")]
        [SerializeField] private float _aposeArmMinDeg = 30f;
        [Tooltip("上臂張開角度上限。T-pose 約 90°")]
        [SerializeField] private float _aposeArmMaxDeg = 80f;
        [Tooltip("手肘/膝蓋需打直的最小角度（實測手臂伸直約 143~152°）")]
        [SerializeField] private float _aposeLimbStraightMinDeg = 130f;
        private float _aposeHoldTimer = 0f;
        private float _aposeLogTimer = 0f;
        private float _personRefBoxHeight = 0f;   // 校準成功時的身形外框高度，當作「有效人物」基準
        private Vector3[] _aposeJoints;

        // Test state
        private TestState _testState = TestState.Idle;
        private float _timer = 0f;
        private float _balanceSafetyTimer = 0f;
        private bool _balanceFailed = false;
        // 重新定位狀態
        private bool _repositionPaused = false;
        private bool _repositionSuccessShowing = false;
        private float _repositionStableTimer = 0f;
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
            // 進定位校準關才開始追蹤：重置追蹤器，等 VideoPose 偵測到 A-pose 才吐骨架（避免首頁就被判定）。
            // 之後各關不再重置，以免測驗途中骨架中斷。回首頁時由 UIManager.GoHome 停止追蹤。
            if (videoPoseTest != null && step == FlowStep.APoseCalibration)
                videoPoseTest.BeginAPoseCalibration();
            if (step == FlowStep.APoseCalibration) ResetCalibrationGuidance();
            // 步行、坐站時關閉身分比對（改靠區域遮罩與位置跳動）：
            //  - 步行：走近鏡頭只剩局部身體，衣服顏色取樣不可靠
            //  - 坐站：蹲下時上衣被遮/變暗、大腿變水平取到椅子背景，實測一輪誤判其他人 6 次
            if (videoPoseTest != null && videoPoseTest.subject != null)
                videoPoseTest.subject.IdentityCheckEnabled = step != FlowStep.Walk_Test && step != FlowStep.SitStand_Test;
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
            // A-pose 校準：進校準關時已重置追蹤器(detect_apose)，VideoPose 抓到 A-pose 才會開始吐骨架。
            // 語音講完後，重置後收到足夠骨架幀並持續有人 _aposeHoldTime 秒 → 成功。
            if (!_isCalibrate && _currentStep == FlowStep.APoseCalibration)
            {
                bool videoPoseLocked = videoPoseTest != null
                    && videoPoseTest.poseCountSinceReset >= _aposeMinPoseFrames
                    && videoPoseTest.IsPersonPresent(0.5f);
                bool strictOk = !_useStrictAPoseCheck || IsHoldingAPose();

                // 必須全身關節都在畫面內才算（避免離太近、腳是猜出來的也通過）
                if (videoPoseLocked && strictOk && _guideFullBody)
                {
                    _aposeHoldTimer += Time.deltaTime;
                    ShowAposeHoldProgress(Mathf.Clamp01(_aposeHoldTimer / _aposeHoldTime));
                    if (_aposeHoldTimer >= _aposeHoldTime)
                    {
                        OnAPoseCalibrationComplete();
                        return;
                    }
                }
                else
                {
                    _aposeHoldTimer = 0f;
                    ShowAposeHoldProgress(0f);
                }
            }

            if (!_isCalibrate && _currentStep == FlowStep.APoseCalibration)
                UpdateCalibrationGuidance();

            // Test feature: press P to increment sit-stand count
            if (_currentStep == FlowStep.SitStand_Test && _testState == TestState.Testing && Input.GetKeyDown(KeyCode.P))
            {
                IncrementSitStandCount();
            }

            HandleRepositionCheck();
            UpdateSubjectSkeleton();

            // 頭部追蹤：測驗進行中凍結（轉頭會改變相機座標，影響腳距/步行深度判定）；暫停重新定位時放開讓頭找人
            if (videoPoseTest != null && videoPoseTest.headTracker != null)
                videoPoseTest.headTracker.Frozen = _testState == TestState.Testing && !_repositionPaused && !_repositionSuccessShowing;

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

        /// <summary>目前是否有「真人」在畫面：有姿勢回傳 + 外框夠大 + 信心值夠高（排除類人物件的假骨架）。</summary>
        private bool IsTrackedPersonPresent(float timeout)
        {
            if (videoPoseTest == null) return false;
            float minH = _personRefBoxHeight > 0f ? _personRefBoxHeight * _personMinSizeRatio : 0f;
            return videoPoseTest.IsValidPerson(timeout, minH, _personMinConfidence);
        }

        private enum Presence { Ok, Lost, OtherPerson }

        /// <summary>受測者狀態：鎖定後以 SubjectLock 身分比對為準；鎖定前退回外框/信心值判斷。</summary>
        private Presence GetPresence(float lostTimeout)
        {
            var subject = videoPoseTest != null ? videoPoseTest.subject : null;
            if (subject != null && subject.IsLocked)
            {
                switch (subject.CurrentState)
                {
                    case SubjectLock.State.Tracking: return Presence.Ok;
                    case SubjectLock.State.OtherPerson: return Presence.OtherPerson;
                    default: return Presence.Lost;
                }
            }
            return IsTrackedPersonPresent(lostTimeout) ? Presence.Ok : Presence.Lost;
        }

        /// <summary>目前骨架是否為 A-pose（且為有效人物）。每 0.5s 印出不通過原因方便調參。</summary>
        private bool IsHoldingAPose()
        {
            if (videoPoseTest == null || videoPoseTest.videoPoseAvatar == null) return false;
            var pose = videoPoseTest.videoPoseAvatar.pose;
            bool ok = false;
            string reason;
            _aposeHint = "";

            if (!videoPoseTest.IsValidPerson(0.5f, 0f, _personMinConfidence))
                reason = $"未偵測到有效人物(信心值 {videoPoseTest.lastMeanConfidence:F2})";
            else if (pose == null || !SppbJoints.TryExtract(pose.globalTransforms, ref _aposeJoints))
                reason = "無骨架";
            else
                ok = APoseCheck.IsAPose(_aposeJoints, _aposeArmMinDeg, _aposeArmMaxDeg, _aposeLimbStraightMinDeg, out reason, out _aposeHint);

            _aposeLogTimer += Time.deltaTime;
            if (!ok && _aposeLogTimer >= 0.5f)
            {
                _aposeLogTimer = 0f;
                Debug.Log($"[APose] 未通過：{reason}");
            }
            _aposeOk = ok;
            return ok;
        }

        #region A-pose 校準引導

        // 校準時依骨架給受測者具體提示（遠近/全身入鏡/置中/手臂姿勢），只用語音講（上方文字維持簡短版以免爆版）。
        // 注意：detect_apose 下 VideoPose 要先認到 A-pose 才吐骨架，之前沒有關節可判斷，只能給一般提示。
        [Header("A-pose 校準引導")]
        [SerializeField] private float _guideStableSec = 1.2f;       // 提示持續這麼久才換上（避免閃爍）
        [SerializeField] private float _guideSpeakInterval = 7f;     // 語音提示最短間隔（講話時凱比會搶頭部馬達，別太頻繁）
        [SerializeField] private float _guideFramingPersistSec = 3f; // 頭/腳出畫面持續多久才請受測者移動（先讓頭部追蹤自己調）
        private string _aposeHint = "";
        private bool _aposeOk;
        private bool _guideFullBody;   // 全身關節都抓到（校準通過的必要條件）
        private DwellRing _aposeHoldRing;
        private string _guideShown = "", _guideCandidate = "", _guideLastSpoken = "";
        private float _guideCandidateTimer, _guideSpeakTimer, _guideFramingBadTimer, _guideOffCenterTimer, _guideLogTimer;
        [SerializeField] private float _guideMinLegTorsoRatio = 0.9f;   // 腿(髖→踝)/軀幹(頸→髖) 低於此 = 腳被切掉、是猜的

        private const string GuideNoPerson = "請站到凱比正前方約 2 到 3 公尺，讓頭到腳都在畫面中，並站直、雙手往兩側斜下方張開成 A 字形";
        private const string GuideTooClose = "太近了，請往後退幾步，雙手保持往兩側張開";
        private const string GuideNotFull = "請往後退一點，讓頭到腳都在畫面內，雙手保持往兩側張開";
        private const string GuideTooFar = "有點遠，請往前走近一點，雙手保持往兩側張開";
        private const string GuideCenter = "請往畫面中間移動一點，雙手保持往兩側張開";
        private const string GuideHeadOut = "請往後退一點，讓頭也在畫面內，雙手保持往兩側張開";
        private const string GuideFeetOut = "請往後退一點，讓腳也在畫面內，雙手保持往兩側張開";
        private const string GuideArmsOut = "請站到畫面中間，雙手張開但不要超出畫面";
        private const string GuideArms = "位置很好，請站直，雙手往兩側斜下方張開成 A 字形";
        private const string GuideHold = "很好，請保持這個姿勢不要動";

        private void ResetCalibrationGuidance()
        {
            _aposeHint = _guideShown = _guideCandidate = _guideLastSpoken = "";
            _aposeOk = false;
            _guideFullBody = false;
            ShowAposeHoldProgress(0f);
            _guideCandidateTimer = _guideFramingBadTimer = _guideOffCenterTimer = 0f;
            _guideSpeakTimer = _guideSpeakInterval;   // 第一句提示穩定後就可以講
        }

        private void UpdateCalibrationGuidance()
        {
            float dt = Time.deltaTime;
            _guideSpeakTimer += dt;
            string hint = ComputeCalibrationHint(dt);
            if (string.IsNullOrEmpty(hint)) return;

            // 穩定一段時間才換提示
            if (hint != _guideCandidate) { _guideCandidate = hint; _guideCandidateTimer = 0f; }
            else _guideCandidateTimer += dt;
            // 「保持不動」要馬上出現（維持只需 _aposeHoldTime 秒），其他提示要穩定一段時間才換
            float stable = hint == GuideHold ? 0f : _guideStableSec;
            if (_guideCandidateTimer < stable || hint == _guideShown) return;

            _guideShown = hint;
            // 上方文字維持原本簡短的校準說明（提示句太長會爆版），詳細提示只用語音
            Debug.Log($"[APose][引導] {hint}");

            // 語音：同一句不重複講、間隔夠久才講
            // 「保持不動」一出現就講；其他提示同一句不重複、間隔夠久才講（講話時凱比會轉頭）
            if (hint == GuideHold || (hint != _guideLastSpoken && _guideSpeakTimer >= _guideSpeakInterval))
            {
                _guideLastSpoken = hint;
                _guideSpeakTimer = 0f;
                NuwaManager.Instance.NuwaTTS(hint);
            }
        }

        private string ComputeCalibrationHint(float dt)
        {
            _guideFullBody = false;
            var subject = videoPoseTest != null ? videoPoseTest.subject : null;
            bool hasPose = videoPoseTest != null && videoPoseTest.poseCountSinceReset > 0 && videoPoseTest.IsPersonPresent(0.5f)
                           && subject != null && subject.Keypoints != null && subject.Keypoints.Length >= 25 && subject.PoseAge < 0.5f;
            if (!hasPose)
            {
                _guideFramingBadTimer = _guideOffCenterTimer = 0f;
                return GuideNoPerson;
            }

            // BODY_25 關節（影像像素，y 向下，z=信心值）
            var k = subject.Keypoints;
            float W = subject.ImageWidth, H = subject.ImageHeight;
            const float minConf = 0.55f, edge = 0.03f;
            bool Ok(int i) => k[i].z >= minConf;
            bool In(int i) => Ok(i) && k[i].x > W * edge && k[i].x < W * (1f - edge) && k[i].y > H * edge && k[i].y < H * (1f - edge);

            // 「位置很好」＝身體所有關節點都抓到且在畫面內（眼/耳 15~18 不算：側臉、頭髮遮住本來就抓不到）
            bool headMissing = !In(0) || !In(1);
            bool armsMissing = false; for (int i = 2; i <= 7; i++) armsMissing |= !In(i);
            bool hipsMissing = !In(8) || !In(9) || !In(12);
            bool legsMissing = !In(10) || !In(13) || !In(11) || !In(14);
            bool feetMissing = false; for (int i = 19; i <= 24; i++) feetMissing |= !In(i);

            // 太近時 VideoPose 會在畫面內「猜」出腳點且信心值不低 → 用腿長比例擋：
            // 真的看到腳時「髖→腳踝」應 ≥ 0.9 倍「頸→髖」，腿被切掉的猜測點會短很多
            float torso = In(1) && In(8) ? k[8].y - k[1].y : 0f;
            float leg = In(8) && In(11) && In(14) ? (k[11].y + k[14].y) * 0.5f - k[8].y : 0f;
            float legRatio = torso > 1f ? leg / torso : 0f;
            bool legsTooShort = torso > 1f && legRatio < _guideMinLegTorsoRatio;
            bool lowerMissing = hipsMissing || legsMissing || feetMissing || legsTooShort;

            _guideLogTimer += dt;
            if (_guideLogTimer >= 1f)
            {
                _guideLogTimer = 0f;
                var miss = new System.Text.StringBuilder();
                for (int i = 0; i <= 24; i++) if ((i < 15 || i > 18) && !In(i)) miss.Append(i).Append(' ');
                Debug.Log($"[APose][引導] 缺關節: {(miss.Length > 0 ? miss.ToString() : "無")} 腿/軀幹比={legRatio:F2} A-pose={(_aposeOk ? "通過" : "未過")}");
            }

            // 頭和下半身都缺 → 太近
            if (headMissing && lowerMissing) { _guideFramingBadTimer = 0f; return GuideTooClose; }
            // 只缺頭或只缺下半身：先讓頭部追蹤轉頭去找，持續一段時間仍不行才請受測者後退
            if (headMissing || lowerMissing)
            {
                _guideFramingBadTimer += dt;
                if (_guideFramingBadTimer >= _guideFramingPersistSec)
                    return headMissing ? GuideHeadOut : GuideFeetOut;
                return string.IsNullOrEmpty(_guideCandidate) || _guideCandidate == GuideArms || _guideCandidate == GuideHold
                    ? GuideNotFull : _guideCandidate;
            }
            _guideFramingBadTimer = 0f;
            if (armsMissing) return GuideArmsOut;
            _guideFullBody = true;

            float top = float.MaxValue, bot = float.MinValue;
            for (int i = 0; i < 25; i++) if (Ok(i)) { top = Mathf.Min(top, k[i].y); bot = Mathf.Max(bot, k[i].y); }
            float heightRatio = bot > top ? (bot - top) / H : 0f;
            if (heightRatio > 0f && heightRatio < 0.4f) return GuideTooFar;

            float cx = k[1].x / W;
            _guideOffCenterTimer = Mathf.Abs(cx - 0.5f) > 0.25f ? _guideOffCenterTimer + dt : 0f;
            if (_guideOffCenterTimer >= _guideFramingPersistSec) return GuideCenter;

            if (_aposeOk) return GuideHold;
            if (!string.IsNullOrEmpty(_aposeHint)) return _aposeHint;
            return GuideArms;   // 位置 OK 但 A-pose 還沒判定通過（含骨架尚未完整）
        }

        /// <summary>A-pose 維持進度圈（0 = 隱藏）。放在人型框下方。</summary>
        private void ShowAposeHoldProgress(float progress01)
        {
            if (progress01 <= 0f)
            {
                if (_aposeHoldRing != null) _aposeHoldRing.Hide();
                return;
            }
            if (_aposeHoldRing == null)
            {
                Transform parent = _aPoseFrame != null ? _aPoseFrame.transform : transform;
                var go = new GameObject("APoseHoldRing", typeof(RectTransform));
                go.transform.SetParent(parent, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                _aposeHoldRing = go.AddComponent<DwellRing>();
                _aposeHoldRing.size = 160f;
                _aposeHoldRing.thickness = 0.16f;
                _aposeHoldRing.fillColor = new Color(0.25f, 0.9f, 0.45f, 0.95f);
                _aposeHoldRing.backColor = new Color(1f, 1f, 1f, 0.3f);
            }
            _aposeHoldRing.transform.SetAsLastSibling();
            _aposeHoldRing.Show(progress01);
        }

        #endregion

        /// <summary>是否為需要「人物在場」的實際測驗關（平衡/坐站/步行，校準與教學不算）。</summary>
        private bool IsRealTestStep(FlowStep step)
        {
            return step == FlowStep.BalanceSideBySide_Test
                || step == FlowStep.BalanceSemiTandem_Test
                || step == FlowStep.BalanceTandem_Test
                || step == FlowStep.SitStand_Test
                || step == FlowStep.Walk_Test;
        }

        /// <summary>測驗中人物離開畫面 → 暫停 + 提示重新定位；回到畫面穩定後 → 校準成功圖 → 續測。</summary>
        private void HandleRepositionCheck()
        {
            if (videoPoseTest == null) return;
            bool inActiveTest = _testState == TestState.Testing && IsRealTestStep(_currentStep);
            // 步行整段不觸發重新定位：走向鏡頭時頭手腳一定會出框，暫停會打斷計時
            // （原本用「已開始前進」判斷，但 VideoPose 深度幾乎不隨前進變化，擋不住）
            if (_currentStep == FlowStep.Walk_Test)
                inActiveTest = false;

            if (_repositionSuccessShowing) return; // 正在顯示成功圖，等 Invoke 續測

            if (!_repositionPaused)
            {
                if (!inActiveTest) { _presenceBadTimer = 0f; return; }
                Presence p = GetPresence(_personLostTimeout);
                if (p == Presence.OtherPerson)
                {
                    EnterRepositionPause(p);   // 身分比對已有確認時間，偵測到其他人立即暫停
                }
                else if (p == Presence.Lost)
                {
                    _presenceBadTimer += Time.deltaTime;
                    if (_presenceBadTimer >= _personLostTimeout) EnterRepositionPause(p);
                }
                else
                {
                    _presenceBadTimer = 0f;
                }
            }
            else
            {
                // 暫停中：等「確認是受測者」並連續穩定（同時更新提示文字/語音提醒）
                Presence p = GetPresence(0.3f);
                UpdateRepositionPrompt(p);
                if (p == Presence.Ok)
                {
                    _repositionStableTimer += Time.deltaTime;
                    if (_repositionStableTimer >= _repositionStableTime)
                        CompleteReposition();
                }
                else
                {
                    _repositionStableTimer = 0f;
                }
            }
        }

        private const string RepositionPauseText = "測驗暫停：偵測不到您，請回到畫面中央，對準人形框站好";
        private const string RepositionPauseSpeech = "偵測不到您，測驗先暫停，請回到畫面中央，對準人形框站好";
        private const string OtherPersonText = "測驗暫停：偵測到其他人（紅色骨架）。請旁人離開鏡頭範圍，受測者請站回人形框";
        private const string OtherPersonSpeech = "偵測到其他人，測驗先暫停。請旁邊的人離開鏡頭範圍，受測者請站回人形框";
        private const string SubjectConfirmedText = "已確認是受測者（綠色骨架），請站穩不要動";

        private Presence _repositionShownStatus;
        private Presence _repositionReason;
        private float _presenceBadTimer = 0f;

        private void EnterRepositionPause(Presence reason)
        {
            _repositionPaused = true;
            _repositionStableTimer = 0f;
            _repositionRemindTimer = 0f;
            _presenceBadTimer = 0f;
            _repositionReason = reason;
            _repositionShownStatus = reason;

            if (motionSDKClient != null) motionSDKClient.paused = true; // 凍結判定 elapsed

            // 清掉測驗中的動作提示，顯示暫停圖示 + 人形定位框 + 文字 + 語音
            HideCorrectHint();
            ShowPauseIcon(true);
            if (_aPoseFrame != null) _aPoseFrame.SetActive(true);
            bool other = reason == Presence.OtherPerson;
            SetRepositionDialog(other ? OtherPersonText : RepositionPauseText);
            NuwaManager.Instance.NuwaTTS(other ? OtherPersonSpeech : RepositionPauseSpeech);

            Debug.Log(other ? "[Reposition] 偵測到其他人 → 暫停測驗" : "[Reposition] 人物離開畫面 → 暫停測驗，等待重新定位");
        }

        /// <summary>暫停中每幀更新：依狀態切換提示（離開／其他人／已確認受測者），沒解決就定期語音提醒。</summary>
        private void UpdateRepositionPrompt(Presence p)
        {
            if (p != _repositionShownStatus)
            {
                _repositionShownStatus = p;
                _repositionRemindTimer = 0f;
                switch (p)
                {
                    case Presence.Ok:
                        SetRepositionDialog(SubjectConfirmedText);
                        break;
                    case Presence.OtherPerson:
                        SetRepositionDialog(OtherPersonText);
                        NuwaManager.Instance.NuwaTTS(OtherPersonSpeech);   // 狀態變成「其他人」時立即說明
                        break;
                    default:
                        SetRepositionDialog(RepositionPauseText);
                        break;
                }
            }

            if (p != Presence.Ok)
            {
                _repositionRemindTimer += Time.deltaTime;
                if (_repositionRemindTimer >= _repositionRemindInterval)
                {
                    _repositionRemindTimer = 0f;
                    NuwaManager.Instance.NuwaTTS(p == Presence.OtherPerson ? OtherPersonSpeech : RepositionPauseSpeech);
                }
            }
        }

        // ---------------- 受測者骨架（白=校準中、綠=受測者、紅=其他人） ----------------

        [Header("骨架顯示")]
        [Tooltip("在相機畫面上畫出偵測到的骨架：白=校準中、綠=確認是受測者、紅=偵測到其他人")]
        [SerializeField] private bool _showSkeleton = true;
        [SerializeField] private float _skeletonJointSize = 8f;
        [SerializeField] private float _skeletonBoneThickness = 3f;
        [Tooltip("是否畫骨頭連線（預設只畫關節點）")]
        [SerializeField] private bool _skeletonShowBones = false;
        [Tooltip("關鍵點信心值低於此值不畫")]
        [SerializeField] private float _skeletonMinConfidence = 0.4f;

        // OpenPose BODY_25 骨頭連線
        private static readonly int[,] Body25Bones =
        {
            {1, 8}, {1, 2}, {2, 3}, {3, 4}, {1, 5}, {5, 6}, {6, 7},
            {8, 9}, {9, 10}, {10, 11}, {8, 12}, {12, 13}, {13, 14},
            {1, 0}, {0, 15}, {15, 17}, {0, 16}, {16, 18},
            {14, 19}, {19, 20}, {14, 21}, {11, 22}, {22, 23}, {11, 24},
        };
        private const int Body25Count = 25;

        private RectTransform _skeletonRoot;
        private Image[] _skeletonJoints;
        private Image[] _skeletonBones;
        private Sprite _dotSprite;

        private void UpdateSubjectSkeleton()
        {
            var subject = videoPoseTest != null ? videoPoseTest.subject : null;
            bool show = _showSkeleton && subject != null && _cameraDisplay != null
                        && subject.ImageWidth > 0 && subject.PoseAge < 0.5f
                        && subject.Keypoints != null && subject.Keypoints.Length >= Body25Count
                        && subject.CurrentState != SubjectLock.State.Lost;
            if (!show) { ShowSkeleton(false); return; }
            if (_skeletonRoot == null) CreateSkeleton();

            Color c;
            switch (subject.CurrentState)
            {
                case SubjectLock.State.Tracking: c = new Color(0.25f, 0.9f, 0.45f, 0.6f); break;
                case SubjectLock.State.OtherPerson: c = new Color(1f, 0.3f, 0.3f, 0.7f); break;
                default: c = new Color(1f, 1f, 1f, 0.55f); break;   // 校準中尚未鎖定
            }

            // 影像像素(y 向下) → 相機 RawImage 內座標（RawImage 已上下翻轉，影像 y=0 顯示在最上方）
            var k = subject.Keypoints;
            Rect r = _cameraDisplay.rectTransform.rect;
            float w = subject.ImageWidth, h = subject.ImageHeight;
            Vector2 ToLocal(Vector3 p) => new Vector2(p.x / w * r.width, (1f - p.y / h) * r.height);
            bool Visible(int i) => k[i].z >= _skeletonMinConfidence;

            for (int i = 0; i < Body25Count; i++)
            {
                var img = _skeletonJoints[i];
                bool v = Visible(i);
                img.enabled = v;
                if (!v) continue;
                img.rectTransform.anchoredPosition = ToLocal(k[i]);
                img.color = c;
            }
            for (int b = 0; b < _skeletonBones.Length; b++)
            {
                int i0 = Body25Bones[b, 0], i1 = Body25Bones[b, 1];
                var img = _skeletonBones[b];
                bool v = _skeletonShowBones && Visible(i0) && Visible(i1);
                img.enabled = v;
                if (!v) continue;
                Vector2 p0 = ToLocal(k[i0]), p1 = ToLocal(k[i1]);
                Vector2 d = p1 - p0;
                var rt = img.rectTransform;
                rt.anchoredPosition = p0;
                rt.sizeDelta = new Vector2(d.magnitude, _skeletonBoneThickness);
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                img.color = new Color(c.r, c.g, c.b, c.a * 0.7f);
            }
            ShowSkeleton(true);
        }

        private void ShowSkeleton(bool show)
        {
            if (_skeletonRoot != null && _skeletonRoot.gameObject.activeSelf != show)
                _skeletonRoot.gameObject.SetActive(show);
        }

        private void CreateSkeleton()
        {
            var root = new GameObject("SubjectSkeleton", typeof(RectTransform));
            root.transform.SetParent(_cameraDisplay.transform, false);
            _skeletonRoot = (RectTransform)root.transform;
            _skeletonRoot.anchorMin = Vector2.zero;
            _skeletonRoot.anchorMax = Vector2.one;
            _skeletonRoot.offsetMin = _skeletonRoot.offsetMax = Vector2.zero;
            _dotSprite = CreateDotSprite();

            // 骨頭先建（在下層），關節點蓋在上面
            _skeletonBones = new Image[Body25Bones.GetLength(0)];
            for (int b = 0; b < _skeletonBones.Length; b++)
                _skeletonBones[b] = CreateSkeletonPart("Bone" + b, root.transform, new Vector2(0f, 0.5f), null, Vector2.zero);

            _skeletonJoints = new Image[Body25Count];
            for (int i = 0; i < Body25Count; i++)
                _skeletonJoints[i] = CreateSkeletonPart("Joint" + i, root.transform, new Vector2(0.5f, 0.5f), _dotSprite,
                                                        new Vector2(_skeletonJointSize, _skeletonJointSize));
            root.SetActive(false);
        }

        private static Image CreateSkeletonPart(string name, Transform parent, Vector2 pivot, Sprite sprite, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;   // 左下角為原點
            rt.pivot = pivot;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>程式產生的圓點貼圖（關節點用）。</summary>
        private static Sprite CreateDotSprite()
        {
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float rad = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(rad, rad));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(rad - d)));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        private GameObject _pauseIcon;

        /// <summary>顯示/隱藏畫面中央的暫停圖示（程式產生，不需額外圖檔）。</summary>
        private void ShowPauseIcon(bool show)
        {
            if (show && _pauseIcon == null) _pauseIcon = CreatePauseIcon();
            if (_pauseIcon == null) return;
            _pauseIcon.SetActive(show);
            if (show) _pauseIcon.transform.SetAsLastSibling(); // 蓋在最上層
        }

        /// <summary>半透明深色方塊 + 兩條白色直槓，掛 BreathingAnimator 呼吸縮放。</summary>
        private GameObject CreatePauseIcon()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            Transform parent = canvas != null ? canvas.transform : transform;

            var root = new GameObject("RepositionPauseIcon", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            var rt = (RectTransform)root.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(220f, 220f);
            rt.anchoredPosition = Vector2.zero;
            var bg = root.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);
            bg.raycastTarget = false;

            for (int i = 0; i < 2; i++)
            {
                var bar = new GameObject(i == 0 ? "BarLeft" : "BarRight", typeof(RectTransform), typeof(Image));
                bar.transform.SetParent(root.transform, false);
                var brt = (RectTransform)bar.transform;
                brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
                brt.pivot = new Vector2(0.5f, 0.5f);
                brt.sizeDelta = new Vector2(40f, 130f);
                brt.anchoredPosition = new Vector2(i == 0 ? -35f : 35f, 0f);
                var img = bar.GetComponent<Image>();
                img.color = Color.white;
                img.raycastTarget = false;
            }

            root.AddComponent<BreathingAnimator>();
            root.SetActive(false);
            return root;
        }

        private void SetRepositionDialog(string text)
        {
            if (TopBarManager.Instance != null)
                TopBarManager.Instance.SetDialogTextWithAnimation(text);
        }

        private void CompleteReposition()
        {
            _repositionPaused = false;
            _repositionStableTimer = 0f;
            _repositionSuccessShowing = true;

            if (_aPoseFrame != null) _aPoseFrame.SetActive(false);
            ShowPauseIcon(false);
            Debug.Log("[Reposition] 重新定位完成 → 校準成功 + 倒數後續測");

            if (_repositionCoroutine != null) StopCoroutine(_repositionCoroutine);
            _repositionCoroutine = StartCoroutine(ResumeSequence());
        }

        /// <summary>定位成功圖 → 3-2-1 倒數 → 「繼續」→ 續測。全部沿用現有圖與語音。</summary>
        private IEnumerator ResumeSequence()
        {
            ShowHintImage(_hintImage_APoseSuccess);
            string resumeText = _repositionReason == Presence.OtherPerson ? "已確認是受測者，準備繼續測驗" : "定位成功，準備繼續測驗";
            SetRepositionDialog(resumeText);
            NuwaManager.Instance.NuwaTTS(resumeText);
            yield return new WaitForSeconds(_repositionSuccessDisplay);
            HideAndRestoreHintImage(_hintImage_APoseSuccess);

            for (int n = 3; n >= 1; n--)
            {
                Image img = GetCountdownImage(n);
                if (img != null) ShowHintImage(img);
                NuwaManager.Instance.NuwaTTS(n.ToString());
                yield return new WaitForSeconds(1f);
                if (img != null) HideAndRestoreHintImage(img);
            }

            if (_hintImage_TestStart != null) ShowHintImage(_hintImage_TestStart);
            NuwaManager.Instance.NuwaTTS("繼續");
            ResumeAfterReposition();

            yield return new WaitForSeconds(0.8f);
            if (_hintImage_TestStart != null) HideAndRestoreHintImage(_hintImage_TestStart);
            _repositionCoroutine = null;
        }

        private void ResumeAfterReposition()
        {
            _repositionSuccessShowing = false;

            if (TopBarManager.Instance != null)
                TopBarManager.Instance.SetDialogTextWithAnimation(GetDialogTextForStep(_currentStep));

            if (motionSDKClient != null) motionSDKClient.paused = false; // 繼續判定
            Debug.Log("[Reposition] 續測");
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
            bool isBalanceStep = _currentStep == FlowStep.BalanceSideBySide_Test
                              || _currentStep == FlowStep.BalanceSemiTandem_Test
                              || _currentStep == FlowStep.BalanceTandem_Test;
            // 平衡失敗：不念「已完成」，照順序帶到下一關
            if (isBalanceStep && _balanceFailed)
            {
                switch (_currentStep)
                {
                    case FlowStep.BalanceSideBySide_Test:
                        return "第一種姿勢沒有維持成功，接下來進行第二種姿勢測試";
                    case FlowStep.BalanceSemiTandem_Test:
                        return "第二種姿勢沒有維持成功，接下來進行第三種姿勢測試";
                    default:
                        return "第三種姿勢沒有維持成功，接下來進行坐站測試";
                }
            }

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
                    _timerDisplay.Initialize(BalanceDuration, false, false);
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
            // 重新定位中：凍結計時與安全逾時，等續測
            if (_repositionPaused || _repositionSuccessShowing) return;

            switch (_currentStep)
            {
                case FlowStep.BalanceSideBySide_Test:
                case FlowStep.BalanceSemiTandem_Test:
                case FlowStep.BalanceTandem_Test:
                    // Balance: _timer 累計時數 (0 → 撐滿秒數)；結束由判定器觸發 OnBalanceTestComplete。
                    // _balanceSafetyTimer 是防呆：判定器異常未回應才強制結束（至少比撐滿多 5 秒，避免搶在成功前判失敗）。
                    _timer += Time.deltaTime;
                    if (_timer > BalanceDuration) _timer = BalanceDuration;

                    _balanceSafetyTimer += Time.deltaTime;
                    float safetyTimeout = Mathf.Max(_balanceSafetyTimeout, BalanceDuration + 5f);
                    if (_balanceSafetyTimer >= safetyTimeout)
                    {
                        Debug.LogWarning($"[Balance] Safety timeout ({safetyTimeout}s) — 強制結束");
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
            ShowAposeHoldProgress(0f);
            _timer = 0f;
            _sitStandCount = 0;
            _currentHintImage = null;
            _isHintAnimating = false;
            _isCalibrate = true;
            CancelInvoke(nameof(GoToNextStep));
            CancelInvoke(nameof(StartTesting));
            if (_repositionCoroutine != null)
            {
                StopCoroutine(_repositionCoroutine);
                _repositionCoroutine = null;
            }

            _aposeHoldTimer = 0f;
            if (videoPoseTest != null && videoPoseTest.headTracker != null) videoPoseTest.headTracker.Frozen = false;
            ShowSkeleton(false);

            // 清除重新定位狀態
            _repositionPaused = false;
            _repositionSuccessShowing = false;
            _repositionStableTimer = 0f;
            if (motionSDKClient != null) motionSDKClient.paused = false;
            if (_aPoseFrame != null && _currentStep != FlowStep.APoseCalibration) _aPoseFrame.SetActive(false);
            ShowPauseIcon(false);

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

                    // FillAmount：1 → 0，持續平衡撐滿秒數
                    if (mat != null)
                        mat.SetFloat("_FillAmount", Mathf.Clamp01(1f - fillElapsed / BalanceDuration));

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
            _aposeHoldTimer = 0f;
            ShowAposeHoldProgress(0f);
            // 記下校準時的身形外框高度，之後用來判斷畫面中是否仍是這位受測者（排除假骨架）
            _personRefBoxHeight = videoPoseTest != null ? videoPoseTest.lastBoxHeight : 0f;
            // 鎖定受測者：記下衣服顏色當身分特徵，之後只追蹤這個人
            if (videoPoseTest != null && videoPoseTest.subject != null)
                videoPoseTest.subject.BeginLock(_personRefBoxHeight);
            Debug.Log($"[APose] 校準成功，身形外框高度基準 = {_personRefBoxHeight:F0}px");
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

            // 直接採用 detector 判定的分數（與步行/坐站一致；時間制 debounce 已保護 9~10s 邊界）
            int balanceScore = Mathf.RoundToInt(sdkScore);
            _balanceFailed = balanceScore <= 0;

            // 撐滿成功時計分器顯示滿秒數（兩邊計時各自累加，完成當幀 _timer 可能還是 9.99 → Floor 顯示 9）
            bool heldFull = _currentStep == FlowStep.BalanceTandem_Test ? balanceScore >= 2 : balanceScore >= 1;
            if (heldFull)
            {
                _timer = BalanceDuration;
                if (_timerDisplay != null) _timerDisplay.SetTime(_timer);
            }
            Debug.Log($"[Balance] complete — score={balanceScore}, elapsed={sdkElapsed:F2}, failed={_balanceFailed}");

            switch (_currentStep)
            {
                case FlowStep.BalanceSideBySide_Test:
                    ScoreManager.Instance.SetBalanceSideBySideScore(balanceScore); // 0/1
                    break;
                case FlowStep.BalanceSemiTandem_Test:
                    ScoreManager.Instance.SetBalanceSemiTandemScore(balanceScore); // 0/1
                    break;
                case FlowStep.BalanceTandem_Test:
                    ScoreManager.Instance.SetBalanceTandemScore(balanceScore); // 0/1/2
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
                    return "請從 4.5 公尺外的起點出發，以平常走路的速度向我走過來";

                default:
                    return "";
            }
        }

        #endregion
    }
}
