using System;
using System.Runtime.InteropServices;
using UnityEngine;
using SPPB.Core;
using SPPB.UI.Pages;


public class MotionSDKClient : MonoBehaviour
{
    [SerializeField] private TestPage _testPage;
    public VideoPoseTest poseSource;

    public bool start1_1_single;
    public bool start1_2_single;
    public bool start1_3_single;
    public bool start2_single;
    public bool start3_single;
    public bool end = false;

    // 假設每幀 VideoPoseTest.myText 會傳出 3D 座標 list
    private string originString = "";

    private bool isRunning = false;
    private int currentTest = 0;          // 目前進行中的測驗
    private bool testFinishedPrinted = true;

#if UNITY_ANDROID && !UNITY_EDITOR
    const string LIB_NAME = "motion_sdk";   // Android .so
#else
    const string LIB_NAME = "motion_sdk";   // Windows .dll
#endif

    [DllImport(LIB_NAME)] private static extern void initialize();
    [DllImport(LIB_NAME)] private static extern void start_exercise(int type);
    [DllImport(LIB_NAME)] private static extern void stop_exercise();
    [DllImport(LIB_NAME)] private static extern IntPtr process_points(float[] points, int num_points, int dim);
    [DllImport(LIB_NAME)] private static extern IntPtr get_score();

    void Start()
    {
        initialize();
        Debug.Log("✅ Motion SDK initialized.");
    }

    void Update()
    {
        if (poseSource == null)
            return;

        // -------------------------
        // 控制不同動作開始
        // -------------------------
        if (!isRunning)
        {
            if (start1_1_single) StartTest(1);
            else if (start1_2_single) StartTest(2);
            else if (start1_3_single) StartTest(3);
            else if (start2_single) StartTest(4);
            else if (start3_single) StartTest(5);
        }

        // -------------------------
        // 處理每幀新資料
        // -------------------------
        string raw = poseSource.videoPoseData;
        if (string.IsNullOrEmpty(raw)) return;

        float[] pts = ConvertStringToFloatArray(raw, out int numPts, out int dim);
        if (pts == null || numPts < 22) return;

        string json = Marshal.PtrToStringAnsi(
            process_points(pts, numPts, dim)
        );
        Debug.Log(json);

        if (!string.IsNullOrEmpty(json))
        {
            HandleRuntimeJson(json);
        }

        // -------------------------
        // 結束指令
        // -------------------------
        if (end)
        {
            stop_exercise();
            isRunning = false;
            end = false;

            string scoreJson = Marshal.PtrToStringAnsi(get_score());
            ScoreData score = JsonUtility.FromJson<ScoreData>(scoreJson);

            // 可用 JSON Utility 解析成物件後顯示分數
            Debug.Log("score: " + scoreJson);
            ScoreManager.Instance.SetAllScoresFromSDK(score.score1_1, score.score1_2, score.score1_3, score.score2, score.score3);
            // sppb_script.test1_score.text = $"{score.score1_1 + score.score1_2 + score.score1_3}分";
            // sppb_script.test2_score.text = $"{score.score2}分";
            // sppb_script.test3_score.text = $"{score.score3}分";
            // sppb_script.total_score.text = $"{score.total}分";
        }
    }

    void StartTest(int type)
    {
        start_exercise(type);
        currentTest = type;
        isRunning = true;
        testFinishedPrinted = false;

        start1_1_single = start1_2_single =
        start1_3_single = start2_single =
        start3_single = false;

        Debug.Log($"▶️ 測驗 {type} 開始");
    }

    void HandleRuntimeJson(string json)
    {
        RuntimeResult r = JsonUtility.FromJson<RuntimeResult>(json);

        if (currentTest == 5 && r.sit > 0)
        {
            _testPage.IncrementSitStandCount(r.sit);
            Debug.Log($"坐站進度：{r.sit}/5");
        }

        if (isRunning && !testFinishedPrinted)
        {
            bool finished =
                (r.state == 0 && currentTest != 0) ||
                (currentTest == 3 && r.type == 10);

            if (finished)
            {
                Debug.Log($"測驗 {currentTest} 結束，分數 = {r.score}");
                testFinishedPrinted = true;
                isRunning = false;
                currentTest = 0;
            }
        }
    }

    public void ResetMotionSDK()
    {
        initialize();
    }

    float[] ConvertStringToFloatArray(string raw, out int numPoints, out int dim)
    {
        raw = raw.Replace(" ", "").Trim('[', ']');
        string[] parts = raw.Split(new string[] { "),(" }, StringSplitOptions.RemoveEmptyEntries);

        numPoints = parts.Length;
        dim = 3;

        float[] values = new float[numPoints * dim];
        int idx = 0;

        foreach (string p in parts)
        {
            string[] xyz = p.Replace("(", "").Replace(")", "").Split(',');
            for (int i = 0; i < 3; i++)
                values[idx++] = float.TryParse(xyz[i], out float v) ? v : 0f;
        }
        return values;
    }

    [Serializable]
    public class RuntimeResult
    {
        public int type;    // exe_type
        public int state;   // same as exe_type in DLL
        public int sit;
        public float score;
    }


    [Serializable]
    public class ScoreData
    {
        public int score1_1;
        public int score1_2;
        public int score1_3;
        public int score2;
        public int score3;
        public int total;
    }
}
