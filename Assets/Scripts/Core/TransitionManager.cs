using UnityEngine;
using UnityEngine.UI;
using System;
using DG.Tweening;
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
        [SerializeField] private Ease _fadeInEase = Ease.InQuad;
        [SerializeField] private Ease _fadeOutEase = Ease.OutQuad;

        private Tween _currentTween;

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
            KillCurrentTween();

            if (_blackOverlay == null)
            {
                Debug.LogError("[TransitionManager] Black Overlay is null, cannot execute fade in");
                return;
            }

            _blackOverlay.gameObject.SetActive(true);

            _currentTween = _blackOverlay
                .DOFade(1f, _fadeInDuration)
                .SetEase(_fadeInEase)
                .SetUpdate(true) // Use unscaled time
                .OnComplete(() =>
                {
                    onComplete?.Invoke();
                });
        }

        /// <summary>
        /// Fade from black
        /// </summary>
        public void FadeFromBlack(Action onComplete = null)
        {
            KillCurrentTween();

            if (_blackOverlay == null)
            {
                Debug.LogError("[TransitionManager] Black Overlay is null, cannot execute fade out");
                return;
            }

            _currentTween = _blackOverlay
                .DOFade(0f, _fadeOutDuration)
                .SetEase(_fadeOutEase)
                .SetUpdate(true) // Use unscaled time
                .OnComplete(() =>
                {
                    _blackOverlay.gameObject.SetActive(false);
                    onComplete?.Invoke();
                });
        }

        /// <summary>
        /// Full transition effect: Fade to black → Execute action → Fade from black
        /// </summary>
        /// <param name="onBlackScreen">Action to execute during black screen (e.g., switch pages)</param>
        /// <param name="onComplete">Action to execute after the entire transition completes</param>
        public void TransitionBetweenPages(Action onBlackScreen, Action onComplete = null)
        {
            KillCurrentTween();

            if (_blackOverlay == null)
            {
                Debug.LogError("[TransitionManager] Black Overlay is null, cannot execute transition");
                return;
            }

            _blackOverlay.gameObject.SetActive(true);

            Sequence sequence = DOTween.Sequence();

            // Phase 1: Fade to black
            sequence.Append(
                _blackOverlay.DOFade(1f, _fadeInDuration).SetEase(_fadeInEase)
            );

            // Phase 2: Execute action during black screen (e.g., switch pages)
            sequence.AppendCallback(() =>
            {
                onBlackScreen?.Invoke();
            });

            // Phase 3: Fade from black
            sequence.Append(
                _blackOverlay.DOFade(0f, _fadeOutDuration).SetEase(_fadeOutEase)
            );

            // Phase 4: Notify transition complete
            sequence.OnComplete(() =>
            {
                _blackOverlay.gameObject.SetActive(false);
                onComplete?.Invoke();
            });

            sequence.SetUpdate(true); // Use unscaled time
            _currentTween = sequence;
        }

        /// <summary>
        /// Set to black screen immediately
        /// </summary>
        public void SetBlackImmediate()
        {
            KillCurrentTween();

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
            KillCurrentTween();

            if (_blackOverlay != null)
            {
                Color color = _blackOverlay.color;
                color.a = 0f;
                _blackOverlay.color = color;
                _blackOverlay.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Kill any currently running tween to prevent conflicts
        /// </summary>
        private void KillCurrentTween()
        {
            if (_currentTween != null && _currentTween.IsActive())
            {
                _currentTween.Kill();
                _currentTween = null;
            }
        }

        private void OnDestroy()
        {
            KillCurrentTween();
        }

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
