// using System;
// using System.Runtime.InteropServices;
// using System.Text;
// using UnityEngine;
// using UnityEngine.UI;
// using System.Collections.Generic;
// using System.Globalization;


// public class MotionSDKClient : MonoBehaviour
// {
//     public sppb_controller sppb_script;
//     private VideoPoseTest scriptAReference;

//     public bool start1_1_single = false;
//     public bool start1_2_single = false;
//     public bool start1_3_single = false;
//     public bool start2_single = false;
//     public bool start3_single = false;
//     public bool startModel = false;
//     public bool end = false;

//     // 假設每幀 VideoPoseTest.myText 會傳出 3D 座標 list
//     private string originString = "";

// #if UNITY_ANDROID && !UNITY_EDITOR
//     const string LIB_NAME = "motion_sdk";   // Android .so
// #else
//     const string LIB_NAME = "motion_sdk";   // Windows .dll
// #endif

//     [DllImport(LIB_NAME)] private static extern void initialize();
//     [DllImport(LIB_NAME)] private static extern void start_exercise(int type);
//     [DllImport(LIB_NAME)] private static extern void stop_exercise();
//     [DllImport(LIB_NAME)] private static extern IntPtr process_points(float[] points, int num_points, int dim);
//     [DllImport(LIB_NAME)] private static extern IntPtr get_score();

//     void Start()
//     {
//         scriptAReference = GetComponent<VideoPoseTest>();
//         initialize();
//         Debug.Log("✅ Motion SDK initialized.");
//     }
//     // void Start()
//     // {
//     //     initialize();
//     //     Debug.Log("✅ Motion SDK initialized.");

//     //     // 👉 測試開始一個分析
//     //     start_exercise(1);
//     //     Debug.Log("🏁 Started Exercise Type 1");

//     //     // 建立測試資料（3 個 3D 點）
//     //     float[] points = new float[]
//     //     {
//     //         0.151f, 0.548f, 0.548f,
//     //         0.55f,  0.47f,  0.68f,
//     //         0.33f,  0.52f,  0.71f
//     //     };

//     //     // 傳入分析
//     //     IntPtr resultPtr = process_points(points, 3, 3);
//     //     string json = Marshal.PtrToStringAnsi(resultPtr);
//     //     Debug.Log("🧩 SDK Result: " + json);

//     //     // 取分數
//     //     IntPtr scorePtr = get_score();
//     //     string scoreJson = Marshal.PtrToStringAnsi(scorePtr);
//     //     Debug.Log("📊 SDK Score: " + scoreJson);
//     // }

//     void Update()
//     {
//         if (!startModel || scriptAReference == null)
//             return;

//         // -------------------------
//         // 控制不同動作開始
//         // -------------------------
//         if (start1_1_single) { start_exercise(1); start1_1_single = false; }
//         if (start1_2_single) { start_exercise(2); start1_2_single = false; }
//         if (start1_3_single) { start_exercise(3); start1_3_single = false; }
//         if (start2_single)   { start_exercise(4); start2_single = false; }
//         if (start3_single)   { start_exercise(5); start3_single = false; }

//         // -------------------------
//         // 處理每幀新資料
//         // -------------------------
//         string newString = scriptAReference.myText;
//         if (originString != newString)
//         {
//             float[] flatPoints = ConvertStringToFloatArray(newString, out int numPoints, out int dim);
//             if (flatPoints != null)
//             {
//                 IntPtr ptr = process_points(flatPoints, numPoints, dim);
//                 string resultJson = Marshal.PtrToStringAnsi(ptr);
//                 if (!string.IsNullOrEmpty(resultJson))
//                 {
//                     Debug.Log($"SDK Result: {resultJson}");
//                     // 這裡可解析 JSON 更新 UI
//                 }
//             }
//             originString = newString;
//         }

//         // -------------------------
//         // 結束指令
//         // -------------------------
//         if (end)
//         {
//             stop_exercise();
//             IntPtr ptr = get_score();
//             string resultJson = Marshal.PtrToStringAnsi(ptr);
//             Debug.Log($"Final Score: {resultJson}");
//             end = false;

//             // 可用 JSON Utility 解析成物件後顯示分數
//             try
//             {
//                 var score = JsonUtility.FromJson<ScoreData>(resultJson);
//                 sppb_script.test1_score.text = $"{score.score1_1 + score.score1_2 + score.score1_3}分";
//                 sppb_script.test2_score.text = $"{score.score2}分";
//                 sppb_script.test3_score.text = $"{score.score3}分";
//                 sppb_script.total_score.text = $"{score.total}分";
//             }
//             catch
//             {
//                 Debug.LogWarning("⚠️ Score JSON parse failed");
//             }
//         }
//     }

//     float[] ConvertStringToFloatArray(string raw, out int numPoints, out int dim)
//     {
//         try
//         {
//             if (string.IsNullOrWhiteSpace(raw))
//             {
//                 numPoints = 0;
//                 dim = 0;
//                 return null;
//             }

//             // 移除多餘空白與換行
//             raw = raw.Replace("\n", "").Replace("\r", "").Replace(" ", "");

//             // 去掉最外層括號
//             raw = raw.TrimStart('[').TrimEnd(']');

//             // 用 "),(" 分割每一組座標
//             string[] parts = raw.Split(new string[] { "),(" }, StringSplitOptions.RemoveEmptyEntries);

//             numPoints = parts.Length;
//             dim = 3;
//             List<float> values = new List<float>();

//             foreach (string part in parts)
//             {
//                 // 清除左右括號
//                 string clean = part.Replace("(", "").Replace(")", "");
//                 string[] xyz = clean.Split(',');

//                 for (int j = 0; j < xyz.Length && j < dim; j++)
//                 {
//                     if (float.TryParse(xyz[j], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float val))
//                         values.Add(val);
//                     else
//                         values.Add(0f); // 若轉換失敗，用 0 補
//                 }
//             }

//             return values.ToArray();
//         }
//         catch (Exception ex)
//         {
//             Debug.LogError($"ConvertStringToFloatArray failed: {ex.Message}\nRaw Input: {raw}");
//             numPoints = 0;
//             dim = 0;
//             return null;
//         }
//     }


//     [Serializable]
//     public class ScoreData
//     {
//         public int score1_1;
//         public int score1_2;
//         public int score1_3;
//         public int score2;
//         public int score3;
//         public int total;
//     }
// }
