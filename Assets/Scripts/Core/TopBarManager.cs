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

        protected override void Awake()
        {
            base.Awake();
            InitializeButtons();
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
                _titleImage.sprite = titleSprite;
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
