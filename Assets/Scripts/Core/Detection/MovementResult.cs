namespace SPPB.Core.Detection
{
    /// <summary>單一測驗每幀的判定結果（取代 native SDK 回傳 JSON，且欄名不會再對不上）。</summary>
    public struct MovementResult
    {
        public int type;             // 1~5
        public bool running;
        public bool justFinished;    // 本幀剛結束
        public int score;
        public float elapsed;

        public bool poseCorrect;     // 平衡：本幀姿勢是否正確
        public int repCount;         // 坐站已完成次數
        public float progress01;     // 0~1

        // 診斷用
        public float bdis, fdis, dd, diff, angleL, angleR;

        public static MovementResult Idle(int type) => new MovementResult { type = type, running = false };
    }
}
