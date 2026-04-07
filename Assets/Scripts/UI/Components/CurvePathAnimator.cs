using UnityEngine;
using System.Collections.Generic;

namespace SPPB.UI.Components
{
    /// <summary>
    /// Curve Path Animator - Moves objects along a path back and forth
    /// Supports linear with rounded corners mode (straight segments + rounded corners)
    /// </summary>
    public class CurvePathAnimator : MonoBehaviour
    {
        /// <summary>
        /// Path Mode
        /// </summary>
        public enum PathMode
        {
            SmoothCurve,    // Smooth curve (Catmull-Rom spline)
            LinearWithRoundedCorners  // Linear + rounded corners
        }

        [Header("Target Object")]
        [SerializeField] private RectTransform _targetObject;  // Object to move (dot)

        [Header("Path Settings")]
        [SerializeField] private PathMode _pathMode = PathMode.LinearWithRoundedCorners;  // Path mode
        [SerializeField] private List<Vector2> _waypoints = new List<Vector2>();  // Control point positions

        [Header("Rounded Corner Settings (LinearWithRoundedCorners mode)")]
        [SerializeField] private float _cornerRadius = 20f;    // Corner radius
        [SerializeField] private int _cornerSegments = 8;      // Corner sampling count

        [Header("Animation Settings")]
        [SerializeField] private float _duration = 2f;         // Single trip duration
        [SerializeField] private bool _pingPong = true;        // Whether to move back and forth
        [SerializeField] private bool _playOnEnable = true;    // Auto play on enable
        [SerializeField] private AnimationCurve _easingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);  // Easing curve

        [Header("Curve Settings (SmoothCurve mode)")]
        [SerializeField] private int _curveResolution = 50;    // Curve sampling resolution

        // State
        private bool _isPlaying = false;
        private float _progress = 0f;       // Progress 0~1
        private bool _isForward = true;     // Forward or backward

        // Calculated path points
        private List<Vector2> _pathPoints = new List<Vector2>();
        private float[] _cumulativeDistances;
        private float _totalLength;

        private void Awake()
        {
            if (_waypoints.Count >= 2)
            {
                GeneratePathPoints();
            }
        }

        private void OnEnable()
        {
            if (_playOnEnable && _waypoints.Count >= 2)
            {
                Play();
            }
        }

        private void OnDisable()
        {
            Stop();
        }

        private void Update()
        {
            if (!_isPlaying || _targetObject == null || _pathPoints.Count < 2) return;

            // Update progress
            float deltaProgress = Time.deltaTime / _duration;
            if (_isForward)
            {
                _progress += deltaProgress;
                if (_progress >= 1f)
                {
                    _progress = 1f;
                    if (_pingPong)
                    {
                        _isForward = false;
                    }
                    else
                    {
                        _progress = 0f;  // Loop
                    }
                }
            }
            else
            {
                _progress -= deltaProgress;
                if (_progress <= 0f)
                {
                    _progress = 0f;
                    _isForward = true;
                }
            }

            // Apply easing curve
            float easedProgress = _easingCurve.Evaluate(_progress);

            // Get position on path
            Vector2 position = GetPositionOnPath(easedProgress);
            _targetObject.anchoredPosition = position;
        }

        /// <summary>
        /// Generate path points
        /// </summary>
        private void GeneratePathPoints()
        {
            _pathPoints.Clear();

            if (_waypoints.Count < 2) return;

            if (_pathMode == PathMode.SmoothCurve)
            {
                GenerateSmoothCurvePoints();
            }
            else
            {
                GenerateLinearWithRoundedCorners();
            }

            // Calculate cumulative distances
            CalculateCumulativeDistances();
        }

