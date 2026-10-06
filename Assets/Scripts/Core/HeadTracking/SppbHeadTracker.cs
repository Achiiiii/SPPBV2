using UnityEngine;
using SPPB.Core.Detection;

namespace SPPB.Core.HeadTracking
{
    /// <summary>
    /// 凱比頭部自動追蹤受測者、讓全身入鏡（移植自 KebbiHeadTracking 專案的 Body25 框位演算法，參數為實機調過的值）。
    /// 相機在頭上、隨頭動 = 閉環：把人移到畫面中央後誤差自動收斂。
    ///
    /// 使用規則：
    ///  - 追蹤從定位校準開始（VideoPose 開始吐骨架後才有資料；detect_apose 需先偵測到 A-pose）。
    ///  - 測驗進行中 Frozen=true 凍結頭部：轉頭會改變相機座標，影響平衡腳距與步行深度判定。
    ///  - 只追蹤受測者：鎖定後若 SubjectLock 判定為「其他人」，不跟著轉。
    ///  - 回首頁時 ReturnHome()。
    /// </summary>
    public class SppbHeadTracker : MonoBehaviour
    {
        [Header("開關")]
        public bool trackingEnabled = true;
        [Tooltip("由 TestPage 控制：測驗進行中凍結頭部")]
        public bool Frozen;

        [Header("框位 (BODY_25)")]
        [Tooltip("pose 超過此秒數沒更新 = 找不到人")] public float poseStaleSec = 0.5f;
        [Tooltip("離畫面邊緣多近算「貼邊/出畫面」(比例)")] public float edgeMargin = 0.03f;
        [Tooltip("頭或腳出畫面時，每次尋找的誤差量(→角度 = kp*此值*FOV)")] public float searchErr = 0.2f;
        [Tooltip("採用的最低信心值（實測真點 0.7~0.9，畫面外猜測點 ≤0.5）")] public float body25MinConf = 0.55f;
        [Tooltip("有效點少於此數 = 找不到人")] public int minKeyPoints = 5;

        [Header("控制參數")]
        public float deadzoneX = 0.06f;
        public float deadzoneY = 0.08f;
        [Tooltip("比例增益（KebbiHeadTracking 用 0.4；SPPB 影像延遲較長，調低避免擺盪）")]
        public float kp = 0.25f;
        public float cameraHFovDeg = 70f;
        public float cameraVFovDeg = 90f;
        [Tooltip("每次指令最多轉幾度")]
        public float maxStepDeg = 4f;
        [Tooltip("目標最多超前實機角度幾度（防延遲造成轉過頭）")]
        public float maxLeadDeg = 8f;
        [Tooltip("垂直目標最多超前實機幾度。neck_y 實測離目標差 ~8° 內會推不動而停住（15:10 log：目標 -2.7°、實機卡 -10.7°），" +
                 "所以要讓目標繼續累加到推得動為止；全身入鏡時 HoldCurrent 會把目標拉回實機角度")]
        public float maxPitchLeadDeg = 20f;
        [Tooltip("垂直馬達：目標變化超過此度數才立即重送")]
        public float pitchResendDeltaDeg = 2f;
        [Tooltip("垂直馬達：目標小幅變化時，至少間隔多久才重送（給馬達時間完成動作）")]
        public float pitchResendInterval = 0.5f;
        [Tooltip("馬達速度(下限10)")] public float motorSpeed = 25f;
        public float commandHz = 8f;
        public float verticalBias = 0.05f;

        [Header("角度上下限")]
        public float yawMinDeg = -80f, yawMaxDeg = 80f;
        public float pitchMinDeg = -25f, pitchMaxDeg = 30f;
        [Tooltip("水平方向若轉反了(人在右頭往左)就打勾")] public bool invertYaw = false;
        [Tooltip("實機 neck_y 抬頭方向的正負號（2026-09-29 實測：+ 是低頭 → -1）")]
        public float pitchUpSign = -1f;

        [Header("找不到人")]
        public bool returnHomeWhenLost = true;
        public float lostGraceSec = 1.5f;
        public float yawHomeDeg = 0f, pitchHomeDeg = 0f;

        [Header("除錯")]
        public bool logEnabled = true;

        // 狀態（給 UI/除錯）
        public bool IsFullBodyInView { get; private set; }
        public bool IsSettled { get; private set; }

        private VideoPoseTest _vpt;
        private float _yawDeg, _pitchDeg, _yawActual, _pitchActual;
        private float _cmdTimer, _lostTimer, _settledTimer, _logTimer;
        private volatile bool _serviceStartSignal;
        private bool _nuwaReady;
        private float _readyWaitTimer;
        private const float ReadyFallbackSec = 3f;
        private bool _editorStub;

