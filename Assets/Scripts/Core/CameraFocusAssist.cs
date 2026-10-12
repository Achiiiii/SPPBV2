using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SPPB.Core
{
    /// <summary>
    /// WebCamTexture 對焦輔助（依 Docs/CameraAutoFocus.md，參考廠商《NB3 相機對焦做法參考（Unity 版）》）。
    ///   A. 平常保持 autoFocusPoint = null → 連續自動對焦。不要開場設中央、也不要定期重設：
    ///      每設一次 Unity 就送 CANCEL + 單次對焦，鏡頭重新掃描 0.4～2 秒（畫面糊），之後停在單次對焦不再追焦。
    ///   B. FocusAt(uv)：把 autoFocusPoint 設到人身上觸發一次對焦，AssumedLockS + RestoreContinuousS 後設回 null。
    ///      設回 null 不會清掉對焦區域 → 之後的連續對焦仍以人身上為準。
    ///   C. 清晰度監看：用 CameraInput 已經同步讀回的整張畫面，在 CPU 上隔點取樣算 Laplacian 變異數（不另做 GPU 回讀）。
    ///      清晰度穩定、但遠低於最近 BaselineS 秒的最高分，持續 LowStableFailS → 判定卡在糊的位置 → 用 B 重新對焦。
    /// 撞牆：WebCamTexture 拿不到 AF state，「對好了沒」只能用計時與畫面推估。
    /// 門檻值取自廠商 demo（NB3、單一場景），凱比要實測再調。log tag [CamAF]。
    /// 計時一律用 unscaledTime：PausePanel 暫停時 timeScale = 0，用 Time.time 會讓還原計時停住。
    /// 這是唯一會寫 autoFocusPoint 的地方；所有呼叫都在主執行緒。
    /// </summary>
    public class CameraFocusAssist : IDisposable
    {
        const string Tag = "[CamAF]";
        const float AssumedLockS = 1f;        // 讀不到 AF state，設點後假設 1 秒鎖住
        const float RestoreContinuousS = 3f;  // 鎖住後多久切回連續對焦
        const float ScanMaxS = 2.2f;          // 設點後鏡頭最長的掃描時間（相機 ~1.98s + Unity 送 START ~0.2s），這段不看清晰度
        const float CooldownS = 3f;           // 切回連續對焦後至少隔這麼久，才會再自動觸發

        const int SampleStep = 2;             // 隔點取樣：640x480 → 等同 320x240
        const int SampleEvery = 3;            // 每幾個新相機幀算一次分（30fps → 約 10 次/秒）
        const int StableSamples = 5;          // 連續幾次分數變化不超過 StableDelta 才算穩定
        const float StableDelta = 0.10f;
        const float LowOfBaseline = 0.5f;     // 低於參考值（最近 BaselineS 秒最高分）的這個比例算「偏低」
        const float LowStableFailS = 1f;
        const float BaselineS = 5f;
        const float StatsLogS = 30f;          // 每隔多久 log 一次算分耗時與分數

        readonly WebCamTexture cam;
        readonly Func<Vector2?> targetProvider;   // 回傳要對焦的位置（autoFocusPoint 座標）；null = 沒有人

        bool focusing;                 // autoFocusPoint 已設、還沒切回 null
        float setAt = -10, restoreAt = -1, cooldownUntil;
        int lastTickFrame = -1, lastSampleFrame = -1, camFrames;

        float[] gray;
        readonly List<Vector2> history = new List<Vector2>();   // (time, score)，建參考值用
        readonly List<float> recent = new List<float>();
        float lowSince = -1;

        readonly Stopwatch costWatch = new Stopwatch();
        double costSumMs, costMaxMs;
        int costN;
        float statsAt;

        public bool IsScanning => focusing && Time.unscaledTime - setAt < ScanMaxS;
        public float LastScore { get; private set; }

        public CameraFocusAssist(WebCamTexture cam, Func<Vector2?> targetProvider)
        {
            this.cam = cam;
            this.targetProvider = targetProvider;
            cam.autoFocusPoint = null;
            statsAt = Time.unscaledTime + StatsLogS;
            Debug.Log($"{Tag} init {cam.width}x{cam.height} continuous AF, sharpness monitor=CPU step {SampleStep} every {SampleEvery} frames");
        }

        /// 每幀呼叫一次（這裡自己擋成一幀最多跑一次）。
        public void Tick()
        {
            if (Time.frameCount == lastTickFrame) return;
            lastTickFrame = Time.frameCount;
            if (restoreAt >= 0 && Time.unscaledTime >= restoreAt) RestoreContinuous("timer");
        }

        /// 有新相機幀時呼叫，px 為 CameraInput 讀回、尚未被訂閱端改動（SubjectLock 遮罩）的畫面。
        public void OnCameraFrame(Color32[] px, int w, int h)
        {
            if (px == null || Time.frameCount == lastSampleFrame) return;
            lastSampleFrame = Time.frameCount;
            if (++camFrames % SampleEvery != 0) return;

            costWatch.Restart();
            float score = Sharpness(px, w, h);
            costWatch.Stop();
            double ms = costWatch.Elapsed.TotalMilliseconds;
            costSumMs += ms; costN++;
            if (ms > costMaxMs) costMaxMs = ms;

            LastScore = score;
            Evaluate(score, Time.unscaledTime);
            LogStats();
        }

        /// 對 uv（autoFocusPoint 座標：感光元件方向、原點左下）觸發一次對焦。
        public void FocusAt(Vector2 uv, string reason)
        {
            uv = new Vector2(Mathf.Clamp(uv.x, 0.05f, 0.95f), Mathf.Clamp(uv.y, 0.05f, 0.95f));
            cam.autoFocusPoint = uv;   // Unity 自己送 CANCEL → AUTO + 單次 START
            focusing = true;
            setAt = Time.unscaledTime;
            restoreAt = setAt + AssumedLockS + RestoreContinuousS;
            // 掃描期間的分數不算；參考值重建，免得拿換場景前的高分一直判定偏低
            ResetBaseline();
            Debug.Log($"{Tag} focus at ({uv.x:F3},{uv.y:F3}) reason={reason}, restore continuous in {AssumedLockS + RestoreContinuousS:F0}s");
        }

        /// 畫面內容大幅改變（例如頭部轉動）時呼叫：清晰度參考值重新累積，避免拿轉動前的高分誤判為對焦卡住。
        public void ResetBaseline()
        {
            history.Clear();
            recent.Clear();
            lowSince = -1;
        }

        void RestoreContinuous(string reason)
        {
            restoreAt = -1;
            focusing = false;
            cooldownUntil = Time.unscaledTime + CooldownS;
            cam.autoFocusPoint = null;   // 切回連續對焦；對焦區域會留在上一次設的位置
            Debug.Log($"{Tag} restore continuous ({reason})");
        }

        /// App 切到背景：先切回連續對焦，免得計時器漏掉還原。
        public void OnPause()
        {
            if (cam != null) cam.autoFocusPoint = null;
            focusing = false;
            restoreAt = -1;
            ResetBaseline();
        }

        void Evaluate(float score, float t)
        {
            if (t - setAt < ScanMaxS) return;   // 鏡頭還在掃描

            history.Add(new Vector2(t, score));
            history.RemoveAll(h => t - h.x > BaselineS);
            float baseline = 0;
            foreach (var h in history) baseline = Mathf.Max(baseline, h.y);

            recent.Add(score);
            if (recent.Count > StableSamples) recent.RemoveAt(0);
            bool stable = recent.Count == StableSamples;
            for (int i = 1; stable && i < recent.Count; i++)
                stable = Mathf.Abs(recent[i] - recent[i - 1]) <= StableDelta * Mathf.Max(recent[i], recent[i - 1]);
            bool low = history.Count >= StableSamples * 2 && score < LowOfBaseline * baseline;

            if (!(stable && low) || focusing || t < cooldownUntil) { lowSince = -1; return; }
            if (lowSince < 0) { lowSince = t; return; }
            if (t - lowSince < LowStableFailS) return;

            var target = targetProvider?.Invoke() ?? new Vector2(0.5f, 0.5f);
            Debug.Log($"{Tag} stable but low score={score:F0} baseline={baseline:F0} for {(t - lowSince) * 1000:F0}ms → refocus");
            FocusAt(target, "sharpness-low");
        }

        /// 清晰度：隔 SampleStep 點取灰階，再算 Laplacian 變異數，越大越清楚。只有同一場景的分數能互相比。
        float Sharpness(Color32[] px, int w, int h)
        {
            int gw = w / SampleStep, gh = h / SampleStep;
            if (gw < 3 || gh < 3 || px.Length < w * h) return 0f;
            if (gray == null || gray.Length != gw * gh) gray = new float[gw * gh];
            for (int gy = 0; gy < gh; gy++)
            {
                int src = gy * SampleStep * w, dst = gy * gw;
                for (int gx = 0; gx < gw; gx++)
                {
                    var c = px[src + gx * SampleStep];
                    gray[dst + gx] = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
                }
            }

            double sum = 0, sq = 0;
            int n = 0;
            for (int y = 1; y < gh - 1; y++)
            {
                int row = y * gw;
                for (int x = 1; x < gw - 1; x++)
                {
                    int i = row + x;
                    float lap = gray[i - 1] + gray[i + 1] + gray[i - gw] + gray[i + gw] - 4 * gray[i];
                    sum += lap; sq += lap * lap; n++;
                }
            }
            double mean = sum / n;
            return (float)(sq / n - mean * mean);
        }

        void LogStats()
        {
            if (Time.unscaledTime < statsAt || costN == 0) return;
            statsAt = Time.unscaledTime + StatsLogS;
            Debug.Log($"{Tag} sharpness cost avg={costSumMs / costN:F2}ms max={costMaxMs:F2}ms n={costN}, score={LastScore:F0}, focusing={focusing}");
            costSumMs = 0; costMaxMs = 0; costN = 0;
        }

        public void Dispose()
        {
            if (cam != null) cam.autoFocusPoint = null;
            gray = null;
        }
    }
}
