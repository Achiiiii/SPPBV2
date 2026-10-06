using UnityEngine;

namespace SPPB.Core.Detection
{
    /// <summary>
    /// 步行/前進速度（Type 4）。
    /// 場地：受測者起點站在離機器約 4.5~5m，從起點往機器走滿 walkDistanceTarget(3m) 即完成，
    /// 終點離機器仍有 1.5~2m，全身不會出框（太靠近鏡頭 VideoPose 會丟失頭手腳）。
    ///  - 起點：開始後前 BaselineFrames 個有效幀的骨盆深度平均（比單一幀穩定）。
    ///  - 計時：真實時間，沒偵測到骨架的幀也照樣累加，避免掉幀時秒數偏少、分數偏高。
    ///  - 達標後依耗時給 0~4；超時給 0。
    /// </summary>
    public class WalkDetector : IMovementDetector
    {
        private const int BaselineFrames = 5;
        // 追蹤快丟失時 VideoPose 偶爾吐出深度錯亂的一幀(實測出現 diff=60,289,520)，會造成假完成。
        private const float MaxDepthJumpPerFrame = 100f;   // 單幀深度跳動上限(×1000單位)，超過視為錯誤幀丟棄
        private const int FinishConfirmFrames = 3;          // 需連續幾幀超過門檻才算完成
        private float _lastValidDepth;
        private bool _hasLastValidDepth;
        private int _aboveTargetFrames;
        private int _invalidFrames;
        // 走到終點附近太靠近鏡頭會追丟骨架：已走到門檻 NearFinishRatio 以上才追丟 → 以走到最遠那刻的時間算完成
        private const float NearFinishRatio = 0.9f;
        private const float LostConfirmSec = 0.5f;
        private float _maxDiffTime, _lostTime;

        private readonly SppbThresholds _cfg;
        private bool _running, _finished, _baselineReady;
        private float _baselineDepth, _baselineSum, _elapsed;
        private int _baselineSamples, _finalScore;
        private MovementResult _result;

        public WalkDetector(SppbThresholds cfg) { _cfg = cfg; }

        public int ExerciseType => 4;
        public bool IsRunning => _running;
        public MovementResult Result => _result;

        /// <summary>起點深度（×1000）；尚未取得時為 0。</summary>
        public float BaselineDepth => _baselineReady ? _baselineDepth : 0f;
        public bool BaselineReady => _baselineReady;
        /// <summary>最近一幀的骨盆深度（×1000），校準用。</summary>
        public float LastDepth { get; private set; }
        /// <summary>本次追蹤到的最大有效 diff（已排除錯誤幀），校準用。</summary>
        public float MaxDiff { get; private set; }

        public void Begin() { Reset(); _running = true; }
        public void Reset()
        {
            _running = false; _finished = false; _baselineReady = false;
            _baselineDepth = 0f; _baselineSum = 0f; _baselineSamples = 0;
            _elapsed = 0f; _finalScore = 0; LastDepth = 0f; MaxDiff = 0f;
            _lastValidDepth = 0f; _hasLastValidDepth = false; _aboveTargetFrames = 0; _invalidFrames = 0;
            _maxDiffTime = 0f; _lostTime = 0f;
            _result = MovementResult.Idle(4);
        }

        /// <param name="joints">可為 null（本幀沒偵測到骨架）：只累加時間與檢查逾時。</param>
        public MovementResult Tick(Vector3[] joints, float dt)
        {
            if (!_running || _finished) return _result;

            _elapsed += dt;
            _result.type = 4;
            _result.running = true;
            _result.justFinished = false;
            _result.elapsed = _elapsed;

            bool hasPose = joints != null && joints.Length >= SppbJoints.MinRequiredJoints;
            _lostTime = hasPose ? 0f : _lostTime + dt;
            if (!hasPose && _lostTime >= LostConfirmSec && _baselineReady && !_cfg.walkCalibrationMode
                && MaxDiff >= _cfg.walkDistanceTarget * NearFinishRatio)
            {
                _result.elapsed = _maxDiffTime;
                return Finish(ScoreByTime(_maxDiffTime));
            }

            if (hasPose)
            {
                float z = SppbJoints.Depth(joints[SppbJoints.Hips]);
                bool validFrame = !float.IsNaN(z) && !float.IsInfinity(z)
                    && (!_hasLastValidDepth || Mathf.Abs(z - _lastValidDepth) <= MaxDepthJumpPerFrame);

                if (!validFrame)
                {
                    // 錯誤幀：不更新距離，只計時。若連續太多幀都被判無效，代表參考值本身可能是錯誤幀 → 重新對齊
                    _invalidFrames++;
                    if (_invalidFrames >= 10 && !float.IsNaN(z) && !float.IsInfinity(z))
                    {
                        _lastValidDepth = z;
                        _invalidFrames = 0;
                    }
                }
                else if (!_baselineReady)
                {
                    _baselineSum += z;
                    _baselineSamples++;
                    if (_baselineSamples >= BaselineFrames)
                    {
                        _baselineDepth = _baselineSum / _baselineSamples;
                        _baselineReady = true;
                    }
                }
                else
                {
                    float diff = z - _baselineDepth;
                    _result.diff = diff;
                    _result.progress01 = Mathf.Clamp01(diff / _cfg.walkDistanceTarget);
                    if (diff > MaxDiff) { MaxDiff = diff; _maxDiffTime = _elapsed; }
                    _aboveTargetFrames = diff >= _cfg.walkDistanceTarget ? _aboveTargetFrames + 1 : 0;
                    // 校準模式不因距離完成，只記錄數值（逾時才結束）
                    if (!_cfg.walkCalibrationMode && _aboveTargetFrames >= FinishConfirmFrames)
                        return Finish(ScoreByTime(_elapsed));
                }

                if (validFrame)
                {
                    _invalidFrames = 0;
                    LastDepth = z;
                    _lastValidDepth = z;
                    _hasLastValidDepth = true;
                }
            }

            if (_elapsed > _cfg.walkTimeout) return Finish(0);
            return _result;
        }

        private int ScoreByTime(float t)
        {
            if (t < _cfg.walkTime4) return 4;
            if (t <= _cfg.walkTime3) return 3;
            if (t <= _cfg.walkTime2) return 2;
            if (t <= _cfg.walkTime1) return 1;
            return 0;
        }

        private MovementResult Finish(int score)
        {
            _finished = true; _running = false; _finalScore = score;
            _result.running = false; _result.justFinished = true; _result.score = score; _result.progress01 = 1f;
            return _result;
        }
    }
}
