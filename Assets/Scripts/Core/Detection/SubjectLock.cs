using System;
using UnityEngine;

namespace SPPB.Core.Detection
{
    /// <summary>
    /// 鎖定受測者（多人場域用）。由 VideoPoseTest 在主執行緒每幀呼叫 ProcessFrame（在 PushFrame 之前）。
    ///  1. 身分特徵：校準成功時記下上衣(肩→髖下半段)與褲子(髖→膝)的平均顏色，之後每個新姿勢比對。
    ///     VideoPose 一次只輸出一人、不會記得是誰；旁人被抓到時顏色對不上 → OtherPerson。
    ///  2. 區域遮罩：鎖定後只把受測者周圍一圈影像交給 VideoPose，其他區域塗黑，旁人不會被看到。
    ///  3. 對焦跟隨：自動對焦點跟著受測者（鏡頭需支援指定對焦點）。
    /// keyPoints 為 OpenPose BODY_25 排列、影像像素座標（y 向下，等於影像緩衝區的列索引），z=信心值。
    /// </summary>
    public class SubjectLock : MonoBehaviour
    {
        public enum State { Unlocked, Capturing, Tracking, OtherPerson, Lost }

        // BODY_25 關鍵點索引
        private const int KpRShoulder = 2, KpLShoulder = 5, KpRHip = 9, KpLHip = 12, KpRKnee = 10, KpLKnee = 13;

        [Header("身分比對")]
        [Tooltip("校準時取樣幀數（平均後當作受測者特徵）")]
        public int captureFrames = 8;
        [Tooltip("顏色差異門檻：差異分數 > 1 視為不同人。此值是色度(0~1)差異的尺度，調大=較寬鬆")]
        public float chromaTolerance = 0.07f;
        [Tooltip("亮度差異容許倍數（光線變化）。調大=較寬鬆")]
        public float brightnessTolerance = 1.8f;
        [Tooltip("連續不符多久才判定為其他人")]
        public float mismatchConfirmTime = 0.3f;
        [Tooltip("連續相符多久才判定為受測者")]
        public float matchConfirmTime = 0.3f;
        [Tooltip("骨架中心單次跳動超過畫面寬的此比例，視為可能換人")]
        public float jumpRatio = 0.3f;

        [Header("有效人物")]
        [Tooltip("外框高度至少為校準時的此比例。不能太高：校準時站得近、之後退後或頭部抬頭只拍到上半身，身形會變小")]
        public float minSizeRatio = 0.3f;
        [Tooltip("外框高度至少為畫面高度的此比例（排除 VideoPose 抓到的小型假骨架，實測約 100px/480px）")]
        public float minImageHeightRatio = 0.25f;
        [Tooltip("keyPoints 平均信心值下限")]
        public float minConfidence = 0.4f;
        [Tooltip("多久沒有有效骨架判定為離開")]
        public float lostTime = 0.3f;

        [Header("區域遮罩")]
        public bool maskEnabled = true;
        /// <summary>頭部追蹤轉動中暫停遮罩（由 SppbHeadTracker 設定）：轉頭時人在畫面位移，遮罩跟不上會遮掉頭腳。</summary>
        [System.NonSerialized] public bool maskSuspended;
        [Tooltip("遮罩範圍：左右各多留外框寬的比例")]
        public float marginXRatio = 0.45f;
        [Tooltip("遮罩範圍：上下各多留外框高的比例")]
        public float marginYRatio = 0.25f;
        [Tooltip("遮罩範圍最小寬度（佔畫面寬比例）")]
        public float minRoiWidthRatio = 0.4f;

        [Header("對焦跟隨")]
        public bool focusFollow = true;
        [Tooltip("對焦點更新間隔(秒)")]
        public float focusInterval = 1.0f;
        [Tooltip("中心移動超過畫面此比例才重新對焦")]
        public float focusMoveRatio = 0.05f;

        [Header("除錯")]
        public bool logEnabled = true;

        // ---- 對外狀態 ----
        public State CurrentState { get; private set; } = State.Unlocked;
        public bool IsLocked => CurrentState != State.Unlocked && CurrentState != State.Capturing;
        /// <summary>步行、坐站時關閉身分比對（走近鏡頭只剩局部身體／蹲下時顏色取樣不可靠）。</summary>
        public bool IdentityCheckEnabled { get; set; } = true;
        /// <summary>最近一次偵測到的人（影像像素座標，y 向下）。</summary>
        public Rect LastBox { get; private set; }
        public bool HasBox { get; private set; }
        public int ImageWidth { get; private set; }
        public int ImageHeight { get; private set; }
        public float LastDistance { get; private set; }
        /// <summary>最近一幀 keyPoints（主執行緒副本，BODY_25 像素座標，z=信心值）。勿修改內容。</summary>
        public Vector3[] Keypoints => _kp;
        /// <summary>距離上次收到新姿勢的秒數。</summary>
        public float PoseAge => Time.unscaledTime - _lastPoseTime;
        private float _lastPoseTime = -999f;
        public float RefBoxHeight { get; private set; }