        private struct Framing { public float centerX01, topY01, bottomY01, errY; public bool fullBody, headIn, feetIn; }

        // OpenPose BODY_25
        private const int K_Nose = 0, K_Neck = 1;
        private static readonly int[] K_Head = { 0, 15, 16, 17, 18 };
        private static readonly int[] K_Feet = { 11, 14, 19, 20, 21, 22, 23, 24 };

        public void Init(VideoPoseTest vpt) => _vpt = vpt;

        void Awake()
        {
#if UNITY_EDITOR
            _editorStub = true;   // Editor 下馬達 API 是空殼
#endif
            Nuwa.onWikiServiceStart += OnNuwaServiceStart;   // Nuwa.init 由 NuwaEventTrigger 呼叫
            _yawDeg = yawHomeDeg; _pitchDeg = pitchHomeDeg;
            _yawActual = _yawDeg; _pitchActual = _pitchDeg;
        }

        void OnDestroy() => Nuwa.onWikiServiceStart -= OnNuwaServiceStart;
        private void OnNuwaServiceStart() => _serviceStartSignal = true;

        /// <summary>頭轉回預設位置（回首頁時）。</summary>
        public void ReturnHome()
        {
            _lostTimer = 0f; _settledTimer = 0f;
            DriveTo(yawHomeDeg, pitchHomeDeg, true);
            Log("頭部歸位");
        }

        private int _posesThisSec;
        private int _cmdSent, _yawFail, _pitchFail, _pitchSent;   // 每秒統計
        private float _lastPitchSent, _lastPitchSendTime;
        private bool _pitchSentOnce;
        private float _poseRateTimer, _poseRate;

        /// <summary>頭部是否正在主動追蹤（未凍結）。追蹤時 SubjectLock 暫停區域遮罩，避免遮掉正要進畫面的頭腳。</summary>
        public bool IsActive => trackingEnabled && !Frozen && _vpt != null && _vpt.trackingEnabled;

        void Update()
        {
            UpdateNuwaReady();
            if (_vpt != null && _vpt.subject != null) _vpt.subject.maskSuspended = IsActive;
            if (!IsActive)
            {
                // 剛凍結（測驗開始）：讓頭停在目前位置，避免繼續轉向先前的目標
                if (Frozen && !_heldForFreeze) { HoldCurrent(); _heldForFreeze = true; }
                return;
            }
            _heldForFreeze = false;

            _cmdTimer += Time.deltaTime;
            if (_cmdTimer < 1f / Mathf.Max(1f, commandHz)) return;
            float dt = _cmdTimer; _cmdTimer = 0f;

            // 控制方式與 KebbiHeadTracking 相同：每秒 commandHz 次、用最新骨架
            _posesThisSec++;
            _poseRateTimer += dt;
            if (_poseRateTimer >= 1f) { _poseRate = _posesThisSec / _poseRateTimer; _posesThisSec = 0; _poseRateTimer = 0f; }

            _prevPitchActual = _pitchActual;   // 上一 tick 的實機角度（判斷馬達是否正在動）
            RefreshActual();

            if (!TryComputeFraming(out Framing f))
            {
                IsSettled = false; IsFullBodyInView = false;
                _lostTimer += dt;
                if (returnHomeWhenLost && _lostTimer >= lostGraceSec)
                    DriveTo(Mathf.Lerp(_yawDeg, yawHomeDeg, 0.25f), Mathf.Lerp(_pitchDeg, pitchHomeDeg, 0.25f));
                return;
            }
            _lostTimer = 0f;
            IsFullBodyInView = f.fullBody;

            float ex = f.centerX01 - 0.5f;
            float ey = f.errY;   // >0 = 人在畫面偏下 → 要低頭
            bool inX = Mathf.Abs(ex) < deadzoneX, inY = Mathf.Abs(ey) < deadzoneY;
            if (inX) ex = 0f;
            if (inY) ey = 0f;

            LogStatus(dt, f);

            if (inX && inY)
            {
                if (_settledTimer == 0f) HoldCurrent();   // 剛對準：停在目前角度，不再往前衝
                _settledTimer += dt;
                IsSettled = _settledTimer > 0.3f && f.fullBody;
                return;
            }
            _settledTimer = 0f; IsSettled = false;

            float dYaw = Mathf.Clamp(kp * ex * cameraHFovDeg, -maxStepDeg, maxStepDeg) * (invertYaw ? -1f : 1f);
            float dPitch = Mathf.Clamp(kp * ey * cameraVFovDeg, -maxStepDeg, maxStepDeg) * -pitchUpSign;

            // 兩軸都用「目標累加」：目標持續變化馬達才會動（以實機角度為基準會重送同一目標，凱比馬達每收到
            // 新指令就重新起步 → 卡住不動，實測上下軸卡在固定差 6°）。
            // 目標不可超前實機 maxLeadDeg：SPPB 影像延遲較長，超前太多會轉過頭而左右擺盪。
            // 已在中間的軸，目標設為目前角度讓頭停住。
            bool haveActual = !_editorStub && _nuwaReady;
            float yawNow = haveActual ? _yawActual : _yawDeg;
            float pitchNow = haveActual ? _pitchActual : _pitchDeg;
            float yawT = inX ? yawNow : Mathf.Clamp(_yawDeg + dYaw, yawNow - maxLeadDeg, yawNow + maxLeadDeg);

            // 垂直：0=全身入鏡微調置中、+1=腳出畫面找腳、-1=頭出畫面找頭。
            // 模式切換（剛入鏡、或找的方向反轉）→ 目標拉回實機角度，丟掉累積的超前量，避免衝過頭來回擺。
            int vMode = f.fullBody ? 0 : (f.errY > 0f ? 1 : -1);
            bool forcePitch = false;
            if (vMode != _lastVMode) { _pitchDeg = pitchNow; forcePitch = true; }
            _lastVMode = vMode;

            float pitchT;
            if (inY) pitchT = pitchNow;
            else
            {
                // 馬達正朝目標移動中 → 不再加碼（避免衝過頭）；推不動才繼續累加超前量。
                // neck_y 離目標 ~8° 內會停住不動，找頭腳時允許超前到 maxPitchLeadDeg；全身微調只用 maxLeadDeg。
                bool moving = Mathf.Sign(dPitch) * (pitchNow - _prevPitchActual) > 0.5f;
                float lead = vMode == 0 ? maxLeadDeg : maxPitchLeadDeg;
                pitchT = moving ? _pitchDeg : Mathf.Clamp(_pitchDeg + dPitch, pitchNow - lead, pitchNow + lead);
            }
            DriveTo(yawT, pitchT, forcePitch);
        }