        /// <summary>
        /// Generate smooth curve points (Catmull-Rom spline)
        /// </summary>
        private void GenerateSmoothCurvePoints()
        {
            for (int i = 0; i < _waypoints.Count - 1; i++)
            {
                Vector2 p0 = i > 0 ? _waypoints[i - 1] : _waypoints[i];
                Vector2 p1 = _waypoints[i];
                Vector2 p2 = _waypoints[i + 1];
                Vector2 p3 = i < _waypoints.Count - 2 ? _waypoints[i + 2] : _waypoints[i + 1];

                int steps = _curveResolution / (_waypoints.Count - 1);
                for (int j = 0; j < steps; j++)
                {
                    float t = (float)j / steps;
                    Vector2 point = CatmullRom(p0, p1, p2, p3, t);
                    _pathPoints.Add(point);
                }
            }

            // Add last point
            _pathPoints.Add(_waypoints[_waypoints.Count - 1]);
        }

        /// <summary>
        /// Generate linear path with rounded corners
        /// </summary>
        private void GenerateLinearWithRoundedCorners()
        {
            // Start point
            _pathPoints.Add(_waypoints[0]);

            // Process each intermediate point (corner)
            for (int i = 1; i < _waypoints.Count - 1; i++)
            {
                Vector2 prev = _waypoints[i - 1];
                Vector2 current = _waypoints[i];
                Vector2 next = _waypoints[i + 1];

                // Calculate direction vectors
                Vector2 dirToPrev = (prev - current).normalized;
                Vector2 dirToNext = (next - current).normalized;

                // Calculate segment lengths
                float distToPrev = Vector2.Distance(prev, current);
                float distToNext = Vector2.Distance(current, next);

                // Limit corner radius to half of segment length
                float maxRadius = Mathf.Min(distToPrev * 0.5f, distToNext * 0.5f, _cornerRadius);

                if (maxRadius < 0.1f)
                {
                    // Too short, use sharp corner
                    _pathPoints.Add(current);
                    continue;
                }

                // Calculate corner start and end points
                Vector2 cornerStart = current + dirToPrev * maxRadius;
                Vector2 cornerEnd = current + dirToNext * maxRadius;

                // Add line end point before corner start (if needed)
                if (_pathPoints.Count > 0 && Vector2.Distance(_pathPoints[_pathPoints.Count - 1], cornerStart) > 0.1f)
                {
                    _pathPoints.Add(cornerStart);
                }

                // Generate rounded corner
                GenerateCornerArc(cornerStart, current, cornerEnd, maxRadius);
            }

            // End point
            _pathPoints.Add(_waypoints[_waypoints.Count - 1]);
        }

