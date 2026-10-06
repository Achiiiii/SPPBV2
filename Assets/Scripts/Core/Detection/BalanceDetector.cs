using UnityEngine;

namespace SPPB.Core.Detection
{
    /// <summary>
    /// 平衡測驗（併腳=1 / 半前後腳=2 / 前後腳=3）。連續異常超過 violationTimeout 秒才判定離開姿勢。
    /// Type1/2：撐滿 holdDuration→1，中途垮→0。Type3：撐滿→2，tandemMinHold 秒後才垮→1，太早垮→0。
    /// </summary>
    public class BalanceDetector : IMovementDetector
    {
        private readonly int _type;
        private readonly SppbThresholds _cfg;
        private bool _running, _finished;
        private float _elapsed, _violationTime;
        private int _finalScore;
        private MovementResult _result;

        public BalanceDetector(int type, SppbThresholds cfg) { _type = Mathf.Clamp(type, 1, 3); _cfg = cfg; }

        public int ExerciseType => _type;
        public bool IsRunning => _running;
        public MovementResult Result => _result;

        public void Begin() { Reset(); _running = true; }
        public void Reset()
        {
            _running = false; _elapsed = 0f; _violationTime = 0f; _finished = false; _finalScore = 0;
            _result = MovementResult.Idle(_type);
        }

        public MovementResult Tick(Vector3[] joints, float dt)
        {
            if (!_running || _finished || joints == null || joints.Length < SppbJoints.MinRequiredJoints) return _result;
            _elapsed += dt;

            var th = _cfg.GetBalance(_type);
            float fdis = SppbJoints.ToeWidth(joints);
            float bdis = SppbJoints.AnkleWidth(joints);
            float dd = SppbJoints.TandemDepthDiff(joints);

            bool abnormal = fdis > th.toeWidthMax || bdis > th.ankleWidthMax
                         || (th.tandemDepthMin > 0f && dd < th.tandemDepthMin);

            // 起始緩衝期間不累計異常（受測者剛聽到開始，還在調整站姿）
            bool inGrace = _elapsed < _cfg.balanceStartGrace;
            if (abnormal && !inGrace) _violationTime += dt; else _violationTime = 0f;
            bool poseCorrect = !abnormal;

            _result = new MovementResult
            {
                type = _type, running = true, justFinished = false, score = _finalScore, elapsed = _elapsed,
                poseCorrect = poseCorrect, progress01 = Mathf.Clamp01(_elapsed / _cfg.balanceHoldDuration),
                bdis = bdis, fdis = fdis, dd = dd,
            };

            if (_violationTime >= _cfg.balanceViolationTimeout)
            {
                int score = _type == 3 ? (_elapsed >= _cfg.tandemMinHoldForPartial ? 1 : 0) : 0;
                return Finish(score);
            }
            if (_elapsed >= _cfg.balanceHoldDuration)
                return Finish(_type == 3 ? 2 : 1);

            return _result;
        }

        private MovementResult Finish(int score)
        {
            _finished = true; _running = false; _finalScore = score;
            _result.running = false; _result.justFinished = true; _result.score = score;
            return _result;
        }
    }
}