        private int _lastVMode = int.MinValue;
        private float _prevPitchActual;

        private bool _heldForFreeze;

        /// <summary>停在目前實機角度（凍結、對準時用）。</summary>
        private void HoldCurrent()
        {
            RefreshActual();
            bool haveActual = !_editorStub && _nuwaReady;
            DriveTo(haveActual ? _yawActual : _yawDeg, haveActual ? _pitchActual : _pitchDeg, true);
        }

        private bool TryComputeFraming(out Framing f)
        {
            f = default;
            var subject = _vpt.subject;
            if (subject == null || subject.PoseAge > poseStaleSec) return false;
            // 鎖定後只追蹤受測者：偵測到其他人或離開時不跟著轉
            if (subject.IsLocked && subject.CurrentState != SubjectLock.State.Tracking) return false;

            var k = subject.Keypoints;
            float W = subject.ImageWidth, H = subject.ImageHeight;
            if (k == null || k.Length < 25 || W <= 0 || H <= 0) return false;

            bool Ok(int i) => k[i].z >= body25MinConf;
            bool InFrame(int i) => Ok(i) && k[i].y > H * edgeMargin && k[i].y < H * (1f - edgeMargin)
                                          && k[i].x > W * edgeMargin && k[i].x < W * (1f - edgeMargin);

            int conf = 0;
            float topPx = float.MaxValue, botPx = float.MinValue, sumX = 0f;
            for (int i = 0; i < 25; i++)
            {
                if (!Ok(i)) continue;
                conf++;
                topPx = Mathf.Min(topPx, k[i].y); botPx = Mathf.Max(botPx, k[i].y); sumX += k[i].x;
            }
            if (conf < minKeyPoints) return false;

            bool headIn = false; foreach (int i in K_Head) headIn |= InFrame(i);
            bool feetIn = false; foreach (int i in K_Feet) feetIn |= InFrame(i);
            f.headIn = headIn; f.feetIn = feetIn;

            // 水平：脖子 > 鼻子 > 有信心點平均
            f.centerX01 = (Ok(K_Neck) ? k[K_Neck].x : Ok(K_Nose) ? k[K_Nose].x : sumX / conf) / W;

            // 頭頂往上留「鼻→脖子」一半距離（頭髮/頭頂不在關節點上）
            float margin = (Ok(K_Nose) && Ok(K_Neck)) ? Mathf.Abs(k[K_Neck].y - k[K_Nose].y) * 0.5f : 0.04f * H;
            f.topY01 = (topPx - margin) / H;
            f.bottomY01 = botPx / H;
            f.fullBody = headIn && feetIn;

            if (f.fullBody)
            {
                // 置中，但移動量不可超過另一端剩下邊距的一半：人佔滿畫面時為了置中會把頭或腳推出去 → 來回擺
                float e = (f.topY01 + f.bottomY01) * 0.5f - (0.5f - verticalBias);
                float roomUp = Mathf.Max(0f, f.topY01 - edgeMargin) * 0.5f;              // 低頭(e>0)時頭頂還能往上移多少
                float roomDown = Mathf.Max(0f, 1f - edgeMargin - f.bottomY01) * 0.5f;    // 抬頭(e<0)時腳還能往下移多少
                f.errY = Mathf.Clamp(e, -roomDown, roomUp);
            }
            else if (!headIn && feetIn) f.errY = -searchErr;   // 頭出畫面 → 抬頭
            else if (headIn && !feetIn) f.errY = +searchErr;   // 腳出畫面 → 低頭
            else f.errY = headIn ? 0f : -searchErr;            // 太近：先把頭找回來
            return true;
        }

