using UnityEngine;
using UnityEngine.Profiling;

namespace SPPB.Utils
{
    /// <summary>
    /// 記憶體監測：每 30 秒 log 一次 C# 託管堆積 / Unity 原生配置 / 追蹤狀態。
    /// 對照 adb dumpsys meminfo 的 Native Heap：
    ///  - Mono 跟著漲 → C# 物件被留住；
    ///  - Unity 配置跟著漲 → Unity 物件(貼圖/材質…)沒釋放；
    ///  - 兩者都平、Native Heap 仍漲 → 原生層洩漏（2026-09-29 查到 Vulkan 每幀漏 ~37KB，已改 GLES3）。
    /// 啟動時自動建立，不需掛在場景上。
    /// </summary>
    public class MemoryProbe : MonoBehaviour
    {
        public float intervalSec = 30f;
        private float _timer;
        private VideoPoseTest _vpt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            var go = new GameObject("MemoryProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<MemoryProbe>();
        }

        void Update()
        {
            _timer += Time.unscaledDeltaTime;
            if (_timer < intervalSec) return;
            _timer = 0f;

            if (_vpt == null) _vpt = FindObjectOfType<VideoPoseTest>();
            const float MB = 1024f * 1024f;
            Debug.Log($"[Mem] Mono 使用 {Profiler.GetMonoUsedSizeLong() / MB:F1}MB / 堆積 {Profiler.GetMonoHeapSizeLong() / MB:F1}MB" +
                      $" | Unity 配置 {Profiler.GetTotalAllocatedMemoryLong() / MB:F1}MB / 保留 {Profiler.GetTotalReservedMemoryLong() / MB:F1}MB" +
                      $" | 行程實際佔用 {ReadRssMB():F0}MB" +
                      $" | 追蹤={(_vpt != null && _vpt.trackingEnabled ? "開" : "關")} fps={1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime):F0}");
        }

        /// <summary>整個行程實際佔用的記憶體（/proc/self/status 的 VmRSS，含原生外掛與驅動）。讀不到回 -1。</summary>
        private static float ReadRssMB()
        {
            try
            {
                foreach (var line in System.IO.File.ReadAllLines("/proc/self/status"))
                {
                    if (!line.StartsWith("VmRSS:")) continue;
                    var parts = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                    return long.Parse(parts[1]) / 1024f;   // kB → MB
                }
            }
            catch { }
            return -1f;
        }
    }
}
