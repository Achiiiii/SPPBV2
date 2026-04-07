using UnityEngine;
using SPPB.Utils;

namespace SPPB.Core
{
    /// <summary>
    /// Score Manager - Responsible for storing test scores from SDK
    /// All scoring logic is handled by SDK, this class only receives and stores scores
    /// </summary>
    public class ScoreManager : Singleton<ScoreManager>
    {
        // Test scores (set directly by SDK)
        private int _balanceScore = 0;      // Balance test total (0-4)
        private int _sitStandScore = 0;     // Sit-stand test (0-4)
        private int _walkScore = 0;         // Walk test (0-4)

        // Balance test sub-scores (set directly by SDK)
        private int _balanceSideBySideScore = 0;    // Side-by-side stance (0-1)
        private int _balanceSemiTandemScore = 0;    // Semi-tandem stance (0-1)
        private int _balanceTandemScore = 0;        // Tandem stance (0-2)

        private const int MAX_BALANCE_SCORE = 4;    // Balance test max score (1+1+2)
        private const int MAX_SINGLE_SCORE = 4;     // Single test max score

        /// <summary>
        /// Reset all scores
        /// </summary>
        public void ResetAllScores()
        {
            _balanceScore = 0;
            _sitStandScore = 0;
            _walkScore = 0;

            _balanceSideBySideScore = 0;
            _balanceSemiTandemScore = 0;
            _balanceTandemScore = 0;
        }

        #region Balance Test

        /// <summary>
        /// Set balance test total score (directly from SDK)
        /// </summary>
        public void SetBalanceScore(int score)
        {
            _balanceScore = Mathf.Clamp(score, 0, MAX_BALANCE_SCORE);
        }

        /// <summary>
        /// Set side-by-side stance score (SDK Type 1)
        /// </summary>
        public void SetBalanceSideBySideScore(int score)
        {
            _balanceSideBySideScore = Mathf.Clamp(score, 0, 1);
            UpdateBalanceTotalScore();
        }

        /// <summary>
        /// Set semi-tandem stance score (SDK Type 2)
        /// </summary>
        public void SetBalanceSemiTandemScore(int score)
        {
            _balanceSemiTandemScore = Mathf.Clamp(score, 0, 1);
            UpdateBalanceTotalScore();
        }

        /// <summary>
        /// Set tandem stance score (SDK Type 3)
        /// </summary>
        public void SetBalanceTandemScore(int score)
        {
            _balanceTandemScore = Mathf.Clamp(score, 0, 2);
            UpdateBalanceTotalScore();
        }

        /// <summary>
        /// Update balance total score (sum of three sub-scores)
        /// </summary>
        private void UpdateBalanceTotalScore()
        {
            _balanceScore = _balanceSideBySideScore + _balanceSemiTandemScore + _balanceTandemScore;
        }

        public int GetBalanceScore() => _balanceScore;
        public int GetBalanceSideBySideScore() => _balanceSideBySideScore;
        public int GetBalanceSemiTandemScore() => _balanceSemiTandemScore;
        public int GetBalanceTandemScore() => _balanceTandemScore;

        #endregion

        #region Sit-Stand Test

        /// <summary>
        /// Set sit-stand test score (SDK Type 5)
        /// </summary>
        public void SetSitStandScore(int score)
        {
            _sitStandScore = Mathf.Clamp(score, 0, MAX_SINGLE_SCORE);
        }

        public int GetSitStandScore() => _sitStandScore;

        #endregion

        #region Walk Test

        /// <summary>
        /// Set walk test score (SDK Type 4)
        /// </summary>
        public void SetWalkScore(int score)
        {
            _walkScore = Mathf.Clamp(score, 0, MAX_SINGLE_SCORE);
        }

        public int GetWalkScore() => _walkScore;

        #endregion

        #region SDK Integration

        /// <summary>
        /// Set all scores from SDK at once
        /// </summary>
        /// <param name="score1_1">Side-by-side stance score (0-1)</param>
        /// <param name="score1_2">Semi-tandem stance score (0-1)</param>
        /// <param name="score1_3">Tandem stance score (0-2)</param>
        /// <param name="score2">Walk test score (0-4)</param>
        /// <param name="score3">Sit-stand test score (0-4)</param>
        public void SetAllScoresFromSDK(int score1_1, int score1_2, int score1_3, int score2, int score3)
        {
            SetBalanceSideBySideScore(score1_1);
            SetBalanceSemiTandemScore(score1_2);
            SetBalanceTandemScore(score1_3);
            SetWalkScore(score2);
            SetSitStandScore(score3);
        }

        #endregion

        #region Total Score

        /// <summary>
        /// Get total score (0-12)
        /// </summary>
        public int GetTotalScore()
        {
            return _balanceScore + _sitStandScore + _walkScore;
        }

        #endregion
    }
}