        private void DriveTo(float yaw, float pitch, bool forcePitch = false)
        {
            yaw = Mathf.Clamp(yaw, yawMinDeg, yawMaxDeg);
            pitch = Mathf.Clamp(pitch, pitchMinDeg, pitchMaxDeg);
            bool okY = Nuwa.setMotorPositionInDegree(Nuwa.NuwaMotorType.neck_z, yaw, motorSpeed);

            // 垂直(neck_y)馬達每收到指令就重新起步：同目標每 0.125 秒重送會讓它還沒動就被重置而卡住
            // （實測目標 -0.9°、實機停在 -8.9° 達 15 秒，指令未被拒）。目標沒明顯變化時讓馬達把動作做完。
            float now = Time.unscaledTime;
            float change = Mathf.Abs(pitch - _lastPitchSent);
            bool sendPitch = forcePitch || !_pitchSentOnce
                             || change >= pitchResendDeltaDeg
                             || (change >= 0.3f && now - _lastPitchSendTime >= pitchResendInterval);
            bool okP = true;
            if (sendPitch)
            {
                okP = Nuwa.setMotorPositionInDegree(Nuwa.NuwaMotorType.neck_y, pitch, motorSpeed);
                _lastPitchSent = pitch;
                _lastPitchSendTime = now;
                _pitchSentOnce = true;
            }
            if (!_editorStub)
            {
                if (!okY) _yawFail++;
                if (!okP) _pitchFail++;
                _cmdSent++;
                if (sendPitch) _pitchSent++;
            }
            _yawDeg = yaw; _pitchDeg = pitch;
            if (_editorStub) { _yawActual = yaw; _pitchActual = pitch; }
        }

        private void UpdateNuwaReady()
        {
            if (_nuwaReady || _editorStub) return;
            _readyWaitTimer += Time.deltaTime;
            if (!_serviceStartSignal && _readyWaitTimer < ReadyFallbackSec) return;
            _nuwaReady = true;
            RefreshActual();
            _yawDeg = _yawActual; _pitchDeg = _pitchActual;   // 從實機目前角度開始
        }

        private void RefreshActual()
        {
            if (_editorStub || !_nuwaReady) return;
            _yawActual = Nuwa.getMotorPresentPossitionInDegree(Nuwa.NuwaMotorType.neck_z);
            _pitchActual = Nuwa.getMotorPresentPossitionInDegree(Nuwa.NuwaMotorType.neck_y);
        }

        private void LogStatus(float dt, Framing f)
        {
            if (!logEnabled) return;
            _logTimer += dt;
            if (_logTimer < 1f) return;
            _logTimer = 0f;
            string s = f.fullBody ? "全身入鏡" : !f.headIn && !f.feetIn ? "太近" : !f.headIn ? "頭出畫面→抬頭" : "腳出畫面→低頭";
            Log($"{s} 中心x={f.centerX01:F2} 上={f.topY01:F2} 下={f.bottomY01:F2} yaw={_yawDeg:F1}°(實機 {_yawActual:F1}°) pitch={_pitchDeg:F1}°(實機 {_pitchActual:F1}°) 指令 {_poseRate:F1}/秒 送出{_cmdSent}(垂直{_pitchSent}) 水平被拒{_yawFail} 垂直被拒{_pitchFail}");
            _cmdSent = _yawFail = _pitchFail = _pitchSent = 0;
        }

        private void Log(string msg) { if (logEnabled) Debug.Log($"[HeadTrack] {msg}"); }
    }
}
