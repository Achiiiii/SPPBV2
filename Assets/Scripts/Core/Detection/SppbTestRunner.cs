using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using SPPB.Core;
using SPPB.UI.Pages;

namespace SPPB.Core.Detection
{
    /// <summary>
    /// SPPB 測驗執行器 —— 取代呼叫 native motion_sdk 的 MotionSDKClient，全程純 C# 判定。
    /// 觸發接口與舊版相同（start1_1_single… / end / ResetMotionSDK），方便在場景直接替換元件。
    ///
    /// 分數整合（對齊現有 TestPage）：
    ///  - 平衡：呼叫 _testPage.OnBalanceTestComplete(score, elapsed)，由 TestPage 設 _balanceFailed 與存分數。
    ///  - 步行：呼叫 _testPage.OnWalkTestComplete(score)，由 TestPage 存分數。
    ///  - 坐站：TestPage 沒有存坐站分數（舊版靠 native get_score），故這裡自行 SetSitStandScore；
    ///          次數用 IncrementSitStandCount 驅動（達標會自動 CompleteTest）。
    /// </summary>
    public class SppbTestRunner : MonoBehaviour
    {
        [Header("參考")]
        [SerializeField] private TestPage _testPage;
        [Tooltip("結構化姿態來源（優先）：讀 pose.globalTransforms，免字串解析")]
        public VideoPoseAvatar poseAvatar;
        [Tooltip("字串姿態來源（後備）：poseAvatar 未指定時解析 videoPoseData")]
        public VideoPoseTest poseSource;
        [SerializeField] private SppbThresholds _thresholds;

        [Header("觸發（與舊 SDK 相容）")]
        public bool start1_1_single;
        public bool start1_2_single;
        public bool start1_3_single;
        public bool start2_single;   // 步行
        public bool start3_single;   // 坐站
        public bool end = false;
        [Tooltip("暫停：凍結判定(elapsed 不累積)，用於測驗中人物離開畫面重新定位")]
        public bool paused = false;

        /// <summary>平衡需撐滿秒數（單一來源，TestPage 計分器/動畫都用這個）。</summary>
        public float BalanceHoldDuration => _thresholds != null ? _thresholds.balanceHoldDuration : 10f;

        /// <summary>步行進行中的距離進度 0~1；非步行時為 0。TestPage 用來判斷「已開始走」後不觸發重新定位。</summary>
        public float WalkProgress01 => _walk != null && ReferenceEquals(_active, _walk) ? _walk.Result.progress01 : 0f;
        private bool _walkStartLogged;

        private BalanceDetector _balance1, _balance2, _balance3;
        private WalkDetector _walk;
        private SitStandDetector _sitStand;
        private IMovementDetector _active;
        private bool _prevPoseCorrect;
        private int _prevRepCount;
        private Vector3[] _jointBuffer;
        private float _walkLogTimer;   // 步行距離校準用：節流印出 diff

        [Header("診斷")]
        [Tooltip("每個測驗把判定數值與 keyPoints 寫入 CSV（Editor: 專案根目錄；裝置: persistentDataPath），用來調門檻/找距離訊號。正式上線可關閉")]
        public bool writeDiagnostics = true;
        private StreamWriter _diag;
        private float _diagTime, _diagTimer;

        void Awake()
        {
            if (_thresholds == null)
            {
                Debug.LogError("[SppbTestRunner] 未指定 SppbThresholds，改用預設值。請在 Inspector 指定 config 以便調門檻。");
                _thresholds = ScriptableObject.CreateInstance<SppbThresholds>();
            }
            _balance1 = new BalanceDetector(1, _thresholds);
            _balance2 = new BalanceDetector(2, _thresholds);
            _balance3 = new BalanceDetector(3, _thresholds);
            _walk = new WalkDetector(_thresholds);
            _sitStand = new SitStandDetector(_thresholds);
        }

        void Update()
        {
            if (_active == null)
            {
                if (start1_1_single) BeginExercise(_balance1);
                else if (start1_2_single) BeginExercise(_balance2);
                else if (start1_3_single) BeginExercise(_balance3);
                else if (start2_single) BeginExercise(_walk);
                else if (start3_single) BeginExercise(_sitStand);
            }
            ClearTriggers();

            if (end)
            {
                end = false;
                StopActive();
            }

            if (_active == null) return;
            if (paused) return;   // 重新定位中：凍結判定，elapsed 不累積

            bool hasJoints = TryGetJoints(out Vector3[] joints);
            bool isWalk = ReferenceEquals(_active, _walk);
            // 步行用真實時間：沒偵測到骨架的幀也要推進計時（其他測驗沒骨架就不判定）
            if (!hasJoints && !isWalk) return;

            // 步行：追丟時 VideoPose/Avatar 會留著最後一幀骨架 → 視為沒骨架，讓步行偵測知道追丟了
            if (hasJoints && isWalk && poseSource != null
                && (poseSource.timeSinceLastPose > 0.3f || poseSource.lastMeanConfidence < 0.2f))
                hasJoints = false;
            MovementResult r = _active.Tick(hasJoints ? joints : null, Time.deltaTime);
            WriteDiag(hasJoints, r, Time.deltaTime);
            RouteResult(r);
            if (r.justFinished) OnExerciseFinished(r);
        }

