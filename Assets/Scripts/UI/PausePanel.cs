using UnityEngine;
using UnityEngine.UI;
using SPPB.Core;

namespace SPPB.UI
{
    /// <summary>
    /// Pause Panel - Displays pause options
    /// </summary>
    public class PausePanel : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private GameObject _panel;           // Pause panel
        [SerializeField] private Image _frameImage;           // Frame image
        [SerializeField] private Button _resumeButton;        // Resume button
        [SerializeField] private Button _exitButton;          // Exit button

        private void Awake()
        {
            // Bind button events
            if (_resumeButton != null)
            {
                _resumeButton.onClick.AddListener(OnResumeClicked);
            }

            if (_exitButton != null)
            {
                _exitButton.onClick.AddListener(OnExitClicked);
            }

            // Hide panel on initialization
            Hide();
        }

        /// <summary>
        /// Show pause panel
        /// </summary>
        public void Show()
        {
            if (_panel != null)
            {
                _panel.SetActive(true);
            }

            // Pause game time
            Time.timeScale = 0f;
        }

        /// <summary>
        /// Hide pause panel
        /// </summary>
        public void Hide()
        {
            if (_panel != null)
            {
                _panel.SetActive(false);
            }

            // Resume game time
            Time.timeScale = 1f;
        }

        /// <summary>
        /// Resume button clicked
        /// </summary>
        private void OnResumeClicked()
        {
            Hide();
        }

        /// <summary>
        /// Exit button clicked
        /// </summary>
        private void OnExitClicked()
        {
            Hide();

            // Return to home page
            if (UIManager.Instance != null)
            {
                UIManager.Instance.GoHome();
            }
        }
    }
}
