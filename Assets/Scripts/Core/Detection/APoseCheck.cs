using UnityEngine;

namespace SPPB.Core.Detection
{
    /// <summary>
    /// A-pose 判斷（定位校準用）。以 3D 骨架角度判斷，不受離鏡頭遠近影響：
    ///  - 上臂相對「身體往下方向(頸→骨盆)」張開 armMinDeg~armMaxDeg（A-pose 約 20~70°；手垂下≈0°、T-pose≈90°）
    ///  - 兩手肘打直、兩膝打直
    /// 關節索引見 VideoPoseAvatar.boneId：10=Neck, 13/14/15=左上臂/前臂/手, 17/18/19=右上臂/前臂/手。
    /// </summary>
    public static class APoseCheck
    {
        public const int Neck = 10;
        public const int LeftUpperArm = 13, LeftLowerArm = 14, LeftHand = 15;
        public const int RightUpperArm = 17, RightLowerArm = 18, RightHand = 19;

        public static bool IsAPose(Vector3[] j, float armMinDeg, float armMaxDeg, float limbStraightMinDeg, out string reason)
            => IsAPose(j, armMinDeg, armMaxDeg, limbStraightMinDeg, out reason, out _);

        /// <param name="reason">除錯用（含角度）</param>
        /// <param name="hint">給受測者的口語提示（沒通過時才有值）</param>
        public static bool IsAPose(Vector3[] j, float armMinDeg, float armMaxDeg, float limbStraightMinDeg, out string reason, out string hint)
        {
            reason = ""; hint = "";
            if (j == null || j.Length < SppbJoints.MinRequiredJoints) { reason = "無骨架"; return false; }

            Vector3 down = j[SppbJoints.Hips] - j[Neck];
            if (down.sqrMagnitude < 1e-6f) { reason = "身體方向無效"; return false; }

            float leftArm = Vector3.Angle(j[LeftLowerArm] - j[LeftUpperArm], down);
            float rightArm = Vector3.Angle(j[RightLowerArm] - j[RightUpperArm], down);
            if (leftArm < armMinDeg || leftArm > armMaxDeg || rightArm < armMinDeg || rightArm > armMaxDeg)
            {
                reason = $"手臂張開角度 左{leftArm:F0}° 右{rightArm:F0}°（需 {armMinDeg:F0}~{armMaxDeg:F0}°）";
                hint = (leftArm > armMaxDeg || rightArm > armMaxDeg) && leftArm >= armMinDeg && rightArm >= armMinDeg
                    ? "手臂請放低一點，斜斜向下張開就好"
                    : "請把雙手往兩側斜下方張開，像 A 字形";
                return false;
            }

            float leftElbow = SppbJoints.JointAngle(j[LeftUpperArm], j[LeftLowerArm], j[LeftHand]);
            float rightElbow = SppbJoints.JointAngle(j[RightUpperArm], j[RightLowerArm], j[RightHand]);
            if (leftElbow < limbStraightMinDeg || rightElbow < limbStraightMinDeg)
            {
                reason = $"手肘未打直 左{leftElbow:F0}° 右{rightElbow:F0}°";
                hint = "請把手肘伸直";
                return false;
            }

            float leftKnee = SppbJoints.LeftKneeAngle(j);
            float rightKnee = SppbJoints.RightKneeAngle(j);
            if (leftKnee < limbStraightMinDeg || rightKnee < limbStraightMinDeg)
            {
                reason = $"膝蓋未打直 左{leftKnee:F0}° 右{rightKnee:F0}°";
                hint = "請站直，膝蓋伸直";
                return false;
            }

            return true;
        }
    }
}
