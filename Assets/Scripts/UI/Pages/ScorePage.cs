using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SPPB.Core;
using SPPB.Data;
using SPPB.UI.Components;
using System.Collections;

namespace SPPB.UI.Pages
{
    /// <summary>
    /// Score Page - Displays all test scores and total score (single page)
    /// </summary>
    public class ScorePage : BasePage
    {
        [Header("Top Bar")]
        [SerializeField] private Sprite _scoreTitleSprite;    // Score page title

        [Header("Background")]
        [SerializeField] private Image _backgroundImage;

        [Header("Balance Test Score Section")]
        [SerializeField] private GameObject _balanceScoreContainer;
        [SerializeField] private Image _balanceIcon;                    // Test icon
        [SerializeField] private Image _balanceNameImage;               // Test name image
        [SerializeField] private TextMeshProUGUI _balanceScoreText;     // Score text
        [SerializeField] private Image _balanceBarFrame;                // Score bar frame
        [SerializeField] private RectTransform _balanceBarFill;         // Score bar fill (width controlled via RectTransform)

        [Header("Sit-Stand Test Score Section")]
        [SerializeField] private GameObject _sitStandScoreContainer;
        [SerializeField] private Image _sitStandIcon;                   // Test icon
        [SerializeField] private Image _sitStandNameImage;              // Test name image
        [SerializeField] private TextMeshProUGUI _sitStandScoreText;    // Score text
        [SerializeField] private Image _sitStandBarFrame;               // Score bar frame
        [SerializeField] private RectTransform _sitStandBarFill;        // Score bar fill (width controlled via RectTransform)

        [Header("Walk Test Score Section")]
        [SerializeField] private GameObject _walkScoreContainer;
        [SerializeField] private Image _walkIcon;                       // Test icon
        [SerializeField] private Image _walkNameImage;                  // Test name image
        [SerializeField] private TextMeshProUGUI _walkScoreText;        // Score text
        [SerializeField] private Image _walkBarFrame;                   // Score bar frame
        [SerializeField] private RectTransform _walkBarFill;            // Score bar fill (width controlled via RectTransform)

        [Header("Total Score Section")]
        [SerializeField] private GameObject _totalScoreContainer;
        [SerializeField] private Image _totalRingFrame;                 // Total score ring frame
        [SerializeField] private Image _totalRingFill;                  // Total score ring fill (controlled via Material)

        [Header("Health Rating Images (4)")]
        [SerializeField] private Image _healthRatingImage_Healthy;      // Healthy (10-12 points)
        [SerializeField] private Image _healthRatingImage_PreFrail;     // Pre-frail (7-9 points)
        [SerializeField] private Image _healthRatingImage_Frail;        // Frail (4-6 points)
        [SerializeField] private Image _healthRatingImage_Unable;       // Unable to complete (0-3 points)

        [Header("Score Bar Settings")]
        [SerializeField] private float _barAnimationDuration = 0.8f;    // Score bar animation duration
        [SerializeField] private float _ringAnimationDuration = 1.0f;   // Ring animation duration
        [SerializeField] private string _ringFillPropertyName = "_FillAmount";  // Ring shader fill property name

        [Header("Health Rating Animation Settings")]
        [SerializeField] private float _healthRatingScaleStart = 0.3f;      // Start scale
        [SerializeField] private float _healthRatingScaleEnd = 1.0f;        // End scale
        [SerializeField] private float _healthRatingScaleDuration = 0.4f;   // Animation duration

        [Header("Score Text Pop Animation Settings")]
        [SerializeField] private float _scoreTextPopDuration = 0.3f;        // Pop animation duration
        [SerializeField] private float _scoreTextPopMaxScale = 1.0f;        // Final scale

        [Header("Buttons")]
        [SerializeField] private Button _homeButton;                    // Home button

        // Current step
        private FlowStep _currentStep;

        // Breathing animation component
        private BreathingAnimator _homeButtonBreathing;

        // Score data
        private int _balanceScore = 0;
        private int _sitStandScore = 0;
        private int _walkScore = 0;
        private const int MAX_SINGLE_SCORE = 4;     // Single test max score
        private const int MAX_TOTAL_SCORE = 12;     // Total max score