        /// <summary>
        /// Generate corner arc
        /// </summary>
        private void GenerateCornerArc(Vector2 start, Vector2 corner, Vector2 end, float radius)
        {
            // Calculate direction from corner to start and end
            Vector2 dirToStart = (start - corner).normalized;
            Vector2 dirToEnd = (end - corner).normalized;

            // Calculate angle between directions
            float dot = Vector2.Dot(dirToStart, dirToEnd);
            float cornerAngle = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f));  // Corner interior angle

            if (cornerAngle < 0.01f || cornerAngle > Mathf.PI - 0.01f)
            {
                // Almost straight or completely reversed, no corner needed
                return;
            }

            // Center is on angle bisector, distance from corner is radius / sin(halfAngle)
            float halfAngle = cornerAngle * 0.5f;
            float centerDist = radius / Mathf.Sin(halfAngle);

            // Angle bisector direction (pointing to corner interior)
            Vector2 bisector = (dirToStart + dirToEnd).normalized;
            Vector2 center = corner + bisector * centerDist;

            // Calculate arc start and end angles relative to center
            Vector2 startFromCenter = start - center;
            Vector2 endFromCenter = end - center;

            float arcStartAngle = Mathf.Atan2(startFromCenter.y, startFromCenter.x);
            float arcEndAngle = Mathf.Atan2(endFromCenter.y, endFromCenter.x);

            // Calculate arc angle difference, choose shorter arc
            float arcAngleDiff = arcEndAngle - arcStartAngle;

            // Ensure we take the shorter arc (interior arc)
            if (arcAngleDiff > Mathf.PI) arcAngleDiff -= 2f * Mathf.PI;
            if (arcAngleDiff < -Mathf.PI) arcAngleDiff += 2f * Mathf.PI;

            // Generate arc points
            for (int i = 0; i <= _cornerSegments; i++)
            {
                float t = (float)i / _cornerSegments;
                float angle = arcStartAngle + arcAngleDiff * t;
                Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                _pathPoints.Add(point);
            }
        }

        /// <summary>
        /// Catmull-Rom spline interpolation
        /// </summary>
        private Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }

        /// <summary>
        /// Calculate cumulative distances (for uniform speed movement)
        /// </summary>
        private void CalculateCumulativeDistances()
        {
            _cumulativeDistances = new float[_pathPoints.Count];
            _cumulativeDistances[0] = 0f;
            _totalLength = 0f;

            for (int i = 1; i < _pathPoints.Count; i++)
            {
                float segmentLength = Vector2.Distance(_pathPoints[i - 1], _pathPoints[i]);
                _totalLength += segmentLength;
                _cumulativeDistances[i] = _totalLength;
            }
        }

        /// <summary>
        /// Get position on path based on progress (uniform speed)
        /// </summary>
        private Vector2 GetPositionOnPath(float progress)
        {
            if (_pathPoints.Count < 2) return Vector2.zero;

            float targetDistance = progress * _totalLength;

            // Find corresponding segment
            for (int i = 1; i < _pathPoints.Count; i++)
            {
                if (_cumulativeDistances[i] >= targetDistance)
                {
                    float segmentStart = _cumulativeDistances[i - 1];
                    float segmentEnd = _cumulativeDistances[i];
                    float segmentLength = segmentEnd - segmentStart;

                    if (segmentLength < 0.001f) return _pathPoints[i];

                    float t = (targetDistance - segmentStart) / segmentLength;
                    return Vector2.Lerp(_pathPoints[i - 1], _pathPoints[i], t);
                }
            }

            return _pathPoints[_pathPoints.Count - 1];
        }

        /// <summary>
        /// Start playback
        /// </summary>
        public void Play()
        {
            if (_waypoints.Count < 2)
            {
                Debug.LogWarning("[CurvePathAnimator] At least 2 control points required!");
                return;
            }

            if (_pathPoints.Count < 2)
            {
                GeneratePathPoints();
            }

            _isPlaying = true;
            _progress = 0f;
            _isForward = true;

            // Set initial position
            if (_targetObject != null && _pathPoints.Count > 0)
            {
                _targetObject.anchoredPosition = _pathPoints[0];
            }
        }

        /// <summary>
        /// Stop playback
        /// </summary>
        public void Stop()
        {
            _isPlaying = false;
        }

        /// <summary>
        /// Pause playback
        /// </summary>
        public void Pause()
        {
            _isPlaying = false;
        }

        /// <summary>
        /// Resume playback
        /// </summary>
        public void Resume()
        {
            _isPlaying = true;
        }

        /// <summary>
        /// Set animation duration
        /// </summary>
        public void SetDuration(float duration)
        {
            _duration = duration;
        }

        /// <summary>
        /// Set corner radius
        /// </summary>
        public void SetCornerRadius(float radius)
        {
            _cornerRadius = radius;
            GeneratePathPoints();
        }

        /// <summary>
        /// Regenerate path (call after modifying control points)
        /// </summary>
        public void RegeneratePath()
        {
            GeneratePathPoints();
        }

        /// <summary>
        /// Check if animation is playing
        /// </summary>
        public bool IsPlaying => _isPlaying;

