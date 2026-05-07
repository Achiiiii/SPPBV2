using UnityEngine;
using SPPB.Utils;
using SPPB.Data;
using SPPB.UI.Pages;
using System;

namespace SPPB.Core
{
    /// <summary>
    /// UI Manager - Responsible for page switching and flow control
    /// </summary>
    public class UIManager : Singleton<UIManager>
    {
        [SerializeField] private MotionSDKClient motionSDKClient;
        [Header("Current Step")]
        [SerializeField] private FlowStep _currentStep = FlowStep.Home;

        // Page references
        private HomePage _homePage;
        private BasePage _teachingPage;
        private BasePage _testPage;
        private BasePage _scorePage;
        private BasePage _currentPage;

        /// <summary>
        /// 頁面轉場鎖定旗標，防止轉場期間重複觸發 NextStep/GoToStep
        /// </summary>
        private bool _isTransitioning = false;

        /// <summary>
        /// Current flow step
        /// </summary>
        public FlowStep CurrentStep => _currentStep;

        /// <summary>
        /// Step changed event
        /// </summary>
        public event Action<FlowStep> OnStepChanged;

        /// <summary>
        /// Register all pages (called by GameInitializer)
        /// </summary>
        public void RegisterPages(HomePage homePage, BasePage teachingPage, BasePage testPage, BasePage scorePage)
        {
            _homePage = homePage;
            _teachingPage = teachingPage;
            _testPage = testPage;
            _scorePage = scorePage;

        }

        /// <summary>
        /// Go to next step
        /// </summary>
        public void NextStep()
        {
            if (_isTransitioning)
            {
                Debug.Log("[UIManager] NextStep blocked: transition in progress");
                return;
            }

            int nextIndex = (int)_currentStep + 1;
            int maxIndex = Enum.GetValues(typeof(FlowStep)).Length - 1;

            if (nextIndex <= maxIndex)
            {
                GoToStep((FlowStep)nextIndex);
            }
            else
            {
                // Already at last step, return to home
                GoToStep(FlowStep.Home);
            }
        }

        /// <summary>
        /// Go to specified step
        /// </summary>
        public void GoToStep(FlowStep step)
        {
            if (_isTransitioning)
            {
                Debug.Log($"[UIManager] GoToStep({step}) blocked: transition in progress");
                return;
            }

            _isTransitioning = true;

            FlowStep previousStep = _currentStep;
            _currentStep = step;

            // Trigger step changed event
            OnStepChanged?.Invoke(_currentStep);

            // Execute page switch
            SwitchPage(_currentStep);
        }

        /// <summary>
        /// Return to home page
        /// </summary>
        public void GoHome()
        {
            // GoHome 強制解鎖，確保隨時可以回首頁
            _isTransitioning = false;
            GoToStep(FlowStep.Home);
            motionSDKClient.ResetMotionSDK();
            ScoreManager.Instance.ResetAllScores();
        }

        /// <summary>
        /// Switch page based on step
        /// </summary>
        private void SwitchPage(FlowStep step)
        {
            PageType pageType = GetPageType(step);
            BasePage targetPage = GetPageByType(pageType);

            // Determine if transition effect is needed
            bool needsTransition = ShouldUseTransition(_currentPage, targetPage);
            NuwaManager.Instance.StopNuwaTTS();

            if (needsTransition && TransitionManager.Instance != null)
            {
                // Use black screen transition (switch pages during blackout)
                // 轉場完成後解鎖
                TransitionManager.Instance.TransitionBetweenPages(
                    () => PerformPageSwitchDuringTransition(targetPage, step),
                    () => _isTransitioning = false
                );
            }
            else
            {
                // Switch pages directly (using page's own fade effects)
                PerformNormalPageSwitch(targetPage, step);
                // 同步切換完成，立即解鎖
                _isTransitioning = false;
            }
        }