        // Bar original sizes
        private Vector2 _balanceBarOriginalSize;
        private Vector2 _sitStandBarOriginalSize;
        private Vector2 _walkBarOriginalSize;

        // Bar Material instances (controlled via _FillAmount shader property)
        private Material _balanceBarMaterialInstance;
        private Material _sitStandBarMaterialInstance;
        private Material _walkBarMaterialInstance;
        private const string BAR_FILL_PROPERTY = "_FillAmount";

        // Ring Material instance
        private Material _ringMaterialInstance;

        // Health rating animation coroutine
        private Coroutine _healthRatingAnimCoroutine;

        public override void Initialize()
        {
            base.Initialize();

            if (_homeButton != null)
            {
                _homeButton.onClick.AddListener(OnHomeClicked);

                // Auto-add breathing animation component
                _homeButtonBreathing = _homeButton.GetComponent<BreathingAnimator>();
                if (_homeButtonBreathing == null)
                {
                    _homeButtonBreathing = _homeButton.gameObject.AddComponent<BreathingAnimator>();
                }
            }

            // Store original sizes and set pivot for each score bar
            InitializeBarFill(_balanceBarFill, ref _balanceBarOriginalSize);
            InitializeBarFill(_sitStandBarFill, ref _sitStandBarOriginalSize);
            InitializeBarFill(_walkBarFill, ref _walkBarOriginalSize);

            // Create Material instances for bar fills (each bar uses _FillAmount shader property)
            _balanceBarMaterialInstance = CreateBarMaterialInstance(_balanceBarFill);
            _sitStandBarMaterialInstance = CreateBarMaterialInstance(_sitStandBarFill);
            _walkBarMaterialInstance = CreateBarMaterialInstance(_walkBarFill);

            // Create Material instance for Ring Fill (avoid modifying shared Material)
            if (_totalRingFill != null && _totalRingFill.material != null)
            {
                _ringMaterialInstance = new Material(_totalRingFill.material);
                _totalRingFill.material = _ringMaterialInstance;
            }
        }

        /// <summary>
        /// Create a material instance for a bar fill Image, so _FillAmount can be set independently
        /// </summary>
        private Material CreateBarMaterialInstance(RectTransform barFill)
        {
            if (barFill == null) return null;
            Image img = barFill.GetComponent<Image>();
            if (img == null || img.material == null) return null;
            var mat = new Material(img.material);
            img.material = mat;
            return mat;
        }

        /// <summary>
        /// Initialize score bar fill (set pivot to left side)
        /// </summary>
        private void InitializeBarFill(RectTransform barFill, ref Vector2 originalSize)
        {
            if (barFill == null) return;

            originalSize = barFill.sizeDelta;

            // Set pivot to left center to ensure bar grows from left
            Vector2 currentPivot = barFill.pivot;
            Vector2 targetPivot = new Vector2(0f, 0.5f);

            if (currentPivot != targetPivot)
            {
                Vector2 originalPosition = barFill.anchoredPosition;
                float offsetX = (currentPivot.x - targetPivot.x) * originalSize.x;
                barFill.pivot = targetPivot;
                barFill.anchoredPosition = new Vector2(originalPosition.x - offsetX, originalPosition.y);
            }
        }

        public override void Configure(FlowStep step)
        {
            _currentStep = step;
        }

        protected override void OnPageEnter()
        {
            base.OnPageEnter();

            // 重新啟用 home 按鈕：ButtonTrigger 觸發後會 SetActive(false)，第二次進入頁面要復原
            if (_homeButton != null)
            {
                _homeButton.gameObject.SetActive(true);
            }

            // Load scores from ScoreManager
            LoadScoresFromManager();

            // Show all score containers
            ShowAllScoreContainers();

            // Reset all score bars to 0
            ResetAllBars();

            // Start score animation
            StartCoroutine(AnimateAllScores());
        }

