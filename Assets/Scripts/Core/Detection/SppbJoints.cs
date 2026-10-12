using UnityEngine;

namespace SPPB.Core.Detection
{
    /// <summary>
    /// 關節索引常數 + 幾何工具。索引對應 VideoPoseAvatar.boneId（globalTransforms 前 22 個為身體關節）。
    /// 原本包在 native motion_sdk 的 bdis/fdis/dd/角度 計算，改用具名函式在 C# 重算。
    /// </summary>
    public static class SppbJoints
    {
        public const int Hips = 0;
        public const int RightUpperLeg = 1;
        public const int RightLowerLeg = 2;
        public const int RightFoot = 3;   // 右腳踝
        public const int LeftUpperLeg = 4;
        public const int LeftLowerLeg = 5;
        public const int LeftFoot = 6;    // 左腳踝
        public const int RightToes = 20;  // 右腳尖
        public const int LeftToes = 21;   // 左腳尖

        public const int MinRequiredJoints = 22;
        public const float DistanceScale = 1000f; // 沿用 SDK：距離值 ×1000

        /// <summary>從 globalTransforms（世界矩陣）取關節世界座標，buffer 重用避免每幀 GC。</summary>
        public static bool TryExtract(Matrix4x4[] globalTransforms, ref Vector3[] buffer)
        {
            if (globalTransforms == null || globalTransforms.Length < MinRequiredJoints) return false;
            if (buffer == null || buffer.Length < globalTransforms.Length)
                buffer = new Vector3[globalTransforms.Length];
            for (int i = 0; i < globalTransforms.Length; i++)
            {
                Matrix4x4 m = globalTransforms[i];
                buffer[i] = new Vector3(m.m03, m.m13, m.m23);
            }
            return true;
        }

        public static float PlaneDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dy = a.y - b.y;
            return Mathf.Sqrt(dx * dx + dy * dy) * DistanceScale;
        }

        public static float Depth(Vector3 hips) => hips.z * DistanceScale;

        /// <summary>tandem 前後腳深度不對稱 dd = |d1-d2|。太小=沒交錯。</summary>
        public static float TandemDepthDiff(Vector3[] j)
        {
            float d1 = Mathf.Abs(j[LeftToes].z - j[RightFoot].z);
            float d2 = Mathf.Abs(j[RightToes].z - j[LeftFoot].z);
            return Mathf.Abs(d1 - d2);
        }

        public static float JointAngle(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ba = a - b, bc = c - b;
            float denom = ba.magnitude * bc.magnitude;
            if (denom < 1e-6f) return 0f;
            float cos = Mathf.Clamp(Vector3.Dot(ba, bc) / denom, -1f, 1f);
            return Mathf.Acos(cos) * Mathf.Rad2Deg;
        }

        public static float RightKneeAngle(Vector3[] j) => JointAngle(j[LeftUpperLeg], j[LeftLowerLeg], j[LeftFoot]);
        public static float LeftKneeAngle(Vector3[] j) => JointAngle(j[RightUpperLeg], j[RightLowerLeg], j[RightFoot]);
        public static float AnkleWidth(Vector3[] j) => PlaneDistance(j[RightFoot], j[LeftFoot]);
        public static float ToeWidth(Vector3[] j) => PlaneDistance(j[RightToes], j[LeftToes]);
    }
}
