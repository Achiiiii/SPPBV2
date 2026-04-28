using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using SPPB.Utils;

namespace SPPB.Core
{
    /// <summary>
    /// Scene Transition Manager - Global black fade in/out effects
    /// </summary>
    public class TransitionManager : Singleton<TransitionManager>
    {
        [Header("Transition Settings")]
        [SerializeField] private Canvas _transitionCanvas;
        [SerializeField] private Image _blackOverlay;
        [SerializeField] private float _fadeInDuration = 0.5f;   // Time to fade to black
        [SerializeField] private float _fadeOutDuration = 0.5f;  // Time to fade from black

        private Coroutine _currentTransition;

        protected override void Awake()
        {
            base.Awake();

            // Ensure Canvas is on top layer
            if (_transitionCanvas != null)
            {
                // Force set to Screen Space - Overlay
                _transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _transitionCanvas.sortingOrder = 9999;
            }
            else
            {
                Debug.LogError("[TransitionManager] Transition Canvas not set!");
            }

            // Hide black overlay on initialization
            if (_blackOverlay != null)
            {
                Color color = _blackOverlay.color;
                color.a = 0f;
                _blackOverlay.color = color;
                _blackOverlay.gameObject.SetActive(false);
            }
            else
            {
                Debug.LogError("[TransitionManager] Black Overlay not set!");
            }
        }

        /// <summary>
        /// Fade to black
        /// </summary>
        public void FadeToBlack(Action onComplete = null)
        {
            if (_currentTransition != null)
            {
                StopCoroutine(_currentTransition);
            }

            _currentTransition = StartCoroutine(FadeToBlackCoroutine(onComplete));
        }

        /// <summary>
        /// Fade from black
        /// </summary>
        public void FadeFromBlack(Action onComplete = null)
        {
            if (_currentTransition != null)
            {
                StopCoroutine(_currentTransition);
            }

            _currentTransition = StartCoroutine(FadeFromBlackCoroutine(onComplete));
        }

        /// <summary>
        /// Full transition effect: Fade to black → Execute action → Fade from black
        /// </summary>
        /// <param name="onBlackScreen">Action to execute during black screen (e.g., switch pages)</param>
        /// <param name="onComplete">Action to execute after the entire transition completes</param>
        public void TransitionBetweenPages(Action onBlackScreen, Action onComplete = null)
        {
            if (_currentTransition != null)
            {
                StopCoroutine(_currentTransition);
            }

            _currentTransition = StartCoroutine(TransitionCoroutine(onBlackScreen, onComplete));
        }

        /// <summary>
        /// Set to black screen immediately
        /// </summary>
        public void SetBlackImmediate()
        {
            if (_blackOverlay != null)
            {
                _blackOverlay.gameObject.SetActive(true);
                Color color = _blackOverlay.color;
                color.a = 1f;
                _blackOverlay.color = color;
            }
        }

        /// <summary>
        /// Clear black screen immediately
        /// </summary>
        public void ClearBlackImmediate()
        {
            if (_blackOverlay != null)
            {
                Color color = _blackOverlay.color;
                color.a = 0f;
                _blackOverlay.color = color;
                _blackOverlay.gameObject.SetActive(false);
            }
        }

        #region Coroutines

        private IEnumerator FadeToBlackCoroutine(Action onComplete)
        {
            if (_blackOverlay == null)
            {
                Debug.LogError("[TransitionManager] Black Overlay is null, cannot execute fade in");
                yield break;
            }

            _blackOverlay.gameObject.SetActive(true);

            float elapsedTime = 0f;
            Color color = _blackOverlay.color;

            while (elapsedTime < _fadeInDuration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                color.a = Mathf.Clamp01(elapsedTime / _fadeInDuration);
                _blackOverlay.color = color;
                yield return null;
            }

            // Ensure complete black screen
            color.a = 1f;
            _blackOverlay.color = color;

            onComplete?.Invoke();
        }

        private IEnumerator FadeFromBlackCoroutine(Action onComplete)
        {
            if (_blackOverlay == null)
            {
                Debug.LogError("[TransitionManager] Black Overlay is null, cannot execute fade out");
                yield break;
            }

            float elapsedTime = 0f;
            Color color = _blackOverlay.color;
            float startAlpha = color.a;

            while (elapsedTime < _fadeOutDuration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                color.a = Mathf.Lerp(startAlpha, 0f, elapsedTime / _fadeOutDuration);
                _blackOverlay.color = color;
                yield return null;
            }

            // Ensure complete transparency
            color.a = 0f;
            _blackOverlay.color = color;
            _blackOverlay.gameObject.SetActive(false);

            onComplete?.Invoke();
        }

        private IEnumerator TransitionCoroutine(Action onBlackScreen, Action onComplete = null)
        {
            // Phase 1: Fade to black
            yield return FadeToBlackCoroutine(null);

            // Phase 2: Execute action during black screen (e.g., switch pages)
            onBlackScreen?.Invoke();

            // Phase 3: Fade from black
            yield return FadeFromBlackCoroutine(null);

            // Phase 4: Notify transition complete
            onComplete?.Invoke();
        }

        #endregion

        #region Inspector Settings

        /// <summary>
        /// Set fade in duration
        /// </summary>
        public void SetFadeInDuration(float duration)
        {
            _fadeInDuration = Mathf.Max(0.1f, duration);
        }

        /// <summary>
        /// Set fade out duration
        /// </summary>
        public void SetFadeOutDuration(float duration)
        {
            _fadeOutDuration = Mathf.Max(0.1f, duration);
        }

        #endregion
    }
}
