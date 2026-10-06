using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq; // �K�[�o�Ӥޥ�

public class HandDetect : MonoBehaviour
{
    public VideoPoseTest videoPoseTest;
    private string originstring = "";
    float distance = 0;
    public Transform pointer;
    private int screenWidth;
    private int screenHeight;
    private float zeropos_x;
    private int curX;
    private int curY;
    private Vector2 curPos;
    private Vector2 smoothedPos;
    bool first_time = true;
    private int windowSize = 10;
    private Queue<Vector2> positionBuffer = new Queue<Vector2>();

    [Tooltip("游標移動靈敏度倍率；手伸到底仍碰不到邊緣就調大")]
    public float reachGain = 1f;
    [Tooltip("肩寬基準的平滑係數(0~1)，越大越快跟上尺度變化")]
    public float shoulderSmoothing = 0.1f;

    private void Start()
    {
        if (videoPoseTest == null)
        {
            Debug.LogError("ScriptA component not found!");
        }

        AddPointerOutline();

        screenWidth = 1024;//Screen.width;
        screenHeight = 600; //Screen.height;
        zeropos_x = -1 * screenWidth / 2;
    }

    private void Update()
    {
        string newstring = videoPoseTest.videoPoseData;

        // 還沒有骨架（首頁、校準前）：直接置中，不進解析（否則每幀丟一次例外，浪費 CPU）
        if (string.IsNullOrEmpty(newstring))
        {
            smoothedPos = Vector2.zero;
            pointer.localPosition = smoothedPos;
            return;
        }

        try
        {
            List<Vector3> vectors = ParseStringToVector3List(newstring);

            // 肩寬每幀更新（平滑）。原本只在第一幀抓一次，VideoPose 追蹤器 Reset(例如進校準關開 A-pose)
            // 後骨架尺度改變，舊肩寬失效 → 游標移動範圍被壓縮、碰不到螢幕右側。
            float shoulder = Vector3.Distance(vectors[13], vectors[17]);
            if (shoulder > 1f)
            {
                distance = first_time ? shoulder : Mathf.Lerp(distance, shoulder, shoulderSmoothing);
                first_time = false;
            }
            if (distance <= 1f) return; // 尚未取得有效肩寬，維持原位置

            curX = (int)(zeropos_x + ((vectors[15].x - vectors[17].x) / distance * reachGain * screenWidth / 2));
            curY = (int)((vectors[15].y - vectors[17].y) / distance * reachGain * screenHeight / 2);

            curX = Mathf.Clamp(curX, screenWidth / 2 * -1, screenWidth / 2);

            curY = Mathf.Clamp(curY, screenHeight / 2 * -1, screenHeight / 2);
            curPos.x = curX;
            curPos.y = curY;

            positionBuffer.Enqueue(curPos);

            if (positionBuffer.Count > windowSize)
            {
                positionBuffer.Dequeue();
            }

            smoothedPos = positionBuffer.Aggregate(Vector2.zero, (sum, next) => sum + next) / positionBuffer.Count;
        }
        catch (System.Exception ex)
        {
            smoothedPos.x = 0;
            smoothedPos.y = 0;
        }

        pointer.localPosition = smoothedPos;
    }

    /// <summary>
    /// 游標外圍的藍色細描邊（另一層，停在按鈕上時只有手掌變色、描邊維持藍色做對比）。
    /// 圖檔：Resources/pointer_outline.png，與 pointer.png 同尺寸、只含手掌外圍一圈。
    /// </summary>
    private void AddPointerOutline()
    {
        if (pointer == null || pointer.Find("PointerOutline") != null) return;
        var tex = Resources.Load<Texture2D>("pointer_outline");
        if (tex == null) return;
        var go = new GameObject("PointerOutline", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(pointer, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<RawImage>();
        img.texture = tex;
        img.raycastTarget = false;
    }

    List<Vector3> ParseStringToVector3List(string input)
    {
        List<Vector3> result = new List<Vector3>();

        input = input.Trim(new char[] { '[', ']' }).Replace(" ", "");

        string[] tuples = input.Split(new string[] { "),(" }, System.StringSplitOptions.None);

        foreach (string tuple in tuples)
        {
            string[] numbers = tuple.Trim(new char[] { '(', ')' }).Split(',');

            if (numbers.Length == 3)
            {
                float x = float.Parse(numbers[0]) * 1000;
                float y = float.Parse(numbers[1]) * 1000;
                float z = float.Parse(numbers[2]) * 1000;

                result.Add(new Vector3(x, y, z));
            }
        }

        return result;
    }
}