        // ---- 內部 ----
        private VideoPoseTest _vpt;
        private readonly object _kpLock = new object();
        private Vector3[] _kpShared;
        private int _kpVersion;          // native thread 遞增
        private int _kpSeenVersion;
        private Vector3[] _kp = new Vector3[0];

        private Vector3 _refTorso, _refLegs;   // x,y = 色度 (r,g)，z = 亮度
        private bool _refHasLegs;
        private Vector3 _accTorso, _accLegs;
        private int _accTorsoN, _accLegsN;

        private float _mismatchTimer, _matchTimer, _lostTimer;
        private float _lastEvalTime;
        private Vector2 _lastCenter;
        private bool _hasLastCenter;
        private RectInt _roi;
        private bool _roiValid;
        private float _focusTimer;
        private Vector2 _lastFocusCenter = new Vector2(-1, -1);
        private float _logTimer;
        private float _lastProcessTime;
        // 最近一次比對的細項（除錯用）
        private Vector3 _lastTorso, _lastLegs;
        private bool _lastHasTorso, _lastHasLegs;
        private float _lastTorsoD, _lastLegsD;

        public void Init(VideoPoseTest vpt) => _vpt = vpt;

        /// <summary>VideoPose 回傳姿勢時呼叫（native thread）。</summary>
        public void OnPoseKeypoints(Vector3[] kps)
        {
            if (kps == null) return;
            lock (_kpLock)
            {
                if (_kpShared == null || _kpShared.Length != kps.Length) _kpShared = new Vector3[kps.Length];
                Array.Copy(kps, _kpShared, kps.Length);
                _kpVersion++;
            }
        }

        /// <summary>清除鎖定（進校準關、回首頁時）。</summary>
        public void ResetLock()
        {
            CurrentState = State.Unlocked;
            IdentityCheckEnabled = true;
            _accTorso = _accLegs = Vector3.zero;
            _accTorsoN = _accLegsN = 0;
            _mismatchTimer = _matchTimer = _lostTimer = 0f;
            _hasLastCenter = false;
            _roiValid = false;
            HasBox = false;
            RefBoxHeight = 0f;
            _lastFocusCenter = new Vector2(-1, -1);
        }

        /// <summary>校準成功時呼叫：開始取樣受測者特徵。</summary>
        public void BeginLock(float refBoxHeight)
        {
            ResetLock();
            RefBoxHeight = refBoxHeight;
            CurrentState = State.Capturing;
            Log($"開始記錄受測者特徵（身形高度基準 {refBoxHeight:F0}px）");
        }

