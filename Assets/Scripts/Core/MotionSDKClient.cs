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

    private float _noCorrectTimer = 0f;      // 距離上次正確動作的時間
    private const float WRONG_HINT_TIMEOUT = 3f;
    private float _testStartTime = 0f;       // 測驗開始時間（防止第一幀誤觸 finished）
    private const float FINISHED_GRACE = 1.0f;
    private bool _walkHintShown = false;
    private const float WALK_TIMEOUT_HINT = 10f;

    // 平衡測試：state=0 短暫閃爍不視為違規，需持續 VIOLATION_TIMEOUT 秒以上
    private int _previousState = 0;
    private float _balanceViolationTimer = 0f;
    private bool _balanceIsCorrect = false;  // 目前是否正在顯示正確提示
    private const float BALANCE_VIOLATION_TIMEOUT = 0.5f;

    // 捕捉 state 第一次變 0 的那一幀的 score/elapsed（SDK 結束後會把值重置成 0，debounce 之後才讀就晚了）
    private bool _finalSnapshotTaken = false;
    private float _finalScoreSnapshot = 0f;
    private float _finalElapsedSnapshot = 0f;

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
        try
        {
            initialize();
            Debug.Log("✅ Motion SDK initialized.");
        }
        catch (DllNotFoundException e)
        {
            Debug.LogError($"❌ Motion SDK DLL not found: {e.Message}. 請確認 motion_sdk.dll 的 Editor 平台已啟用。");
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Motion SDK initialize failed: {e.Message}");
        }
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

            Debug.Log("score: " + scoreJson);
            ScoreManager.Instance.SetAllScoresFromSDK(score.score1_1, score.score1_2, score.score1_3, score.score2, score.score3);
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

        _previousState = 0;
        _noCorrectTimer = 0f;
        _testStartTime = Time.time;
        _walkHintShown = false;
        _finalSnapshotTaken = false;
        _finalScoreSnapshot = 0f;
        _finalElapsedSnapshot = 0f;
        _balanceViolationTimer = 0f;
        _balanceIsCorrect = false;
        Debug.Log($"▶️ 測驗 {type} 開始");
    }

    void HandleRuntimeJson(string json)
    {
        RuntimeResult r = JsonUtility.FromJson<RuntimeResult>(json);

        // 依 type 輸出對應 SDK 欄位
        switch (currentTest)
        {
            case 1:
            case 2:
                Debug.Log($"{{ \"type\":{r.type}, \"fdis_norm\":{r.fdis_norm:F2}, \"score\":{r.score}, \"state\":{r.state}, \"elapsed\":{r.elapsed:F2} }}");
                break;
            case 3:
                Debug.Log($"{{ \"type\":{r.type}, \"dd_norm\":{r.dd_norm:F2}, \"fdis_norm\":{r.fdis_norm:F2}, \"score\":{r.score}, \"state\":{r.state}, \"elapsed\":{r.elapsed:F2} }}");
                break;
            case 4:
                Debug.Log($"{{ \"type\":{r.type}, \"diff_norm\":{r.diff_norm:F2}, \"score\":{r.score}, \"state\":{r.state}, \"elapsed\":{r.elapsed:F2} }}");
                break;
            case 5:
                int sitVal = r.sit_count > 0 ? r.sit_count : r.sit;
                Debug.Log($"{{ \"type\":{r.type}, \"sit_count\":{sitVal}, \"score\":{r.score}, \"state\":{r.state}, \"elapsed\":{r.elapsed:F2} }}");
                break;
        }

        if (currentTest == 5)
        {
            int sitVal = r.sit_count > 0 ? r.sit_count : r.sit; // 相容兩種欄位名稱
            if (sitVal > 0)
            {
                _testPage.IncrementSitStandCount(sitVal);
            }
        }

        // 平衡/步行測試（type 1-4）：動作正確/錯誤提示
        if (currentTest >= 1 && currentTest <= 4)
        {
            if (currentTest <= 3)
            {
                // 平衡測試：使用 debounce 避免 state 短暫閃爍誤判
                bool isNowCorrect = (r.state == currentTest);

                if (isNowCorrect)
                {
                    // 姿勢正確：重置違規計時器
                    _balanceViolationTimer = 0f;
                    _noCorrectTimer = 0f;

                    if (!_balanceIsCorrect)             // 剛進入正確姿勢
                    {
                        _balanceIsCorrect = true;
                        _testPage.OnActionFeedback(true, r.score);
                    }
                }
                else
                {
                    // 姿勢不正確：累積違規時間，超過閾值才視為真正違規
                    _balanceViolationTimer += Time.deltaTime;

                    if (_balanceIsCorrect && _balanceViolationTimer >= BALANCE_VIOLATION_TIMEOUT)
                    {
                        // 確認持續不正確 → 隱藏正確提示（平衡測試不顯示錯誤提示）
                        _balanceIsCorrect = false;
                        _testPage.HideCorrectHint();
                    }
                }
            }
            else
            {
                // 步行測試：elapsed 超過閾值且未完成 → 顯示錯誤提示
                if (r.elapsed > WALK_TIMEOUT_HINT && !_walkHintShown)
                {
                    _testPage.OnActionFeedback(false, r.score);
                    _walkHintShown = true;
                }

                // 更新步行進度條
                _testPage.UpdateWalkProgress(r.diff_norm);
            }

            _previousState = r.state;
        }

        // 在 state 第一次變 0 的那一幀捕捉最終 score/elapsed
        // （SDK 結束後會把回傳值重置為 0，debounce 等 0.5 秒後再讀就拿不到真正的分數）
        if (isRunning && !_finalSnapshotTaken && r.state == 0 && currentTest != 0
            && (Time.time - _testStartTime) >= FINISHED_GRACE)
        {
            _finalScoreSnapshot = r.score;
            _finalElapsedSnapshot = r.elapsed;
            _finalSnapshotTaken = true;
            Debug.Log($"[Snapshot] type={currentTest}, score={r.score}, elapsed={r.elapsed:F2}");
        }

        if (isRunning && !testFinishedPrinted && (Time.time - _testStartTime) >= FINISHED_GRACE)
        {
            // 平衡測試需等違規計時器確認，防止短暫 state=0 閃爍誤判結束
            bool stateEnded = (r.state == 0 && currentTest != 0);
            bool balanceDebounceOk = (currentTest > 3) || (_balanceViolationTimer >= BALANCE_VIOLATION_TIMEOUT);
            bool finished = stateEnded && balanceDebounceOk;

            if (finished)
            {
                // 使用 snapshot 的值（state 第一次變 0 那幀捕捉到的真正分數），而不是現在已被 SDK 重置為 0 的值
                float finalScore = _finalSnapshotTaken ? _finalScoreSnapshot : r.score;
                float finalElapsed = _finalSnapshotTaken ? _finalElapsedSnapshot : r.elapsed;

                Debug.Log($"[結束] type={currentTest}, score={finalScore}, elapsed={finalElapsed:F2} (snapshot={_finalSnapshotTaken})");
                testFinishedPrinted = true;

                // 由 SDK state=0 驅動各測驗結束
                if (currentTest == 4)
                    _testPage.OnWalkTestComplete();
                else if (currentTest >= 1 && currentTest <= 3)
                    _testPage.OnBalanceTestComplete(finalScore, finalElapsed);

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
        public int type;        // 測驗類型
        public int state;       // 當前狀態（0=結束）
        public int sit_count;   // type 5 深蹲次數（PDF 原名）
        public int sit;         // 相容舊版欄位名稱
        public float score;     // 分數
        public float elapsed;   // 已用時間（秒）
        public float diff_norm;    // 步行測試進度（實際使用欄位）
        public float fdis_norm;
        public float dd_norm;
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