#if UNITY_EDITOR
        /// <summary>
        /// Draw path preview in Scene window
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (_waypoints == null || _waypoints.Count < 2) return;

            // Get RectTransform
            RectTransform rt = GetComponent<RectTransform>();
            if (rt == null) return;

            // Draw control points
            Gizmos.color = Color.yellow;
            foreach (var point in _waypoints)
            {
                Vector3 worldPos = rt.TransformPoint(point);
                Gizmos.DrawSphere(worldPos, 5f);
            }

            // Generate preview path
            List<Vector2> previewPoints = new List<Vector2>();

            if (_pathMode == PathMode.SmoothCurve)
            {
                // Smooth curve preview
                for (int i = 0; i < _waypoints.Count - 1; i++)
                {
                    Vector2 p0 = i > 0 ? _waypoints[i - 1] : _waypoints[i];
                    Vector2 p1 = _waypoints[i];
                    Vector2 p2 = _waypoints[i + 1];
                    Vector2 p3 = i < _waypoints.Count - 2 ? _waypoints[i + 2] : _waypoints[i + 1];

                    for (int j = 0; j <= 10; j++)
                    {
                        float t = (float)j / 10;
                        Vector2 point = CatmullRom(p0, p1, p2, p3, t);
                        previewPoints.Add(point);
                    }
                }
            }
            else
            {
                // Linear + rounded corner preview
                // Temporary path storage
                List<Vector2> tempPath = new List<Vector2>();
                tempPath.Add(_waypoints[0]);

                for (int i = 1; i < _waypoints.Count - 1; i++)
                {
                    Vector2 prev = _waypoints[i - 1];
                    Vector2 current = _waypoints[i];
                    Vector2 next = _waypoints[i + 1];

                    Vector2 dirToPrev = (prev - current).normalized;
                    Vector2 dirToNext = (next - current).normalized;

                    float distToPrev = Vector2.Distance(prev, current);
                    float distToNext = Vector2.Distance(current, next);
                    float maxRadius = Mathf.Min(distToPrev * 0.5f, distToNext * 0.5f, _cornerRadius);

                    if (maxRadius < 0.1f)
                    {
                        tempPath.Add(current);
                        continue;
                    }

                    Vector2 cornerStart = current + dirToPrev * maxRadius;
                    Vector2 cornerEnd = current + dirToNext * maxRadius;

                    tempPath.Add(cornerStart);

                    // Simplified rounded corner preview (using corrected algorithm)
                    float dot = Vector2.Dot(dirToPrev, dirToNext);
                    float cornerAngle = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f));

                    if (cornerAngle >= 0.01f && cornerAngle <= Mathf.PI - 0.01f)
                    {
                        float halfAngle = cornerAngle * 0.5f;
                        float centerDist = maxRadius / Mathf.Sin(halfAngle);
                        Vector2 bisector = (dirToPrev + dirToNext).normalized;
                        Vector2 center = current + bisector * centerDist;

                        // Calculate arc start and end angles relative to center
                        Vector2 startFromCenter = cornerStart - center;
                        Vector2 endFromCenter = cornerEnd - center;
                        float arcStartAngle = Mathf.Atan2(startFromCenter.y, startFromCenter.x);
                        float arcEndAngle = Mathf.Atan2(endFromCenter.y, endFromCenter.x);

                        float arcAngleDiff = arcEndAngle - arcStartAngle;
                        if (arcAngleDiff > Mathf.PI) arcAngleDiff -= 2f * Mathf.PI;
                        if (arcAngleDiff < -Mathf.PI) arcAngleDiff += 2f * Mathf.PI;

                        for (int j = 0; j <= 8; j++)
                        {
                            float t = (float)j / 8;
                            float angle = arcStartAngle + arcAngleDiff * t;
                            Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * maxRadius;
                            tempPath.Add(point);
                        }
                    }
                }

                tempPath.Add(_waypoints[_waypoints.Count - 1]);
                previewPoints = tempPath;
            }

            // Draw path
            Gizmos.color = Color.cyan;
            for (int i = 0; i < previewPoints.Count - 1; i++)
            {
                Vector3 start = rt.TransformPoint(previewPoints[i]);
                Vector3 end = rt.TransformPoint(previewPoints[i + 1]);
                Gizmos.DrawLine(start, end);
            }
        }
#endif
    }
}