        /// <summary>主執行緒每幀、PushFrame 前呼叫：更新狀態、遮罩影像、更新對焦。</summary>
        public void ProcessFrame(Color32[] pixels, int w, int h)
        {
            if (_vpt == null || pixels == null || w <= 0 || h <= 0) return;
            _pixels = pixels; _pw = w; _ph = h;
            ImageWidth = w;
            ImageHeight = h;
            float now = Time.unscaledTime;
            // 本函式只在有新相機畫面時呼叫（約每秒 8 次），不是每個 Unity 幀，所以要用兩次呼叫的實際間隔
            float dt = _lastProcessTime > 0f ? Mathf.Clamp(now - _lastProcessTime, 0f, 0.5f) : 0f;
            _lastProcessTime = now;

            bool newPose = false;
            lock (_kpLock)
            {
                if (_kpShared != null && _kpVersion != _kpSeenVersion)
                {
                    _kpSeenVersion = _kpVersion;
                    if (_kp.Length != _kpShared.Length) _kp = new Vector3[_kpShared.Length];
                    Array.Copy(_kpShared, _kp, _kpShared.Length);
                    newPose = true;
                    _lastPoseTime = now;
                }
            }

            // 最小身形：固定下限（排除假骨架）與校準身形比例取大者；比例放寬避免退後/頭部轉動時被誤判為離開
            float minH = Mathf.Max(h * minImageHeightRatio, RefBoxHeight > 0f ? RefBoxHeight * minSizeRatio : 0f);
            bool valid = _vpt.IsValidPerson(0.4f, minH, minConfidence);

            if (newPose && valid && TryGetBox(out Rect box))
            {
                LastBox = box;
                HasBox = true;
            }
            else if (!valid)
            {
                HasBox = false;
            }

            switch (CurrentState)
            {
                case State.Unlocked:
                    // 尚未鎖定（校準中）：只做對焦跟隨
                    if (valid && HasBox) UpdateFocus(dt);
                    break;

                case State.Capturing:
                    if (newPose && valid) Capture();
                    break;

                default:
                    UpdateLocked(newPose, valid, now, dt);
                    break;
            }

            if (maskEnabled && !maskSuspended && _roiValid && IsLocked && CurrentState != State.Lost)
                ApplyMask(pixels, w, h, _roi);

            if (logEnabled && IsLocked)
            {
                _logTimer += dt;
                if (_logTimer >= 1f)
                {
                    _logTimer = 0f;
                    string torso = _lastHasTorso
                        ? $"上衣 色({_lastTorso.x:F2},{_lastTorso.y:F2}) 亮{_lastTorso.z:F2} vs 基準({_refTorso.x:F2},{_refTorso.y:F2}) 亮{_refTorso.z:F2} 差{_lastTorsoD:F2}"
                        : "上衣 未取得";
                    string legs = !_refHasLegs ? "褲子 無基準"
                        : _lastHasLegs ? $"褲子 色({_lastLegs.x:F2},{_lastLegs.y:F2}) 亮{_lastLegs.z:F2} vs 基準({_refLegs.x:F2},{_refLegs.y:F2}) 亮{_refLegs.z:F2} 差{_lastLegsD:F2}"
                        : "褲子 未取得";
                    Log($"state={CurrentState} 差異={LastDistance:F2} | {torso} | {legs} | 身分比對={(IdentityCheckEnabled ? "開" : "關(步行/坐站)")} 遮罩={(_roiValid && CurrentState != State.Lost ? _roi.ToString() : "無")}");
                }
            }
        }

        // ---------------- 鎖定後狀態機 ----------------

        private void UpdateLocked(bool newPose, bool valid, float now, float dt)
        {
            if (!valid)
            {
                _lostTimer += dt;
                _matchTimer = 0f;
                if (_lostTimer >= lostTime && CurrentState != State.Lost)
                {
                    SetState(State.Lost);
                    _hasLastCenter = false;
                }
                return;
            }
            _lostTimer = 0f;
            if (!newPose || !HasBox) return;

            float evalDt = _lastEvalTime > 0f ? Mathf.Clamp(now - _lastEvalTime, 0f, 0.5f) : 0f;
            _lastEvalTime = now;

            Vector2 center = LastBox.center;
            bool jumped = _hasLastCenter && CurrentState == State.Tracking
                          && Vector2.Distance(center, _lastCenter) > jumpRatio * ImageWidth;

            bool match;
            if (!IdentityCheckEnabled)
            {
                match = true;
                LastDistance = 0f;
            }
            else
            {
                float d = ComputeDistance(out bool sampled);
                if (!sampled) return;   // 關鍵點不足，無法判斷，維持原狀態
                LastDistance = d;
                match = d <= 1f && !jumped;
            }

            if (match)
            {
                _matchTimer += evalDt;
                _mismatchTimer = 0f;
                bool confirm = CurrentState == State.Tracking || _matchTimer >= matchConfirmTime || !IdentityCheckEnabled;
                if (confirm && CurrentState != State.Tracking) SetState(State.Tracking);
            }
            else
            {
                _mismatchTimer += evalDt;
                _matchTimer = 0f;
                // 從離開狀態回來時，一出現就不符 → 直接判定為其他人
                bool confirm = _mismatchTimer >= mismatchConfirmTime || CurrentState == State.Lost;
                if (confirm && CurrentState != State.OtherPerson) SetState(State.OtherPerson);
            }

            if (CurrentState == State.Tracking)
            {
                _lastCenter = center;
                _hasLastCenter = true;
                UpdateRoi(LastBox);
                UpdateFocus(dt);
            }
        }

        private void SetState(State s)
        {
            if (CurrentState == s) return;
            Log($"{CurrentState} → {s}（差異 {LastDistance:F2}）");
            CurrentState = s;
            if (s == State.Lost) _roiValid = false;   // 離開：取消遮罩，讓受測者回到畫面任何位置都找得到
            _mismatchTimer = _matchTimer = 0f;
        }

