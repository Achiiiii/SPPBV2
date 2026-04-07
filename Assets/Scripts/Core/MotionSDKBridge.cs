using System;
using System.Runtime.InteropServices;
using UnityEngine;
using SPPB.Utils;

namespace SPPB.Core
{
    /// <summary>
    /// Motion SDK Bridge - Wrapper for motion_sdk native library
    /// Handles all DllImport calls and score retrieval
    /// </summary>
    public class MotionSDKBridge : Singleton<MotionSDKBridge>
    {
        #region DllImport Declarations

#if UNITY_ANDROID && !UNITY_EDITOR
        private const string LIB_NAME = "motion_sdk";   // Android .so
#else
        private const string LIB_NAME = "motion_sdk";   // Windows .dll
#endif

        [DllImport(LIB_NAME)]
        private static extern void initialize();

        [DllImport(LIB_NAME)]
        private static extern void start_exercise(int type);

        [DllImport(LIB_NAME)]
        private static extern void stop_exercise();

        [DllImport(LIB_NAME)]
        private static extern IntPtr process_points(float[] points, int num_points, int dim);

        [DllImport(LIB_NAME)]
        private static extern IntPtr get_score();

        #endregion

        #region Data Structures

        /// <summary>
        /// SDK score data structure (matches SDK JSON format)
        /// </summary>
        [Serializable]
        public class SDKScoreData
        {
            public int score1_1;  // Side-by-side stance (0-1)
            public int score1_2;  // Semi-tandem stance (0-1)
            public int score1_3;  // Tandem stance (0-2)
            public int score2;    // Walk test (0-4)
            public int score3;    // Sit-stand test (0-4)
            public int total;     // Total score (0-12)
        }

        #endregion

        #region Exercise Types

        /// <summary>
        /// Exercise type constants
        /// </summary>
        public static class ExerciseType
        {
            public const int BalanceSideBySide = 1;   // Side-by-side stance
            public const int BalanceSemiTandem = 2;   // Semi-tandem stance
            public const int BalanceTandem = 3;       // Tandem stance
            public const int Walk = 4;                // Walk test
            public const int SitStand = 5;            // Sit-stand test
        }

        #endregion

        private bool _isInitialized = false;
        private int _currentExerciseType = 0;

        /// <summary>
        /// Whether SDK is initialized
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Current exercise type (0 = none)
        /// </summary>
        public int CurrentExerciseType => _currentExerciseType;

        /// <summary>
        /// Event fired when score is retrieved
        /// </summary>
        public event Action<SDKScoreData> OnScoreReceived;

        private void Start()
        {
            Initialize();
        }

        /// <summary>
        /// Initialize the SDK
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized)
            {
                Debug.LogWarning("[MotionSDKBridge] SDK already initialized");
                return;
            }