        /// <summary>
        /// Load scores from ScoreManager
        /// </summary>
        private void LoadScoresFromManager()
        {
            if (ScoreManager.Instance == null)
            {
                Debug.LogError("[ScorePage] ScoreManager not initialized! Using default score 0");
                _balanceScore = 0;
                _sitStandScore = 0;
                _walkScore = 0;
                return;
            }

            _balanceScore = ScoreManager.Instance.GetBalanceScore();
            _sitStandScore = ScoreManager.Instance.GetSitStandScore();
            _walkScore = ScoreManager.Instance.GetWalkScore();
        }

        protected override void OnPageExit()
        {
            base.OnPageExit();
            StopAllCoroutines();
        }

        private void OnDestroy()
        {
            // Unsubscribe events to prevent memory leaks
            if (_homeButton != null)
            {
                _homeButton.onClick.RemoveListener(OnHomeClicked);
            }

            // Clean up Material instances
            if (_ringMaterialInstance != null) Destroy(_ringMaterialInstance);
            if (_balanceBarMaterialInstance != null) Destroy(_balanceBarMaterialInstance);
            if (_sitStandBarMaterialInstance != null) Destroy(_sitStandBarMaterialInstance);
            if (_walkBarMaterialInstance != null) Destroy(_walkBarMaterialInstance);
        }

        /// <summary>
        /// Reset all score bars to 0
        /// </summary>
        private void ResetAllBars()
        {
            if (_balanceBarMaterialInstance != null) _balanceBarMaterialInstance.SetFloat(BAR_FILL_PROPERTY, 0f);
            if (_sitStandBarMaterialInstance != null) _sitStandBarMaterialInstance.SetFloat(BAR_FILL_PROPERTY, 0f);
            if (_walkBarMaterialInstance != null) _walkBarMaterialInstance.SetFloat(BAR_FILL_PROPERTY, 0f);

            if (_ringMaterialInstance != null)
            {
                _ringMaterialInstance.SetFloat(_ringFillPropertyName, 0f);
            }

            // Hide all health rating images (show after animation completes)
            HideAllHealthRatingImages();

            // Hide all score texts (show when bar animation starts)
            HideAllScoreTexts();
        }

        /// <summary>
        /// Hide all score texts
        /// </summary>
        private void HideAllScoreTexts()
        {
            if (_balanceScoreText != null)
                _balanceScoreText.transform.localScale = Vector3.zero;

            if (_sitStandScoreText != null)
                _sitStandScoreText.transform.localScale = Vector3.zero;

            if (_walkScoreText != null)
                _walkScoreText.transform.localScale = Vector3.zero;
        }

        /// <summary>
        /// Animate all scores with staggered playback (next starts halfway through previous)
        /// </summary>
        private IEnumerator AnimateAllScores()
        {
            // Update text first (but don't show, wait for pop animation)
            UpdateScoreTexts();

            // Calculate stagger delay (half of animation duration)
            float barOverlapDelay = _barAnimationDuration * 0.5f;
            float ringOverlapDelay = _ringAnimationDuration * 0.5f;

            // Start balance test animation + score pop
            StartCoroutine(AnimateBar(_balanceBarMaterialInstance, _balanceScore));
            StartCoroutine(AnimateScoreTextPop(_balanceScoreText));
            yield return new WaitForSeconds(barOverlapDelay);

            // Start sit-stand test animation + score pop
            StartCoroutine(AnimateBar(_sitStandBarMaterialInstance, _sitStandScore));
            StartCoroutine(AnimateScoreTextPop(_sitStandScoreText));
            yield return new WaitForSeconds(barOverlapDelay);

            // Start walk test animation + score pop
            StartCoroutine(AnimateBar(_walkBarMaterialInstance, _walkScore));
            StartCoroutine(AnimateScoreTextPop(_walkScoreText));
            yield return new WaitForSeconds(barOverlapDelay);

            // Start total score ring animation
            StartCoroutine(AnimateRing());
            yield return new WaitForSeconds(ringOverlapDelay);

            // Update health rating (show when ring animation is halfway)
            UpdateHealthRating(GetTotalScore());
        }

