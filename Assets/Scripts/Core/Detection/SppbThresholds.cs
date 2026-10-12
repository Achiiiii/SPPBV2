using UnityEngine;

namespace SPPB.Core.Detection
{
    /// <summary>
    /// SPPB 判定門檻總表（Inspector 可調）。預設值取自原 motion_sdk 文件，但把「連續異常幀數(tmp>25)」
    /// 改為「連續異常秒數」——Android 各機 FPS 不同，幀數門檻會鬆緊不一（這是平衡太容易失敗的主因之一）。
    /// 建檔：Assets → Create → SPPB → Thresholds。
    /// </summary>
    [CreateAssetMenu(fileName = "SppbThresholds", menuName = "SPPB/Thresholds")]
    public class SppbThresholds : ScriptableObject
    {
        [System.Serializable]
        public class BalanceStanceThreshold
        {
            [Tooltip("兩腳尖距離(fdis)上限，超過視為異常")] public float toeWidthMax = 360f;
            [Tooltip("兩腳踝距離(bdis)上限，超過視為異常")] public float ankleWidthMax = 300f;
            [Tooltip("僅 tandem：前後腳深度交錯(dd)下限，低於=沒交錯=異常。<=0 不檢查")] public float tandemDepthMin = 0f;
        }

        // 骨架尺度≈公尺(×1000)。實測(2026-09-17 diag)：VideoPose 雙腳併攏時腳踝距離約 167~204、腳尖 187~245，
        // 所以門檻不能低於此；舊 SDK 的 300/360 又太寬(腳分開仍會過)。
        [Header("平衡 - 併腳站 (Type 1, 0/1)")]
        // 併腳站腳尖距離放寬 310→400：腳跟併攏、腳尖自然外八時 fdis 會到 320（2026-09-30 diag 實測，一開始就被判失敗），
        // 臨床上併腳站不要求腳尖貼齊，真正離開姿勢看腳踝距離(bdis)即可。
        public BalanceStanceThreshold sideBySide = new BalanceStanceThreshold { toeWidthMax = 400f, ankleWidthMax = 260f, tandemDepthMin = 0f };
        [Header("平衡 - 半前後腳站 (Type 2, 0/1)")]
        public BalanceStanceThreshold semiTandem = new BalanceStanceThreshold { toeWidthMax = 300f, ankleWidthMax = 240f, tandemDepthMin = 0f };
        [Header("平衡 - 前後腳站 (Type 3, 0/1/2)")]
        public BalanceStanceThreshold tandem = new BalanceStanceThreshold { toeWidthMax = 230f, ankleWidthMax = 180f, tandemDepthMin = 0.05f };

        [Header("平衡 - 共通")]
        [Tooltip("需維持的秒數，撐滿即滿分")] public float balanceHoldDuration = 10f;
        [Tooltip("連續異常超過此秒數才判定離開姿勢/失敗（取代 tmp>25 幀數；調大=更寬鬆不易失敗）")]
        public float balanceViolationTimeout = 0.8f;
        [Tooltip("Type3：撐超過此秒才垮→得1；未達→0")] public float tandemMinHoldForPartial = 3f;
        [Tooltip("測驗開始後的緩衝秒數：受測者還在調整站姿，此期間異常不累計")]
        public float balanceStartGrace = 1.0f;

        [Header("步行 (Type 4, 0~4)")]
        [Tooltip("前進距離達標門檻（骨盆深度差 ×1000）。VideoPose 深度被壓縮且隨起點遠近變化，須在場地用校準模式實測。" +
                 "實測離鏡頭太近時 diff 約 350 附近就開始丟失追蹤，門檻不可超過追蹤得到的最大值")]
        public float walkDistanceTarget = 380f;
        [Tooltip("步行校準模式：開啟後不會因距離自動完成，Console 持續印出 diff 與追蹤中最大值。" +
                 "從起點走到實際 3m 終點停住，把印出的 diff 填到 walkDistanceTarget 後關閉")]
        public bool walkCalibrationMode = false;
        public float walkTime4 = 3.62f;
        public float walkTime3 = 4.65f;
        public float walkTime2 = 6.52f;
        public float walkTime1 = 15.0f;
        [Tooltip("超過此秒未達標→0 並結束")] public float walkTimeout = 15.0f;
        [Tooltip("終點離機器至少要留的距離(m)。再靠近 VideoPose 會丟失頭手腳。起點建議 ≥ 走行距離+此值")]
        public float walkFinishMinMeters = 1.5f;

        [Header("坐站/深蹲 (Type 5, 0~4)")]
        public int sitStandTargetReps = 5;
        [Tooltip("蹲下：膝角低於(歷史最大-此值)。越小越寬鬆(淺坐也算)。原55, 放寬為40")] public float sitDownAngleDrop = 40f;
        [Tooltip("站起：膝角回到(歷史最大-此值)以上算完成一次。需 > sitDownAngleDrop 才會有一個往返")] public float sitUpAngleReturn = 30f;
        [Tooltip("站直膝角的初始假設值。原本從 0 開始記最大值 → 一開始就坐著時，第一次站起不會被記到。" +
                 "實測站直 160~178°、坐下 74~110°")]
        public float sitStandInitialStandAngle = 165f;
        public float sitTime4 = 11.19f;
        public float sitTime3 = 13.69f;
        public float sitTime2 = 16.69f;
        public float sitTime1 = 59.9f;
        [Tooltip("超過此秒未完成→0 並結束")] public float sitTimeout = 60.0f;

        public BalanceStanceThreshold GetBalance(int type)
        {
            switch (type) { case 1: return sideBySide; case 2: return semiTandem; case 3: return tandem; default: return sideBySide; }
        }
    }
}