        void OnDestroy() => CloseDiag();

        // ---------- 診斷 CSV（所有測驗）----------

        private const int DiagKeyPointColumns = 27;

        private void OpenDiag(int type)
        {
            if (!writeDiagnostics) return;
            CloseDiag();
            try
            {
                string dir = Application.isEditor
                    ? Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
                    : Application.persistentDataPath;
                string name = type == 4 ? "walk" : type == 5 ? "sitstand" : $"balance{type}";
                string path = Path.Combine(dir, $"diag_{name}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                _diag = new StreamWriter(path, false);

                var header = new System.Text.StringBuilder(
                    "t,type,hasPose,poseCorrect,diff,bdis,fdis,dd,angleL,angleR,hipsZ,boxW,boxH,meanConf,imgW,imgH");
                for (int i = 0; i < DiagKeyPointColumns; i++) header.Append($",kp{i}x,kp{i}y,kp{i}c");
                _diag.WriteLine(header.ToString());

                _diagTime = 0f;
                _diagTimer = 0f;
                Debug.Log($"[診斷] 寫入 {path}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[診斷] 無法建立 CSV: {e.Message}");
                _diag = null;
            }
        }

        private void CloseDiag()
        {
            if (_diag == null) return;
            _diag.Flush();
            _diag.Close();
            _diag = null;
        }

        /// <summary>每 0.1s 記一列：判定數值（腳距/深度/膝角）、外框與信心值、全部 keyPoints（像素座標+信心值）。</summary>
        private void WriteDiag(bool hasPose, MovementResult r, float dt)
        {
            if (_diag == null) return;
            _diagTime += dt;
            _diagTimer += dt;
            if (_diagTimer < 0.1f) return;
            _diagTimer = 0f;

            var inv = CultureInfo.InvariantCulture;
            var sb = new System.Text.StringBuilder();
            var p = poseAvatar != null ? poseAvatar.pose : null;

            float hipsZ = 0f;
            if (p != null && p.globalTransforms != null && p.globalTransforms.Length > 0)
                hipsZ = p.globalTransforms[0].m23;

            float boxW = poseSource != null ? poseSource.lastBoxWidth : 0f;
            float boxH = poseSource != null ? poseSource.lastBoxHeight : 0f;
            float conf = poseSource != null ? poseSource.lastMeanConfidence : 0f;
            int imgW = poseSource != null ? poseSource.imageWidth : 0;
            int imgH = poseSource != null ? poseSource.imageHeight : 0;

            sb.Append(_diagTime.ToString("F2", inv)).Append(',')
              .Append(r.type).Append(',')
              .Append(hasPose ? 1 : 0).Append(',')
              .Append(r.poseCorrect ? 1 : 0).Append(',')
              .Append(r.diff.ToString("F1", inv)).Append(',')
              .Append(r.bdis.ToString("F1", inv)).Append(',')
              .Append(r.fdis.ToString("F1", inv)).Append(',')
              .Append(r.dd.ToString("F4", inv)).Append(',')
              .Append(r.angleL.ToString("F1", inv)).Append(',')
              .Append(r.angleR.ToString("F1", inv)).Append(',')
              .Append(hipsZ.ToString("F4", inv)).Append(',')
              .Append(boxW.ToString("F1", inv)).Append(',')
              .Append(boxH.ToString("F1", inv)).Append(',')
              .Append(conf.ToString("F3", inv)).Append(',')
              .Append(imgW).Append(',').Append(imgH);

            var kps = p != null ? p.keyPoints : null;
            for (int i = 0; i < DiagKeyPointColumns; i++)
            {
                if (kps != null && i < kps.Length)
                    sb.Append(',').Append(kps[i].x.ToString("F1", inv))
                      .Append(',').Append(kps[i].y.ToString("F1", inv))
                      .Append(',').Append(kps[i].z.ToString("F2", inv));
                else
                    sb.Append(",,,");
            }
            _diag.WriteLine(sb.ToString());
        }

        private void BeginExercise(IMovementDetector det)
        {
            _active = det;
            _prevPoseCorrect = false;
            _prevRepCount = 0;
            _walkStartLogged = false;
            _walkLogTimer = 0f;
            det.Begin();
            OpenDiag(det.ExerciseType);
            Debug.Log($"[SppbTestRunner] ▶️ 測驗 {det.ExerciseType} 開始");
        }

        private void StopActive()
        {
            CloseDiag();
            if (_active != null) { _active.Reset(); _active = null; }
        }

        public void StartExercise(int type)
        {
            switch (type)
            {
                case 1: BeginExercise(_balance1); break;
                case 2: BeginExercise(_balance2); break;
                case 3: BeginExercise(_balance3); break;
                case 4: BeginExercise(_walk); break;
                case 5: BeginExercise(_sitStand); break;
                default: Debug.LogError($"[SppbTestRunner] 無效 type {type}"); break;
            }
        }

        /// <summary>相容舊 API：重置所有偵測單元。</summary>
        public void ResetMotionSDK()
        {
            _balance1.Reset(); _balance2.Reset(); _balance3.Reset();
            _walk.Reset(); _sitStand.Reset();
            _active = null;
        }

        private void ClearTriggers()
        {
            start1_1_single = start1_2_single = start1_3_single = start2_single = start3_single = false;
        }

        private void RouteResult(MovementResult r)
        {
            if (_testPage == null) return;
            switch (r.type)
            {
                case 1:
                case 2:
                case 3:
                    if (r.poseCorrect && !_prevPoseCorrect) _testPage.OnActionFeedback(true, r.score);
                    else if (!r.poseCorrect && _prevPoseCorrect) _testPage.HideCorrectHint();
                    _prevPoseCorrect = r.poseCorrect;
                    break;
                case 4:
                    // UpdateWalkProgress 內部 /WALK_MAX_DISTANCE(300) 填進度條、/100 換公尺(上限3m)。
                    // VideoPose 深度被壓縮，改用進度比例換算：到達門檻 = 300 = 滿格、顯示 3.00m
                    _testPage.UpdateWalkProgress(r.progress01 * 300f);
                    // 起點取得時印一次（VideoPose 深度不是真實公尺，無法直接推估離機器距離）
                    if (!_walkStartLogged && _walk.BaselineReady)
                    {
                        _walkStartLogged = true;
                        Debug.Log($"[Walk] 起點深度={_walk.BaselineDepth:F0}，目標 diff={_thresholds.walkDistanceTarget:F0}");
                    }

                    // 校準用：每 0.5s 印出目前 diff、追蹤中最大 diff、信心值
                    _walkLogTimer += Time.deltaTime;
                    if (_walkLogTimer >= 0.5f)
                    {
                        _walkLogTimer = 0f;
                        string mode = _thresholds.walkCalibrationMode ? "【校準模式：不會自動完成】" : "";
                        float conf = poseSource != null ? poseSource.lastMeanConfidence : 0f;
                        Debug.Log($"[Walk][校準]{mode} diff={r.diff:F0}  最大有效diff={_walk.MaxDiff:F0}  信心值={conf:F2}  目標={_thresholds.walkDistanceTarget:F0}");
                    }
                    break;
                case 5:
                    if (r.repCount != _prevRepCount)
                    {
                        _testPage.IncrementSitStandCount(r.repCount);
                        _prevRepCount = r.repCount;
                    }
                    break;
            }
        }

        private void OnExerciseFinished(MovementResult r)
        {
            Debug.Log($"[SppbTestRunner] ✅ 測驗 {r.type} 結束 — score={r.score}, elapsed={r.elapsed:F2}");

            if (r.type == 5 && ScoreManager.Instance != null)
                ScoreManager.Instance.SetSitStandScore(r.score); // TestPage 不存坐站分數，這裡補上

            if (_testPage != null)
            {
                if (r.type >= 1 && r.type <= 3)
                    _testPage.OnBalanceTestComplete(r.score, r.elapsed); // TestPage 設 _balanceFailed + 存平衡分數
                else if (r.type == 4)
                    _testPage.OnWalkTestComplete(r.score);               // TestPage 存步行分數
                // type 5：次數達標時 IncrementSitStandCount 已觸發 CompleteTest
            }

            _active = null;
        }

        private bool TryGetJoints(out Vector3[] joints)
        {
            joints = null;
            if (poseAvatar != null && poseAvatar.pose != null)
            {
                if (SppbJoints.TryExtract(poseAvatar.pose.globalTransforms, ref _jointBuffer)) { joints = _jointBuffer; return true; }
                return false;
            }
            if (poseSource != null && !string.IsNullOrEmpty(poseSource.videoPoseData))
                return TryParseString(poseSource.videoPoseData, out joints);
            return false;
        }

        private bool TryParseString(string raw, out Vector3[] joints)
        {
            joints = null;
            try
            {
                raw = raw.Replace(" ", "").Trim('[', ']');
                string[] parts = raw.Split(new[] { "),(" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < SppbJoints.MinRequiredJoints) return false;
                if (_jointBuffer == null || _jointBuffer.Length < parts.Length) _jointBuffer = new Vector3[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                {
                    string[] xyz = parts[i].Replace("(", "").Replace(")", "").Split(',');
                    float x = xyz.Length > 0 && float.TryParse(xyz[0], out var vx) ? vx : 0f;
                    float y = xyz.Length > 1 && float.TryParse(xyz[1], out var vy) ? vy : 0f;
                    float z = xyz.Length > 2 && float.TryParse(xyz[2], out var vz) ? vz : 0f;
                    _jointBuffer[i] = new Vector3(x, y, z);
                }
                joints = _jointBuffer;
                return true;
            }
            catch (Exception e) { Debug.LogWarning($"[SppbTestRunner] 姿態字串解析失敗: {e.Message}"); return false; }
        }
    }
}