        /// <summary>
        /// Score text pop animation
        /// </summary>
        private IEnumerator AnimateScoreTextPop(TextMeshProUGUI scoreText)
        {
            if (scoreText == null) yield break;

            Transform textTransform = scoreText.transform;

            // Start from 0
            textTransform.localScale = Vector3.zero;
            yield return null;

            float elapsed = 0f;
            while (elapsed < _scoreTextPopDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / _scoreTextPopDuration);
                float easedProgress = EaseOutQuad(progress);
                float currentScale = easedProgress * _scoreTextPopMaxScale;
                textTransform.localScale = Vector3.one * currentScale;
                yield return null;
            }

            textTransform.localScale = Vector3.one * _scoreTextPopMaxScale;
        }

        /// <summary>
        /// EaseOutQuad easing function
        /// </summary>
        private float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }

        /// <summary>
        /// Update score texts (without animation)
        /// Format: ?/4 (user score/max score)
        /// </summary>
        private void UpdateScoreTexts()
        {
            if (_balanceScoreText != null)
                _balanceScoreText.text = $"{_balanceScore}/{MAX_SINGLE_SCORE}";

            if (_sitStandScoreText != null)
                _sitStandScoreText.text = $"{_sitStandScore}/{MAX_SINGLE_SCORE}";

            if (_walkScoreText != null)
                _walkScoreText.text = $"{_walkScore}/{MAX_SINGLE_SCORE}";
        }

        /// <summary>
        /// Animate score bar via _FillAmount material property
        /// </summary>
        private IEnumerator AnimateBar(Material barMat, int score)
        {
            if (barMat == null) yield break;

            float targetProgress = (float)score / MAX_SINGLE_SCORE;
            float elapsed = 0f;

            while (elapsed < _barAnimationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _barAnimationDuration);
                // Use EaseOutQuad easing
                float easedT = 1f - (1f - t) * (1f - t);
                float currentProgress = Mathf.Lerp(0f, targetProgress, easedT);
                barMat.SetFloat(BAR_FILL_PROPERTY, currentProgress);
                yield return null;
            }

            barMat.SetFloat(BAR_FILL_PROPERTY, targetProgress);
        }

        /// <summary>
        /// Animate total score ring
        /// </summary>
        private IEnumerator AnimateRing()
        {
            if (_ringMaterialInstance == null) yield break;

            int totalScore = GetTotalScore();
            float targetProgress = (float)totalScore / MAX_TOTAL_SCORE;
            float elapsed = 0f;

            while (elapsed < _ringAnimationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _ringAnimationDuration);
                // Use EaseOutQuad easing
                float easedT = 1f - (1f - t) * (1f - t);
                float currentProgress = Mathf.Lerp(0f, targetProgress, easedT);
                _ringMaterialInstance.SetFloat(_ringFillPropertyName, currentProgress);
                yield return null;
            }

            _ringMaterialInstance.SetFloat(_ringFillPropertyName, targetProgress);
        }

        /// <summary>
        /// Set score bar width
        /// </summary>
        private void SetBarWidth(RectTransform barFill, float progress, Vector2 originalSize)
        {
            if (barFill == null) return;
            // Use the bar's original width as max width
            float newWidth = originalSize.x * progress;
            barFill.sizeDelta = new Vector2(newWidth, originalSize.y);
        }

        #region UI Configuration

        /// <summary>
        /// Show all score containers
        /// </summary>
        private void ShowAllScoreContainers()
        {
            SetGameObjectActive(_balanceScoreContainer, true);
            SetGameObjectActive(_sitStandScoreContainer, true);
            SetGameObjectActive(_walkScoreContainer, true);
            SetGameObjectActive(_totalScoreContainer, true);
        }

        /// <summary>
        /// Update health rating based on total score
        /// 0-3 points: Unable to complete
        /// 4-6 points: Frail
        /// 7-9 points: Pre-frail
        /// 10-12 points: Healthy
        /// </summary>
        private void UpdateHealthRating(int totalScore)
        {
            // Hide all health rating images first
            HideAllHealthRatingImages();

            Image targetImage = null;
            string rating;

            if (totalScore >= 10)
            {
                rating = "Healthy";
                targetImage = _healthRatingImage_Healthy;
            }
            else if (totalScore >= 7)
            {
                rating = "Pre-frail";
                targetImage = _healthRatingImage_PreFrail;
            }
            else if (totalScore >= 4)
            {
                rating = "Frail";
                targetImage = _healthRatingImage_Frail;
            }
            else
            {
                rating = "Unable to complete";
                targetImage = _healthRatingImage_Unable;
            }

            // Play popup animation
            if (targetImage != null)
            {
                if (_healthRatingAnimCoroutine != null)
                {
                    StopCoroutine(_healthRatingAnimCoroutine);
                }
                _healthRatingAnimCoroutine = StartCoroutine(AnimateHealthRatingPopup(targetImage));
            }
        }

        /// <summary>
        /// Health rating popup animation
        /// </summary>
        private IEnumerator AnimateHealthRatingPopup(Image targetImage)
        {
            if (targetImage == null) yield break;

            // Initial state: small scale
            targetImage.transform.localScale = Vector3.one * _healthRatingScaleStart;
            targetImage.gameObject.SetActive(true);

            float elapsed = 0f;

            while (elapsed < _healthRatingScaleDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / _healthRatingScaleDuration);

                // Use EaseOutBack for elastic effect
                float easedProgress = EaseOutBack(progress);
                float scale = Mathf.Lerp(_healthRatingScaleStart, _healthRatingScaleEnd, easedProgress);
                targetImage.transform.localScale = Vector3.one * scale;

                yield return null;
            }

            // Ensure final scale
            targetImage.transform.localScale = Vector3.one * _healthRatingScaleEnd;
            _healthRatingAnimCoroutine = null;
        }

        /// <summary>
        /// EaseOutBack easing function (elastic bounce effect)
        /// </summary>
        private float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        /// <summary>
        /// Hide all health rating images
        /// </summary>
        private void HideAllHealthRatingImages()
        {
            SetImageActive(_healthRatingImage_Healthy, false);
            SetImageActive(_healthRatingImage_PreFrail, false);
            SetImageActive(_healthRatingImage_Frail, false);
            SetImageActive(_healthRatingImage_Unable, false);
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

        #endregion

        #region Button Events

        /// <summary>
        /// Home button clicked
        /// </summary>
        private void OnHomeClicked()
        {
            UIManager.Instance.GoHome();
        }

        #endregion

        #region Top Bar Configuration

        /// <summary>
        /// Configure top bar
        /// ScorePage: Title frame + Title + Dialog + Exit
        /// </summary>
        protected override void ConfigureTopBar()
        {
            TopBarManager.Instance.HideAll();
            TopBarManager.Instance.ShowTitleFrame(true);
            TopBarManager.Instance.ShowTitle(true);
            TopBarManager.Instance.ShowDialog(true);
            TopBarManager.Instance.ShowExitButton(true);
            TopBarManager.Instance.SetDefaultFrame();

            // Set title and dialog content
            TopBarManager.Instance.SetTitleImage(_scoreTitleSprite);
            TopBarManager.Instance.SetDialogTextWithAnimation("感謝您的配合，以下是您的測驗成績");
        }

        #endregion

        #region Public Methods - Set Scores

        /// <summary>
        /// Set balance test score
        /// </summary>
        public void SetBalanceScore(int score)
        {
            _balanceScore = Mathf.Clamp(score, 0, MAX_SINGLE_SCORE);
        }

        /// <summary>
        /// Set sit-stand test score
        /// </summary>
        public void SetSitStandScore(int score)
        {
            _sitStandScore = Mathf.Clamp(score, 0, MAX_SINGLE_SCORE);
        }

        /// <summary>
        /// Set walk test score
        /// </summary>
        public void SetWalkScore(int score)
        {
            _walkScore = Mathf.Clamp(score, 0, MAX_SINGLE_SCORE);
        }

        /// <summary>
        /// Set all scores at once
        /// </summary>
        public void SetAllScores(int balanceScore, int sitStandScore, int walkScore)
        {
            SetBalanceScore(balanceScore);
            SetSitStandScore(sitStandScore);
            SetWalkScore(walkScore);
        }

        /// <summary>
        /// Get total score
        /// </summary>
        public int GetTotalScore()
        {
            return _balanceScore + _sitStandScore + _walkScore;
        }

        #endregion
    }
}
