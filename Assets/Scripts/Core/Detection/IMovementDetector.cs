using UnityEngine;

namespace SPPB.Core.Detection
{
    /// <summary>單一動作判定單元：可獨立 Begin/Reset/每幀 Tick，彼此不共用狀態。</summary>
    public interface IMovementDetector
    {
        int ExerciseType { get; }
        bool IsRunning { get; }
        MovementResult Result { get; }
        void Begin();
        void Reset();
        MovementResult Tick(Vector3[] joints, float dt);
    }
}
