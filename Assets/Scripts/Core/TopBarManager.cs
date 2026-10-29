using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SPPB.Utils;
using SPPB.Data;
using System.Collections;

namespace SPPB.Core
{
    /// <summary>
    /// Top Bar Manager - Manages shared top UI elements across all pages
    /// </summary>
    public class TopBarManager : Singleton<TopBarManager>
    {
        [Header("Title Frame and Title")]
        [SerializeField] private Image _titleFrame;           // Title frame
        [SerializeField] private Sprite _defaultFrameSprite;  // Default frame sprite
        [SerializeField] private Sprite _testFrameSprite;     // Test phase frame sprite
        [SerializeField] private Image _titleImage;           // Page title

        [Header("Dialog Box")]
        [SerializeField] private GameObject _dialogContainer; // Dialog container
        [SerializeField] private TextMeshProUGUI _dialogText; // Dialog text

        [Header("Dialog Pop Animation")]
        [SerializeField] private float _dialogPopDuration = 0.3f;
        [SerializeField] private float _dialogPopMaxScale = 1.0f;

        [Header("Buttons")]
        [SerializeField] private Button _instructionButton;   // Instructions button
        [SerializeField] private Button _exitButton;          // Exit button (quit app on home, open pause panel elsewhere)

        [Header("Pause Panel")]
        [SerializeField] private SPPB.UI.PausePanel _pausePanel;  // Pause panel

        private Coroutine _dialogPopCoroutine;

        // ---------- 中英切換按鈕（只在首頁顯示） ----------
        private Button _langButton;
        private TextMeshProUGUI _langLabel;

        /// <summary>
        /// 建立中英切換按鈕（底圖 Resources/UI/lang_button.png，130x88），位置在「離開」按鈕左邊 114.5px。
        /// </summary>
        private void CreateLangButton()
        {
            if (_langButton != null || _exitButton == null) return;
            var refRt = (RectTransform)_exitButton.transform;
            var go = new GameObject("LangButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(refRt.parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = refRt.anchorMin; rt.anchorMax = refRt.anchorMax; rt.pivot = refRt.pivot;
            rt.sizeDelta = new Vector2(130f, 88f);
            rt.localScale = refRt.localScale;
            rt.anchoredPosition = refRt.anchoredPosition + new Vector2(-114.5f, -1.2f);

            var img = go.GetComponent<Image>();
            img.sprite = Resources.Load<Sprite>("UI/lang_button");
            img.preserveAspect = true;
            go.AddComponent<ButtonTrigger>();   // 與其他按鈕相同：手掌游標停 2 秒觸發

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            var trt = (RectTransform)textGo.transform;
            // 底圖 130x88：膠囊在上方（x14~116、y3~36），下方是光暈 → 文字只放膠囊區（Unity 錨點 y 由下往上算）
            trt.anchorMin = new Vector2(0.14f, 0.61f); trt.anchorMax = new Vector2(0.86f, 0.94f);
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            _langLabel = textGo.GetComponent<TextMeshProUGUI>();
            if (_dialogText != null) { _langLabel.font = _dialogText.font; _langLabel.fontSharedMaterial = _dialogText.fontSharedMaterial; }
            _langLabel.color = Color.white;
            _langLabel.fontStyle = FontStyles.Bold;
            _langLabel.enableAutoSizing = true; _langLabel.fontSizeMin = 8; _langLabel.fontSizeMax = 18;
            _langLabel.alignment = TextAlignmentOptions.Center;
            _langLabel.raycastTarget = false;

            _langButton = go.GetComponent<Button>();
            _langButton.onClick.AddListener(OnLangClicked);
            RefreshLangLabel();
            go.SetActive(false);
        }

        /// <summary>顯示要切換過去的語言：中文模式顯示「EN」，英文模式顯示「中文」。</summary>
        private void RefreshLangLabel()
        {
            if (_langLabel != null) _langLabel.text = Lang.IsEn ? "中文" : "EN";
        }

        private void OnLangClicked()
        {
            Lang.Toggle();
            RefreshLangLabel();
            if (NuwaManager.Instance != null)
                NuwaManager.Instance.NuwaTTS(Lang.T("已切換為中文", "Switched to English"));
        }

        public void ShowLangButton(bool show)
        {
            if (show) CreateLangButton();
            if (_langButton != null) _langButton.gameObject.SetActive(show);
        }

        protected override void Awake()
        {
            base.Awake();
            InitializeButtons();
            ConfigureDialogText();
            Lang.Changed += RefreshLangLabel;
        }

        /// <summary>
        /// 對話框文字（場景設定：寬 200、左右邊距約 -409/-352 → 實際排版寬約 961，幾乎整條上方列，中文從不換行）。
        /// 英文長句會延伸到左邊標題底下 → 英文時改成邊距 0、寬 560（標題與右上按鈕之間），超出就換行並自動縮字；
        /// 中文維持原設定，外觀不變。
        /// </summary>
        private Vector4 _dialogMarginZh;
        private Vector2 _dialogSizeZh;
        private float _dialogFontZh;
        private bool _dialogCaptured;

        private void ConfigureDialogText()
        {
            if (_dialogText == null) return;
            _dialogMarginZh = _dialogText.margin;
            _dialogSizeZh = _dialogText.rectTransform.sizeDelta;
            _dialogFontZh = _dialogText.fontSize;
            _dialogCaptured = true;
            ApplyDialogLayout();
            Lang.Changed += ApplyDialogLayout;
        }

        private void ApplyDialogLayout()
        {
            if (_dialogText == null || !_dialogCaptured) return;
            var rt = _dialogText.rectTransform;
            if (Lang.IsEn)
            {
                _dialogText.margin = Vector4.zero;
                rt.sizeDelta = new Vector2(560f, Mathf.Max(_dialogSizeZh.y, 50f));
                _dialogText.enableWordWrapping = true;
                _dialogText.enableAutoSizing = true;
                _dialogText.fontSizeMax = _dialogFontZh;
                _dialogText.fontSizeMin = Mathf.Min(_dialogFontZh, 11f);
            }
            else
            {
                _dialogText.margin = _dialogMarginZh;
                rt.sizeDelta = _dialogSizeZh;
                _dialogText.enableAutoSizing = false;
                _dialogText.fontSize = _dialogFontZh;
            }
        }

        /// <summary>
        /// Initialize button events
        /// </summary>
        private void InitializeButtons()
        {
            if (_instructionButton != null)
            {
                _instructionButton.onClick.AddListener(OnInstructionClicked);
            }

            if (_exitButton != null)
            {
                _exitButton.onClick.AddListener(OnExitClicked);
            }
        }

        private void OnDestroy()
        {
            Lang.Changed -= RefreshLangLabel;
            Lang.Changed -= ApplyDialogLayout;
            // Unsubscribe events to prevent memory leaks
            if (_instructionButton != null)
            {
                _instructionButton.onClick.RemoveListener(OnInstructionClicked);
            }

            if (_exitButton != null)
            {
                _exitButton.onClick.RemoveListener(OnExitClicked);
            }
        }

        /// <summary>
        /// Set title image
        /// </summary>
        public void SetTitleImage(Sprite titleSprite)
        {
            if (_titleImage != null && titleSprite != null)
            {
                _titleImage.sprite = Lang.S(titleSprite);   // 中英切換：英文時換成 Resources/LangEn 同名圖
            }
        }

        /// <summary>
        /// Set dialog text
        /// </summary>
        public void SetDialogText(string text)
        {
            if (_dialogText != null)
            {
                _dialogText.text = text;
            }
        }

        /// <summary>
        /// Set dialog text and play pop animation
        /// </summary>
        public void SetDialogTextWithAnimation(string text)
        {
            if (_dialogText != null)
            {
                _dialogText.text = text;
            }

            // Play pop animation
            PlayDialogPopAnimation();
        }

        /// <summary>
        /// Play dialog pop animation
        /// </summary>
        public void PlayDialogPopAnimation()
        {
            if (_dialogContainer == null) return;

            if (_dialogPopCoroutine != null)
            {
                StopCoroutine(_dialogPopCoroutine);
            }
            _dialogPopCoroutine = StartCoroutine(DialogPopAnimation());
        }

        private IEnumerator DialogPopAnimation()
        {
            if (_dialogContainer == null) yield break;

            Transform dialogTransform = _dialogContainer.transform;

            // Start from 0
            dialogTransform.localScale = Vector3.zero;
            yield return null;

            float elapsed = 0f;
            while (elapsed < _dialogPopDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / _dialogPopDuration);
                float easedProgress = EaseOutQuad(progress);
                float currentScale = easedProgress * _dialogPopMaxScale;
                dialogTransform.localScale = Vector3.one * currentScale;
                yield return null;
            }

            dialogTransform.localScale = Vector3.one * _dialogPopMaxScale;
        }