        // ---------------- 身分特徵 ----------------

        private void Capture()
        {
            if (!SamplePatches(out Vector3 torso, out bool hasTorso, out Vector3 legs, out bool hasLegs)) return;
            if (hasTorso) { _accTorso += torso; _accTorsoN++; }
            if (hasLegs) { _accLegs += legs; _accLegsN++; }

            if (_accTorsoN >= captureFrames)
            {
                _refTorso = _accTorso / _accTorsoN;
                _refHasLegs = _accLegsN >= captureFrames / 2;
                if (_refHasLegs) _refLegs = _accLegs / _accLegsN;
                if (HasBox) UpdateRoi(LastBox);
                Log($"受測者特徵已記錄：上衣色度({_refTorso.x:F2},{_refTorso.y:F2}) 亮度{_refTorso.z:F2}" +
                    (_refHasLegs ? $"｜褲子色度({_refLegs.x:F2},{_refLegs.y:F2}) 亮度{_refLegs.z:F2}" : "｜褲子未取得"));
                SetState(State.Tracking);
            }
        }

        /// <summary>與受測者特徵的差異分數（>1 視為不同人）。</summary>
        private float ComputeDistance(out bool sampled)
        {
            sampled = SamplePatches(out Vector3 torso, out bool hasTorso, out Vector3 legs, out bool hasLegs) && hasTorso;
            _lastTorso = torso; _lastLegs = legs; _lastHasTorso = hasTorso; _lastHasLegs = hasLegs;
            if (!sampled) return 0f;
            float dTorso = FeatureDistance(torso, _refTorso);
            _lastTorsoD = dTorso;
            _lastLegsD = -1f;
            if (_refHasLegs && hasLegs)
            {
                _lastLegsD = FeatureDistance(legs, _refLegs);
                return dTorso * 0.65f + _lastLegsD * 0.35f;
            }
            return dTorso;
        }

        private float FeatureDistance(Vector3 a, Vector3 b)
        {
            float chroma = Vector2.Distance(new Vector2(a.x, a.y), new Vector2(b.x, b.y)) / Mathf.Max(0.001f, chromaTolerance);
            float bright = Mathf.Abs(Mathf.Log((a.z + 0.02f) / (b.z + 0.02f))) / Mathf.Log(Mathf.Max(1.01f, brightnessTolerance));
            return Mathf.Max(chroma, bright);
        }

        private Color32[] _pixels;
        private int _pw, _ph;

        /// <summary>取樣上衣（肩→髖的下半段，避開抱胸的手）與褲子（髖→膝）平均顏色。</summary>
        private bool SamplePatches(out Vector3 torso, out bool hasTorso, out Vector3 legs, out bool hasLegs)
        {
            torso = legs = Vector3.zero;
            hasTorso = hasLegs = false;
            if (_pixels == null || _kp.Length <= KpLKnee) return false;

            if (Ok(KpRShoulder) && Ok(KpLShoulder) && Ok(KpRHip) && Ok(KpLHip))
            {
                Vector3 sum = Vector3.zero; int n = 0;
                for (int si = 0; si < 3; si++)
                    for (int ti = 0; ti < 3; ti++)
                    {
                        float s = 0.3f + 0.2f * si;       // 0.3 ~ 0.7（左右）
                        float t = 0.5f + 0.15f * ti;      // 0.5 ~ 0.8（肩→髖）
                        Vector2 top = Vector2.Lerp(_kp[KpRShoulder], _kp[KpLShoulder], s);
                        Vector2 bottom = Vector2.Lerp(_kp[KpRHip], _kp[KpLHip], s);
                        if (SampleAt(Vector2.Lerp(top, bottom, t), ref sum)) n++;
                    }
                if (n >= 5) { torso = ToFeature(sum / n); hasTorso = true; }
            }

            Vector3 lsum = Vector3.zero; int ln = 0;
            if (Ok(KpRHip) && Ok(KpRKnee))
                for (int i = 0; i < 2; i++) if (SampleAt(Vector2.Lerp(_kp[KpRHip], _kp[KpRKnee], 0.35f + 0.25f * i), ref lsum)) ln++;
            if (Ok(KpLHip) && Ok(KpLKnee))
                for (int i = 0; i < 2; i++) if (SampleAt(Vector2.Lerp(_kp[KpLHip], _kp[KpLKnee], 0.35f + 0.25f * i), ref lsum)) ln++;
            if (ln >= 2) { legs = ToFeature(lsum / ln); hasLegs = true; }

            return hasTorso || hasLegs;
        }

