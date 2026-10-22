#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using System.Collections;
using System.IO;
using SPPB.Core;
using SPPB.Data;
using UnityEngine;

namespace SPPB.Utils
{
    /// <summary>
    /// 介面巡覽截圖（只在 Windows 版 / Editor 編進來，凱比 APK 不含）。
    /// 用參數啟動：SPPBV2.exe -uitour  → 中、英各走一遍所有頁面並截圖到 exe 旁的 tour/ 資料夾，完成後自動關閉。
    /// 用來在電腦上檢查中英切換的排版，不需要相機與骨架。
    /// </summary>
    public class UITour : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!System.Environment.GetCommandLineArgs().Contains("-uitour")) return;
            var go = new GameObject("UITour");
            DontDestroyOnLoad(go);
            go.AddComponent<UITour>();
        }

        private static readonly FlowStep[] Steps =
        {
            FlowStep.Home, FlowStep.TestIntro, FlowStep.APoseCalibration_Teaching, FlowStep.APoseCalibration,
            FlowStep.BalanceIntro, FlowStep.BalanceSideBySide_Teaching, FlowStep.BalanceSideBySide_Test,
            FlowStep.BalanceSemiTandem_Teaching, FlowStep.BalanceTandem_Teaching, FlowStep.BalanceTandem_Test,
            FlowStep.SitStandIntro, FlowStep.SitStand_Teaching, FlowStep.SitStand_Test,
            FlowStep.WalkIntro, FlowStep.Walk_Teaching, FlowStep.Walk_Test, FlowStep.Score,
        };

        private string _dir;

        IEnumerator Start()
        {
            _dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "tour");
            Directory.CreateDirectory(_dir);
            yield return new WaitForSecondsRealtime(3f);   // 等場景初始化

            foreach (var lang in new[] { Language.Zh, Language.En })
            {
                Lang.Set(lang);
                string tag = lang == Language.En ? "en" : "zh";
                foreach (var step in Steps)
                {
                    if (step == FlowStep.Score && ScoreManager.Instance != null)
                    {
                        ScoreManager.Instance.ResetAllScores();
                        ScoreManager.Instance.SetBalanceScore(3);
                        ScoreManager.Instance.SetSitStandScore(2);
                        ScoreManager.Instance.SetWalkScore(3);   // 總分 8 → 衰弱前期
                    }
                    if (step == FlowStep.Home) UIManager.Instance.GoHome();
                    else UIManager.Instance.GoToStep(step);
                    yield return new WaitForSecondsRealtime(step == FlowStep.Score ? 4.5f : 2.2f);
                    yield return Capture($"{tag}_{(int)step:00}_{step}");
                    if (step == FlowStep.TestIntro) LogTexts();
                }
                // 步行測驗 15 秒逾時會由判定器結束測驗跳到分數頁：截提示圖期間暫停判定器
                var runner = FindObjectOfType<SPPB.Core.Detection.SppbTestRunner>();
                if (runner != null) runner.enabled = false;
                yield return HintImages(tag);
                if (runner != null) runner.enabled = true;
                yield return ScoreLevels(tag);
                // 暫停面板（測驗中按離開）
                UIManager.Instance.GoToStep(FlowStep.BalanceSideBySide_Test);
                yield return new WaitForSecondsRealtime(1.5f);
                var pause = FindObjectOfType<SPPB.UI.PausePanel>(true);
                if (pause != null) { pause.Show(); yield return new WaitForSecondsRealtime(0.8f); yield return Capture($"{tag}_99_PausePanel"); pause.Hide(); }
            }
            Debug.Log("[UITour] 完成，截圖在 " + _dir);
            Lang.Set(Language.Zh);
            Application.Quit();
        }

        /// <summary>測驗中才會跳出的提示大字：用反射叫出 TestPage 的每張提示圖。</summary>
        private IEnumerator HintImages(string tag)
        {
            // 步行測驗不會觸發「偵測不到人→暫停」，提示圖不會被暫停圖示擋住
            UIManager.Instance.GoToStep(FlowStep.Walk_Test);
            yield return new WaitForSecondsRealtime(5f);   // 等倒數跑完，避免倒數圖蓋上去
            var page = FindObjectOfType<SPPB.UI.Pages.TestPage>(true);
            if (page == null) yield break;
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var show = typeof(SPPB.UI.Pages.TestPage).GetMethod("ShowHintImage", flags);
            var hide = typeof(SPPB.UI.Pages.TestPage).GetMethod("HideAndRestoreHintImage", flags);
            foreach (var f in typeof(SPPB.UI.Pages.TestPage).GetFields(flags))
            {
                if (f.FieldType != typeof(UnityEngine.UI.Image) || !f.Name.StartsWith("_hintImage_") || f.Name.Contains("Countdown")) continue;
                var img = f.GetValue(page) as UnityEngine.UI.Image;
                if (img == null) continue;
                show.Invoke(page, new object[] { img });
                yield return new WaitForSecondsRealtime(0.9f);
                yield return Capture($"{tag}_hint{f.Name.Substring(10)}");
                hide.Invoke(page, new object[] { img });
                yield return new WaitForSecondsRealtime(0.2f);
            }
        }

        /// <summary>分數頁四種評級（12 / 8 / 5 / 2 分）。</summary>
        private IEnumerator ScoreLevels(string tag)
        {
            foreach (var s in new[] { (4, 4, 4), (3, 2, 3), (2, 1, 2), (1, 0, 1) })
            {
                ScoreManager.Instance.ResetAllScores();
                ScoreManager.Instance.SetBalanceScore(s.Item1);
                ScoreManager.Instance.SetSitStandScore(s.Item2);
                ScoreManager.Instance.SetWalkScore(s.Item3);
                UIManager.Instance.GoHome();
                yield return new WaitForSecondsRealtime(1f);
                UIManager.Instance.GoToStep(FlowStep.Score);
                yield return new WaitForSecondsRealtime(4.5f);
                yield return Capture($"{tag}_score{s.Item1 + s.Item2 + s.Item3:00}");
            }
        }

        private static void LogTexts()
        {
            foreach (var t in FindObjectsOfType<TMPro.TMP_Text>())
            {
                if (string.IsNullOrEmpty(t.text) || t.text.Length < 12) continue;
                string path = t.name; var p = t.transform.parent;
                while (p != null) { path = p.name + "/" + path; p = p.parent; }
                var r = t.rectTransform;
                Debug.Log($"[UITour][TMP] {path} 寬={r.rect.width} 縮放={r.lossyScale.x:F3} 換行={t.enableWordWrapping} 自動字級={t.enableAutoSizing} 字級={t.fontSize} 溢出={t.overflowMode} 文字={t.text.Substring(0, 12)}");
            }
        }

        private IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(_dir, name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
            Debug.Log("[UITour] " + name);
        }
    }

    internal static class ArgsExt
    {
        public static bool Contains(this string[] a, string s) { foreach (var x in a) if (x == s) return true; return false; }
    }
}
#endif