        /// <summary>
        /// Normal page switch (using page's own fade in/out effects)
        /// </summary>
        private void PerformNormalPageSwitch(BasePage targetPage, FlowStep step)
        {
            // Determine if switching within the same page
            bool isSamePageSwitch = (_currentPage == targetPage);

            if (isSamePageSwitch)
            {
                // Switching between phases within same page, update Configure and TopBar without fade
                if (targetPage != null)
                {
                    targetPage.Configure(step);
                    targetPage.TriggerConfigureTopBar();
                }
            }
            else
            {
                // Switching between different pages, use Show/Hide (with fade effects)

                // Hide current page
                if (_currentPage != null)
                {
                    _currentPage.Hide();
                }

                // Show target page
                if (targetPage != null)
                {
                    targetPage.Configure(step);
                    targetPage.Show();
                    _currentPage = targetPage;
                }
            }
        }

        /// <summary>
        /// Switch pages during black screen transition (skip page's own fade in effect)
        /// </summary>
        private void PerformPageSwitchDuringTransition(BasePage targetPage, FlowStep step)
        {
            // Hide current page (immediately, without animation)
            if (_currentPage != null && _currentPage != targetPage)
            {
                // Trigger OnPageExit first to let page clean up state
                _currentPage.TriggerPageExit();
                _currentPage.gameObject.SetActive(false);
            }

            // Show target page (immediately, without fade in animation)
            if (targetPage != null)
            {
                // Activate page
                targetPage.gameObject.SetActive(true);

                // Configure page content
                targetPage.Configure(step);

                // Manually trigger ConfigureTopBar and OnPageEnter
                targetPage.TriggerConfigureTopBar();

                // Force CanvasGroup to fully visible, skip fade in animation
                var canvasGroup = targetPage.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1f;
                    canvasGroup.interactable = true;
                    canvasGroup.blocksRaycasts = true;
                }
                else
                {
                    Debug.LogWarning($"[UIManager] {targetPage.GetType().Name} has no CanvasGroup");
                }

                // Trigger OnPageEnter (this starts test countdown logic, etc.)
                targetPage.TriggerPageEnter();

                _currentPage = targetPage;
            }
        }

        /// <summary>
        /// Determine if transition effect is needed
        /// </summary>
        private bool ShouldUseTransition(BasePage currentPage, BasePage targetPage)
        {
            // No transition needed if no current page (first startup)
            if (currentPage == null)
            {
                return false;
            }

            // No transition if switching to same page (different phases within same Page)
            if (currentPage == targetPage)
            {
                return false;
            }

            // Use black screen transition when returning from ScorePage to home
            if (currentPage == _scorePage && targetPage == _homePage)
            {
                return true;
            }

            // Other returns to home (from TeachingPage or TestPage), no transition
            if (targetPage == _homePage)
            {
                return false;
            }

            // All other cross-page switches use transition effect (including from home)
            return true;
        }

        /// <summary>
        /// Get page instance by page type
        /// </summary>
        private BasePage GetPageByType(PageType pageType)
        {
            switch (pageType)
            {
                case PageType.Home:
                    return _homePage;
                case PageType.Teaching:
                    return _teachingPage;
                case PageType.Test:
                    return _testPage;
                case PageType.Score:
                    return _scorePage;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Get page type for step
        /// </summary>
        public PageType GetPageType(FlowStep step)
        {
            switch (step)
            {
                case FlowStep.Home:
                    return PageType.Home;

                case FlowStep.TestIntro:
                case FlowStep.APoseCalibration_Teaching:
                case FlowStep.BalanceIntro:
                case FlowStep.BalanceSideBySide_Teaching:
                case FlowStep.BalanceSemiTandem_Teaching:
                case FlowStep.BalanceTandem_Teaching:
                case FlowStep.SitStandIntro:
                case FlowStep.SitStand_Teaching:
                case FlowStep.WalkIntro:
                case FlowStep.Walk_Teaching:
                    return PageType.Teaching;

                case FlowStep.APoseCalibration:
                case FlowStep.BalanceSideBySide_Test:
                case FlowStep.BalanceSemiTandem_Test:
                case FlowStep.BalanceTandem_Test:
                case FlowStep.SitStand_Test:
                case FlowStep.Walk_Test:
                    return PageType.Test;

                case FlowStep.Score:
                    return PageType.Score;

                default:
                    return PageType.Home;
            }
        }
    }

    /// <summary>
    /// Page type
    /// </summary>
    public enum PageType
    {
        Home,
        Teaching,
        Test,
        Score
    }
}
