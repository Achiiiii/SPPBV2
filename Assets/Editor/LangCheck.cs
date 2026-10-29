using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 中英切換檢查：列出場景上用到、且在 Resources/LangEn 有英文版的圖，以及 LangEn 裡沒被用到的圖。
/// Batchmode：Unity.exe -batchmode -quit -projectPath . -executeMethod LangCheck.Run -logFile langcheck.log
/// </summary>
public static class LangCheck
{
    [MenuItem("SPPB/Check English Sprites")]
    public static void Run()
    {
        var en = Resources.LoadAll<Sprite>("LangEn").ToDictionary(s => s.name, s => s);
        Debug.Log($"[LangCheck] LangEn 英文圖 {en.Count} 張（必須是 Sprite 才會被載入）");

        var used = new HashSet<string>();
        // 場景上的 Image
        var scene = EditorSceneManager.OpenScene(EditorBuildSettings.scenes.First(s => s.enabled).path, OpenSceneMode.Single);
        foreach (var root in scene.GetRootGameObjects())
            foreach (var img in root.GetComponentsInChildren<Image>(true))
                if (img.sprite != null && en.ContainsKey(img.sprite.name)) used.Add(img.sprite.name);
        // 所有 MonoBehaviour 上序列化的 Sprite 欄位（程式設定的標題圖等）
        foreach (var root in scene.GetRootGameObjects())
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                var so = new SerializedObject(mb); var it = so.GetIterator();
                while (it.NextVisible(true))
                    if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue is Sprite sp && en.ContainsKey(sp.name))
                        used.Add(sp.name);
            }
        Debug.Log($"[LangCheck] 有對應到的英文圖 {used.Count} 張：{string.Join("、", used.OrderBy(x => x))}");
        var unused = en.Keys.Where(k => !used.Contains(k)).OrderBy(x => x).ToList();
        Debug.Log($"[LangCheck] 沒對應到的英文圖 {unused.Count} 張：{string.Join("、", unused)}");
        var langBtn = Resources.Load<Sprite>("UI/lang_button");
        Debug.Log($"[LangCheck] 語言按鈕底圖 UI/lang_button：{(langBtn != null ? "OK" : "找不到")}");
    }
}
