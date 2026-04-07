using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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

    private void Start()
    {
        if (videoPoseTest == null)
        {
            Debug.LogError("ScriptA component not found!");
        }

        screenWidth = 1024;//Screen.width;
        screenHeight = 600; //Screen.height;
        zeropos_x = -1 * screenWidth / 2;
    }

    private void Update()
    {
        string newstring = videoPoseTest.videoPoseData;

        try
        {
            List<Vector3> vectors = ParseStringToVector3List(newstring);
            if (first_time)
            {
                distance = Vector3.Distance(vectors[13], vectors[17]);
                first_time = false;
            }

            curX = (int)(zeropos_x + ((vectors[15].x - vectors[17].x) / distance * screenWidth / 2));
            curY = (int)((vectors[15].y - vectors[17].y) / distance * screenHeight / 2);

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
