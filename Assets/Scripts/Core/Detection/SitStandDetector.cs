using UnityEngine;

namespace SPPB.Core.Detection
{
    /// <summary>
    /// 坐站/深蹲（Type 5）。左右膝角峰谷偵測：maxL/maxR 記站直最大角，兩膝同時掉超過 sitDownAngleDrop→下蹲，
    /// 再同時回到 sitUpAngleReturn 內→完成一次。完成 target 次後依耗時給 0~4；超時 0。
    /// </summary>
    public class SitStandDetector : IMovementDetector
    {
        private readonly SppbThresholds _cfg;
        private bool _running, _finished;
        private float _elapsed, _maxL, _maxR;
        private bool _down;
        private int _reps, _finalScore;
        private MovementResult _result;

        public SitStandDetector(SppbThresholds cfg) { _cfg = cfg; }

        public int ExerciseType => 5;
        public bool IsRunning => _running;
        public MovementResult Result => _result;

        public void Begin() { Reset(); _running = true; }
        public void Reset()
        {
            // 站直角度先假設為典型值（之後實際站直會往上更新）：一開始就坐著也能立刻判定「已蹲下」，第一次站起才算得到
            _running = false; _finished = false; _elapsed = 0f;
            _maxL = _maxR = _cfg != null ? _cfg.sitStandInitialStandAngle : 165f;
            _down = false; _reps = 0; _finalScore = 0; _result = MovementResult.Idle(5);
        }

        public MovementResult Tick(Vector3[] joints, float dt)
        {
            if (!_running || _finished || joints == null || joints.Length < SppbJoints.MinRequiredJoints) return _result;
            _elapsed += dt;

            float angleR = SppbJoints.RightKneeAngle(joints);
            float angleL = SppbJoints.LeftKneeAngle(joints);
            if (angleR > _maxR) _maxR = angleR;
            if (angleL > _maxL) _maxL = angleL;

            if (!_down && angleR < _maxR - _cfg.sitDownAngleDrop && angleL < _maxL - _cfg.sitDownAngleDrop)
                _down = true;
            else if (_down && angleR > _maxR - _cfg.sitUpAngleReturn && angleL > _maxL - _cfg.sitUpAngleReturn)
            {
                _down = false; _reps++;
            }

            _result = new MovementResult
            {
                type = 5, running = true, justFinished = false, score = _finalScore, elapsed = _elapsed,
                repCount = _reps, progress01 = Mathf.Clamp01((float)_reps / Mathf.Max(1, _cfg.sitStandTargetReps)),
                angleL = angleL, angleR = angleR,
            };

            if (_reps >= _cfg.sitStandTargetReps) return Finish(ScoreByTime(_elapsed));
            if (_elapsed > _cfg.sitTimeout) return Finish(0);
            return _result;
        }

        private int ScoreByTime(float t)
        {
            if (t < _cfg.sitTime4) return 4;
            if (t < _cfg.sitTime3) return 3;
            if (t < _cfg.sitTime2) return 2;
            if (t < _cfg.sitTime1) return 1;
            return 0;
        }

        private MovementResult Finish(int score)
        {
            _finished = true; _running = false; _finalScore = score;
            _result.running = false; _result.justFinished = true; _result.score = score; _result.repCount = _reps;
            return _result;
        }
    }
}
