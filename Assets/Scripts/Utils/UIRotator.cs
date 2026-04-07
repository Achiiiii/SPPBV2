using UnityEngine;

namespace SPPB.Utils
{
    /// <summary>
    /// Continuous 360-degree rotation for UI objects
    /// </summary>
    public class UIRotator : MonoBehaviour
    {
        [Header("Rotation Settings")]
        [SerializeField] private float _rotationSpeed = 90f;  // Rotation angle per second
        [SerializeField] private bool _clockwise = true;      // Clockwise rotation

        private RectTransform _rectTransform;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        private void Update()
        {
            float direction = _clockwise ? -1f : 1f;
            float rotation = _rotationSpeed * direction * Time.deltaTime;

            if (_rectTransform != null)
            {
                _rectTransform.Rotate(0f, 0f, rotation);
            }
            else
            {
                transform.Rotate(0f, 0f, rotation);
            }
        }

        /// <summary>
        /// Set rotation speed
        /// </summary>
        public void SetSpeed(float speed)
        {
            _rotationSpeed = speed;
        }

        /// <summary>
        /// Set rotation direction
        /// </summary>
        public void SetClockwise(bool clockwise)
        {
            _clockwise = clockwise;
        }
    }
}
