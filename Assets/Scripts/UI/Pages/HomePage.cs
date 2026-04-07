using UnityEngine;
using UnityEngine.UI;
using SPPB.Core;
using SPPB.Data;
using SPPB.UI.Components;

namespace SPPB.UI.Pages
{
    /// <summary>
    /// Home Page
    /// </summary>
    public class HomePage : BasePage
    {
        [Header("Top Bar - Title")]
        [SerializeField] private Sprite _homeTitleSprite;  // Home page title image

        [Header("Buttons")]
        [SerializeField] private Button _startButton;      // Start
        [SerializeField] private Button _helpButton;       // Help

        // Breathing animation component
        private BreathingAnimator _startButtonBreathing;

        public override void Initialize()
        {
            base.Initialize();

            // Bind button events
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

            if (_helpButton != null)
            {
                _helpButton.onClick.AddListener(OnHelpClicked);
            }
        }

        protected override void OnPageEnter()
        {
            base.OnPageEnter();
        }

        protected override void OnPageExit()
        {
            base.OnPageExit();
        }

        private void OnDestroy()
        {
            // Unsubscribe events to prevent memory leaks
            if (_startButton != null)
            {
                _startButton.onClick.RemoveListener(OnStartClicked);
            }

            if (_helpButton != null)
            {
                _helpButton.onClick.RemoveListener(OnHelpClicked);
            }
        }

        /// <summary>
        /// Start button clicked
        /// </summary>
        private void OnStartClicked()
        {
            // Reset all scores
            ScoreManager.Instance.ResetAllScores();

            UIManager.Instance.NextStep();
        }

        /// <summary>
        /// Help button clicked
        /// </summary>
        private void OnHelpClicked()
        {
        }

        /// <summary>
        /// Configure top bar
        /// HomePage: Title frame + Title + Instructions + Exit
        /// </summary>
        protected override void ConfigureTopBar()
        {
            TopBarManager.Instance.HideAll();
            TopBarManager.Instance.ShowTitleFrame(true);
            TopBarManager.Instance.ShowTitle(true);
            TopBarManager.Instance.SetDefaultFrame();
            TopBarManager.Instance.SetTitleImage(_homeTitleSprite);
            TopBarManager.Instance.ShowInstructionButton(true);
            TopBarManager.Instance.ShowExitButton(true);
        }
    }
}
