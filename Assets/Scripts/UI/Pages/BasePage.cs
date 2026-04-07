using UnityEngine;
using System.Collections;
using SPPB.Data;
using SPPB.Core;

namespace SPPB.UI.Pages
{
    /// <summary>
    /// Base Page - Abstract parent class for all pages
    /// </summary>
    public abstract class BasePage : MonoBehaviour
    {
        [Header("Page Settings")]
        [SerializeField] protected CanvasGroup _canvasGroup;

        [Header("Transition Settings")]
        [SerializeField] protected bool _useTransition = true;          // Whether to use transition effects
        [SerializeField] protected float _fadeInDuration = 0.3f;        // Fade in duration
        [SerializeField] protected float _fadeOutDuration = 0.3f;       // Fade out duration

        /// <summary>
        /// Whether the page is currently visible
        /// </summary>
        public bool IsVisible { get; private set; }

        /// <summary>
        /// Transition animation coroutine
        /// </summary>
        private Coroutine _transitionCoroutine;

        /// <summary>
        /// Initialize page (called only once)
        /// </summary>
        public virtual void Initialize()
        {
            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
            }

            // Ensure page is hidden on initialization
            ForceHideImmediate();
        }

        /// <summary>
        /// Force immediate hide (without animation, for initialization)
        /// </summary>
        protected void ForceHideImmediate()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }

            gameObject.SetActive(false);
            IsVisible = false;
        }

        /// <summary>
        /// Show page
        /// </summary>
        public virtual void Show()
        {
            // Stop previous transition animation
            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
            }

            gameObject.SetActive(true);
            IsVisible = true;

            // Configure top bar
            ConfigureTopBar();

            if (_useTransition && _canvasGroup != null)
            {
                // Use fade in effect
                _transitionCoroutine = StartCoroutine(FadeIn());
            }
            else
            {
                // Show directly
                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = 1f;
                    _canvasGroup.interactable = true;
                    _canvasGroup.blocksRaycasts = true;
                }
                OnPageEnter();
            }
        }

        /// <summary>
        /// Hide page
        /// </summary>
        public virtual void Hide()
        {
            // Stop previous transition animation
            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
            }

            // If page hasn't been shown (alpha is 0 or near 0), hide directly without animation
            bool shouldUseTransition = _useTransition && _canvasGroup != null && _canvasGroup.alpha > 0.01f;

            if (shouldUseTransition)
            {
                // Use fade out effect
                _transitionCoroutine = StartCoroutine(FadeOut());
            }
            else
            {
                // Hide directly
                OnPageExit();

                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = 0f;
                    _canvasGroup.interactable = false;
                    _canvasGroup.blocksRaycasts = false;
                }

                gameObject.SetActive(false);
                IsVisible = false;
            }
        }

        /// <summary>
        /// Configure page content (show different content based on step)
        /// </summary>
        public virtual void Configure(FlowStep step)
        {
            // Subclasses override this method to set UI content for different steps
        }

        /// <summary>
        /// Called when entering page (public, can be called externally)
        /// </summary>
        public void TriggerPageEnter()
        {
            OnPageEnter();
        }

        /// <summary>
        /// Called when exiting page (public, can be called externally)
        /// </summary>
        public void TriggerPageExit()
        {
            OnPageExit();
        }

        /// <summary>
        /// Called when entering page
        /// </summary>
        protected virtual void OnPageEnter()
        {
        }

        /// <summary>
        /// Called when exiting page
        /// </summary>
        protected virtual void OnPageExit()
        {
        }

        /// <summary>
        /// Configure top bar - subclass must implement (public, can be called externally)
        /// </summary>
        public void TriggerConfigureTopBar()
        {
            ConfigureTopBar();
        }

        /// <summary>
        /// Configure top bar - subclass must implement
        /// </summary>
        protected abstract void ConfigureTopBar();

        #region Transition Animations

        /// <summary>
        /// Fade in coroutine
        /// </summary>
        private IEnumerator FadeIn()
        {
            // Initial setup
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            float elapsedTime = 0f;

            // Fade in animation
            while (elapsedTime < _fadeInDuration)
            {
                elapsedTime += Time.unscaledDeltaTime; // Use unscaledDeltaTime to avoid pause effects
                _canvasGroup.alpha = Mathf.Clamp01(elapsedTime / _fadeInDuration);
                yield return null;
            }

            // Ensure fully visible
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;

            OnPageEnter();
        }

        /// <summary>
        /// Fade out coroutine
        /// </summary>
        private IEnumerator FadeOut()
        {
            OnPageExit();

            // Initial setup
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            float elapsedTime = 0f;
            float startAlpha = _canvasGroup.alpha;

            // Fade out animation
            while (elapsedTime < _fadeOutDuration)
            {
                elapsedTime += Time.unscaledDeltaTime; // Use unscaledDeltaTime to avoid pause effects
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsedTime / _fadeOutDuration);
                yield return null;
            }

            // Ensure fully hidden
            _canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
            IsVisible = false;
        }

        #endregion
    }
}