        private bool Ok(int i) => i < _kp.Length && _kp[i].z >= VideoPoseTest.KeyPointMinConfidence;

        /// <summary>取 3x3 像素平均（RGB 0~1）。</summary>
        private bool SampleAt(Vector2 p, ref Vector3 sum)
        {
            int cx = Mathf.RoundToInt(p.x), cy = Mathf.RoundToInt(p.y);
            if (cx < 1 || cy < 1 || cx >= _pw - 1 || cy >= _ph - 1) return false;
            Vector3 acc = Vector3.zero;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    Color32 c = _pixels[(cy + dy) * _pw + cx + dx];
                    acc += new Vector3(c.r, c.g, c.b);
                }
            sum += acc / (9f * 255f);
            return true;
        }

        /// <summary>RGB → (色度 r, 色度 g, 亮度)。色度對光線強弱較不敏感。</summary>
        private static Vector3 ToFeature(Vector3 rgb)
        {
            float s = rgb.x + rgb.y + rgb.z + 0.05f;
            return new Vector3(rgb.x / s, rgb.y / s, (rgb.x + rgb.y + rgb.z) / 3f);
        }

        // ---------------- 外框 / 遮罩 / 對焦 ----------------

        private bool TryGetBox(out Rect box)
        {
            box = default;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            int used = 0;
            for (int i = 0; i < _kp.Length; i++)
            {
                if (_kp[i].z < VideoPoseTest.KeyPointMinConfidence) continue;
                used++;
                minX = Mathf.Min(minX, _kp[i].x); maxX = Mathf.Max(maxX, _kp[i].x);
                minY = Mathf.Min(minY, _kp[i].y); maxY = Mathf.Max(maxY, _kp[i].y);
            }
            if (used < 4) return false;
            box = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }

        private void UpdateRoi(Rect box)
        {
            int w = ImageWidth, h = ImageHeight;
            float mx = box.width * marginXRatio, my = box.height * marginYRatio;
            float xMin = box.xMin - mx, xMax = box.xMax + mx;
            float minW = w * minRoiWidthRatio;
            if (xMax - xMin < minW)
            {
                float c = (xMin + xMax) * 0.5f;
                xMin = c - minW * 0.5f; xMax = c + minW * 0.5f;
            }
            int x0 = Mathf.Clamp(Mathf.FloorToInt(xMin), 0, w - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(xMax), x0 + 1, w);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(box.yMin - my), 0, h - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(box.yMax + my), y0 + 1, h);
            _roi = new RectInt(x0, y0, x1 - x0, y1 - y0);
            _roiValid = true;
        }

        /// <summary>ROI 以外塗黑（keyPoints y 即緩衝區列索引）。</summary>
        private static void ApplyMask(Color32[] px, int w, int h, RectInt roi)
        {
            int y0 = Mathf.Clamp(roi.yMin, 0, h), y1 = Mathf.Clamp(roi.yMax, 0, h);
            int x0 = Mathf.Clamp(roi.xMin, 0, w), x1 = Mathf.Clamp(roi.xMax, 0, w);
            if (y0 > 0) Array.Clear(px, 0, y0 * w);
            if (y1 < h) Array.Clear(px, y1 * w, (h - y1) * w);
            for (int y = y0; y < y1; y++)
            {
                int row = y * w;
                if (x0 > 0) Array.Clear(px, row, x0);
                if (x1 < w) Array.Clear(px, row + x1, w - x1);
            }
        }

        private void UpdateFocus(float dt)
        {
            if (!focusFollow || _vpt.inputManager == null || !HasBox) return;
            _focusTimer += dt;
            if (_focusTimer < focusInterval) return;
            _focusTimer = 0f;

            // 對焦在軀幹中心（外框中心偏上），避開背景
            Vector2 c = new Vector2(LastBox.center.x, LastBox.yMin + LastBox.height * 0.4f);
            if (_lastFocusCenter.x >= 0f && Vector2.Distance(c, _lastFocusCenter) < focusMoveRatio * ImageWidth) return;
            _lastFocusCenter = c;
            bool ok = _vpt.inputManager.SetFocusPoint(new Vector2(c.x / ImageWidth, c.y / ImageHeight));
            if (logEnabled) Log(ok ? $"對焦跟隨受測者 ({c.x:F0},{c.y:F0})" : "鏡頭不支援指定對焦點");
            if (!ok) focusFollow = false;   // 不支援就不再嘗試
        }

        private void Log(string msg)
        {
            if (logEnabled) Debug.Log($"[Subject] {msg}");
        }
    }
}