            try
            {
                initialize();
                _isInitialized = true;
                Debug.Log("[MotionSDKBridge] SDK initialized successfully");
            }
            catch (Exception e)
            {
                Debug.LogError($"[MotionSDKBridge] Failed to initialize SDK: {e.Message}");
            }
        }

        /// <summary>
        /// Start a specific exercise
        /// </summary>
        /// <param name="type">Exercise type (1-5)</param>
        public void StartExercise(int type)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[MotionSDKBridge] SDK not initialized, initializing now...");
                Initialize();
            }

            if (type < 1 || type > 5)
            {
                Debug.LogError($"[MotionSDKBridge] Invalid exercise type: {type}. Must be 1-5");
                return;
            }

            try
            {
                start_exercise(type);
                _currentExerciseType = type;
                Debug.Log($"[MotionSDKBridge] Started exercise type {type}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[MotionSDKBridge] Failed to start exercise: {e.Message}");
            }
        }

        /// <summary>
        /// Stop the current exercise
        /// </summary>
        public void StopExercise()
        {
            try
            {
                stop_exercise();
                Debug.Log($"[MotionSDKBridge] Stopped exercise type {_currentExerciseType}");
                _currentExerciseType = 0;
            }
            catch (Exception e)
            {
                Debug.LogError($"[MotionSDKBridge] Failed to stop exercise: {e.Message}");
            }
        }

        /// <summary>
        /// Process skeleton points (called by pose detection system)
        /// </summary>
        /// <param name="points">Flat array of 3D coordinates [x1,y1,z1, x2,y2,z2, ...]</param>
        /// <param name="numPoints">Number of points</param>
        /// <param name="dim">Dimension (usually 3 for 3D)</param>
        /// <returns>JSON result string from SDK, or null on failure</returns>
        public string ProcessPoints(float[] points, int numPoints, int dim = 3)
        {
            if (points == null || points.Length == 0)
            {
                return null;
            }

            try
            {
                IntPtr resultPtr = process_points(points, numPoints, dim);
                if (resultPtr != IntPtr.Zero)
                {
                    string json = Marshal.PtrToStringAnsi(resultPtr);
                    return json;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MotionSDKBridge] Failed to process points: {e.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get final score from SDK and update ScoreManager
        /// </summary>
        /// <returns>SDKScoreData or null on failure</returns>
        public SDKScoreData GetFinalScore()
        {
            try
            {
                IntPtr scorePtr = get_score();
                if (scorePtr == IntPtr.Zero)
                {
                    Debug.LogError("[MotionSDKBridge] get_score returned null pointer");
                    return null;
                }

                string json = Marshal.PtrToStringAnsi(scorePtr);
                if (string.IsNullOrEmpty(json))
                {
                    Debug.LogError("[MotionSDKBridge] get_score returned empty JSON");
                    return null;
                }

                Debug.Log($"[MotionSDKBridge] Score JSON: {json}");

                SDKScoreData scoreData = JsonUtility.FromJson<SDKScoreData>(json);
                if (scoreData != null)
                {
                    // Update ScoreManager
                    UpdateScoreManager(scoreData);

                    // Fire event
                    OnScoreReceived?.Invoke(scoreData);
                }

                return scoreData;
            }
            catch (Exception e)
            {
                Debug.LogError($"[MotionSDKBridge] Failed to get score: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Update ScoreManager with SDK score data
        /// </summary>
        private void UpdateScoreManager(SDKScoreData scoreData)
        {
            if (ScoreManager.Instance == null)
            {
                Debug.LogError("[MotionSDKBridge] ScoreManager not found");
                return;
            }

            ScoreManager.Instance.SetAllScoresFromSDK(
                scoreData.score1_1,
                scoreData.score1_2,
                scoreData.score1_3,
                scoreData.score2,
                scoreData.score3
            );

            Debug.Log($"[MotionSDKBridge] ScoreManager updated - Balance: {scoreData.score1_1 + scoreData.score1_2 + scoreData.score1_3}, Walk: {scoreData.score2}, SitStand: {scoreData.score3}, Total: {scoreData.total}");
        }

        #region Debug Methods (Editor Only)

#if UNITY_EDITOR
        /// <summary>
        /// Test method to simulate score retrieval (Editor only)
        /// </summary>
        [ContextMenu("Test Get Score")]
        public void TestGetScore()
        {
            var score = GetFinalScore();
            if (score != null)
            {
                Debug.Log($"[TEST] Score received: 1_1={score.score1_1}, 1_2={score.score1_2}, 1_3={score.score1_3}, 2={score.score2}, 3={score.score3}, Total={score.total}");
            }
        }

        /// <summary>
        /// Test method to set mock scores (Editor only)
        /// </summary>
        [ContextMenu("Set Mock Scores")]
        public void SetMockScores()
        {
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.SetAllScoresFromSDK(1, 1, 2, 3, 3);
                Debug.Log("[TEST] Mock scores set: Balance=4, Walk=3, SitStand=3, Total=10");
            }
        }
#endif

        #endregion
    }
}
