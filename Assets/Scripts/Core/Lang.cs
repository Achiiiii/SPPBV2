using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SPPB.Core
{
    public enum Language { Zh = 0, En = 1 }

    /// <summary>
    /// 中英切換（V2.5）。
    ///  - 文字：L.T("中文", "English")，中英寫在同一處方便對照。
    ///  - 圖片：英文圖放在 Resources/LangEn/，檔名與中文原圖相同；Lang.S(原圖) 取英文版，沒有英文圖就回傳原圖。
    ///  - 場景上的 Image / TMP：LangSceneSwapper 在切換時統一替換，不用改場景。
    ///  - 選擇記在 PlayerPrefs，下次開啟沿用。
    /// </summary>
    public static class Lang
    {
        private const string PrefKey = "sppb_language";
        private static Language? _current;

        public static event Action Changed;

        public static Language Current
        {
            get
            {
                if (_current == null)
                {
                    int v = 0;
                    try { v = PlayerPrefs.GetInt(PrefKey, 0); } catch { }
                    _current = v == 1 ? Language.En : Language.Zh;
                }
                return _current.Value;
            }
        }

        public static bool IsEn => Current == Language.En;

        public static void Set(Language lang)
        {
            if (Current == lang) return;
            _current = lang;
            PlayerPrefs.SetInt(PrefKey, (int)lang);
            PlayerPrefs.Save();
            Debug.Log($"[Lang] 切換語言 → {lang}");
            Changed?.Invoke();
        }

        public static void Toggle() => Set(IsEn ? Language.Zh : Language.En);

        /// <summary>依目前語言回傳文字。</summary>
        public static string T(string zh, string en) => IsEn ? en : zh;

        // ---------------- 圖片 ----------------
        private static readonly Dictionary<Sprite, Sprite> _enOf = new Dictionary<Sprite, Sprite>();
        private static readonly Dictionary<Sprite, Sprite> _zhOf = new Dictionary<Sprite, Sprite>();

        /// <summary>中文原圖 → 目前語言的圖（英文時找 Resources/LangEn/同名圖，找不到用原圖）。</summary>
        public static Sprite S(Sprite zh)
        {
            if (zh == null) return null;
            Sprite orig = Original(zh);
            if (!IsEn) return orig;
            return EnglishOf(orig) ?? orig;
        }

        /// <summary>不論傳入中文或英文圖，回傳中文原圖。</summary>
        public static Sprite Original(Sprite s)
        {
            if (s == null) return null;
            return _zhOf.TryGetValue(s, out var zh) ? zh : s;
        }

        private static Sprite EnglishOf(Sprite zh)
        {
            if (_enOf.TryGetValue(zh, out var en)) return en;
            en = Resources.Load<Sprite>("LangEn/" + zh.name);
            _enOf[zh] = en;
            if (en != null) _zhOf[en] = zh;
            return en;
        }

        // ---------------- 場景上的固定文字 ----------------
        /// <summary>場景/Prefab 裡寫死的 TMP 文字（中文原文 → 英文）。</summary>
        public static readonly Dictionary<string, string> SceneTexts = new Dictionary<string, string>
        {
            { "想要停止測驗嗎?", "Stop the test?" },
            { "若離開，將不記錄本次測驗成績", "Leaving now won't save this test." },
            { "我們將進行3項測試，不用緊張，請依照語音指示進行操作", "We will do 3 tests. Don't worry, just follow the voice instructions." },
            { "我們將進行3項測試，不用緊張，請依照語音指示進行操作。", "We will do 3 tests. Don't worry, just follow the voice instructions." },
        };
    }

    /// <summary>
    /// 切換語言時，把場景上所有 Image 的圖、TMP 的固定文字換成目前語言。啟動時自動建立。
    /// 以「目前顯示的圖」反查中文原圖再換，所以程式後來改過的圖（例如標題）也能正確切換。
    /// </summary>
    public class LangSceneSwapper : MonoBehaviour
    {
        private readonly Dictionary<TMP_Text, string> _zhText = new Dictionary<TMP_Text, string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            var go = new GameObject("LangSceneSwapper");
            DontDestroyOnLoad(go);
            go.AddComponent<LangSceneSwapper>();
        }

        void Start()
        {
            Lang.Changed += Apply;
            Apply();
        }

        void OnDestroy() => Lang.Changed -= Apply;

        public void Apply()
        {
            foreach (var img in Resources.FindObjectsOfTypeAll<Image>())
            {
                if (img == null || !img.gameObject.scene.IsValid() || img.sprite == null) continue;
                var target = Lang.S(img.sprite);
                if (target != img.sprite) img.sprite = target;
            }
            foreach (var t in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (t == null || !t.gameObject.scene.IsValid()) continue;
                if (!_zhText.TryGetValue(t, out var zh))
                {
                    string cur = t.text != null ? t.text.Trim() : "";
                    if (!Lang.SceneTexts.ContainsKey(cur)) continue;
                    _zhText[t] = zh = cur;
                }
                t.text = Lang.IsEn ? Lang.SceneTexts[zh] : zh;
                if (Lang.IsEn && !t.enableAutoSizing)
                {
                    // 文字框比外框寬時（例如暫停面板說明），先限制在外框內
                    var prt = t.rectTransform.parent as RectTransform;
                    if (prt != null && t.rectTransform.anchorMin == t.rectTransform.anchorMax && t.rectTransform.rect.width > prt.rect.width - 24f)
                        t.rectTransform.sizeDelta = new Vector2(prt.rect.width - 24f, t.rectTransform.sizeDelta.y);
                    // 英文較長：在原框內自動縮字
                    t.fontSizeMax = t.fontSize; t.fontSizeMin = Mathf.Min(t.fontSize, 8f);
                    t.enableWordWrapping = true; t.enableAutoSizing = true;
                }
            }
        }
    }
}