        private float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }


        /// <summary>
        /// Show title frame
        /// </summary>
        public void ShowTitleFrame(bool show)
        {
            if (_titleFrame != null)
            {
                _titleFrame.gameObject.SetActive(show);
            }
        }

        /// <summary>
        /// Set title frame to default style
        /// </summary>
        public void SetDefaultFrame()
        {
            if (_titleFrame != null && _defaultFrameSprite != null)
            {
                _titleFrame.sprite = _defaultFrameSprite;
            }
        }

        /// <summary>
        /// Set title frame to test phase style
        /// </summary>
        public void SetTestFrame()
        {
            if (_titleFrame != null && _testFrameSprite != null)
            {
                _titleFrame.sprite = _testFrameSprite;
            }
        }

        /// <summary>
        /// Show title
        /// </summary>
        public void ShowTitle(bool show)
        {
            if (_titleImage != null)
            {
                _titleImage.gameObject.SetActive(show);
            }
        }

        /// <summary>
        /// Show dialog
        /// </summary>
        public void ShowDialog(bool show)
        {
            if (_dialogContainer != null)
            {
                _dialogContainer.SetActive(show);
            }
        }

        /// <summary>
        /// Show instruction button
        /// </summary>
        public void ShowInstructionButton(bool show)
        {
            if (_instructionButton != null)
            {
                _instructionButton.gameObject.SetActive(show);
            }
        }

        /// <summary>
        /// Show exit button
        /// </summary>
        public void ShowExitButton(bool show)
        {
            if (_exitButton != null)
            {
                _exitButton.gameObject.SetActive(show);
            }
        }

        /// <summary>
        /// Hide all elements
        /// </summary>
        public void HideAll()
        {
            ShowTitleFrame(false);
            ShowTitle(false);
            ShowDialog(false);
            ShowInstructionButton(false);
            ShowExitButton(false);
            ShowLangButton(false);
        }

        #region Button Events

        private void OnInstructionClicked()
        {
        }

        private void OnExitClicked()
        {
            // Check if currently on home page
            if (UIManager.Instance == null)
            {
                Debug.LogError("[TopBarManager] UIManager.Instance is null!");
                return;
            }

            FlowStep currentStep = UIManager.Instance.CurrentStep;

            if (currentStep == FlowStep.Home)
            {
                // Home page: quit application directly
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
            else
            {
                // Other pages: open pause panel
                if (_pausePanel != null)
                {
                    _pausePanel.Show();
                }
                else
                {
                    Debug.LogWarning("[TopBarManager] Pause panel not configured!");
                }
            }
        }

        #endregion
    }
}
