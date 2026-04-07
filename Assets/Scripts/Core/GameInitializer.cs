using UnityEngine;
using SPPB.UI.Pages;
using SPPB.Data;

namespace SPPB.Core
{
    /// <summary>
    /// Game Initializer - Responsible for initializing all pages at startup
    /// </summary>
    public class GameInitializer : MonoBehaviour
    {
        [Header("Page References")]
        [SerializeField] private HomePage _homePage;
        [SerializeField] private TeachingPage _teachingPage;
        [SerializeField] private TestPage _testPage;
        [SerializeField] private ScorePage _scorePage;

        [Header("Top Bar Manager")]
        [SerializeField] private TopBarManager _topBarManager;

        private void Start()
        {
            // Ensure TopBarManager is initialized
            if (_topBarManager == null)
            {
                Debug.LogError("[GameInitializer] TopBarManager not configured!");
            }

            InitializePages();
            RegisterToUIManager();

            // Start from home page
            UIManager.Instance.GoToStep(FlowStep.Home);
        }

        /// <summary>
        /// Initialize all pages
        /// </summary>
        private void InitializePages()
        {
            // Initialize() automatically hides pages, no need to call Hide()
            if (_homePage != null)
            {
                _homePage.Initialize();
            }

            if (_teachingPage != null)
            {
                _teachingPage.Initialize();
            }

            if (_testPage != null)
            {
                _testPage.Initialize();
            }

            if (_scorePage != null)
            {
                _scorePage.Initialize();
            }
        }

        /// <summary>
        /// Register pages to UIManager
        /// </summary>
        private void RegisterToUIManager()
        {
            UIManager.Instance.RegisterPages(_homePage, _teachingPage, _testPage, _scorePage);
        }
    }
}
