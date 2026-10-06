using UnityEngine;
using System.Collections.Generic;

public class VideoPoseTest : MonoBehaviour
{
	public InputManager inputManager;
	public VideoPoseAvatar videoPoseAvatar;
	private Touch touch;
	private VideoPose videoPose;
	public bool showGUI = false;
	public bool upperBodyMode = false;
	public bool detectAPose = true;
	public string videoPoseData;
	/// <summary>受測者鎖定（身分比對、區域遮罩、對焦跟隨）。</summary>
	public SPPB.Core.Detection.SubjectLock subject { get; private set; }
	/// <summary>凱比頭部自動追蹤（全身入鏡）。</summary>
	public SPPB.Core.HeadTracking.SppbHeadTracker headTracker { get; private set; }

	// 追蹤開關：進定位校準關才開始把影像餵給 VideoPose。
	// detect_apose 常開 → VideoPose 會「等偵測到 A-pose 才開始吐骨架」，所以校準前不餵影像，
	// 就不會在首頁等畫面提前被判定 A-pose；進校準關重置追蹤器，要求重新做一次 A-pose。
	private volatile bool _trackingEnabled = false;
	public bool trackingEnabled => _trackingEnabled;
	private int _poseCountSinceReset;       // native thread 遞增
	/// <summary>最近一次開始追蹤(重置)後收到的姿勢數。VideoPose 抓到 A-pose 後才會開始增加。</summary>
	public int poseCountSinceReset => _poseCountSinceReset;

	public void Start()
	{
		Screen.sleepTimeout = SleepTimeout.NeverSleep;
		videoPose = new VideoPose();
		bool res = videoPose.Start(OnPose);
		Debug.Log($"VideoPose Start: {res}");
		videoPose.Reset(fovy: 120, detect_apose: detectAPose);
		videoPose.SetUpperBodyMode(upperBodyMode);
		if (inputManager == null)
			inputManager = GetComponent<InputManager>();
		subject = GetComponent<SPPB.Core.Detection.SubjectLock>();
		if (subject == null) subject = gameObject.AddComponent<SPPB.Core.Detection.SubjectLock>();
		subject.Init(this);
		headTracker = GetComponent<SPPB.Core.HeadTracking.SppbHeadTracker>();
		if (headTracker == null) headTracker = gameObject.AddComponent<SPPB.Core.HeadTracking.SppbHeadTracker>();
		headTracker.Init(this);
		inputManager.OnNewFrame += (ImageBuffer buffer) =>
		{
			imageWidth = buffer.width;
			imageHeight = buffer.height;
			if (!_trackingEnabled) return;   // 校準前不餵影像
			// 鎖定受測者：比對身分、把受測者以外區域塗黑、對焦跟隨（主執行緒，須在 PushFrame 前）
			subject.ProcessFrame(buffer.data, buffer.width, buffer.height);
			videoPose.PushFrame(buffer.id, buffer.data, buffer.width, buffer.height, 4);
		};
		inputManager.rawImage.enabled = showGUI;
		if (TryGetComponent(out touch))
		{
			touch.tapped += () =>
			{
				showGUI = !showGUI;
				inputManager.rawImage.enabled = showGUI;
			};
		}
	}
	// --- 人物在場偵測 ---
	// 注意：VideoPose 沒有真人時仍可能把類人物件當骨架持續回傳，所以只看「有沒有回傳」不夠，
	// 還要看 keyPoints 外框大小與信心值（keyPoints 為影像像素座標，z 為 0~1 信心值）。
	private int _poseCounter;               // native thread 遞增
	private int _lastSeenCounter;
	public float timeSinceLastPose { get; private set; }
	public int imageWidth { get; private set; }
	public int imageHeight { get; private set; }
	/// <summary>最近一幀 keyPoints（信心值達標者）外框寬/高（像素）。</summary>
	public float lastBoxWidth { get; private set; }
	public float lastBoxHeight { get; private set; }
	/// <summary>最近一幀 keyPoints 平均信心值。</summary>
	public float lastMeanConfidence { get; private set; }
	public const float KeyPointMinConfidence = 0.3f;

	/// <summary>timeout 秒內有回傳姿勢（不保證是真人，建議搭配 IsValidPerson）。</summary>
	public bool IsPersonPresent(float timeout = 0.5f) => timeSinceLastPose <= timeout;

	/// <summary>有回傳姿勢，且外框高度與平均信心值都達標，才視為真人在畫面中。</summary>
	public bool IsValidPerson(float timeout, float minBoxHeight, float minMeanConfidence)
		=> timeSinceLastPose <= timeout && lastBoxHeight >= minBoxHeight && lastMeanConfidence >= minMeanConfidence;

	void Update()
	{
		if (_poseCounter != _lastSeenCounter) { _lastSeenCounter = _poseCounter; timeSinceLastPose = 0f; }
		else if (timeSinceLastPose < float.MaxValue) timeSinceLastPose += Time.deltaTime;
	}

	private void UpdateKeyPointStats(Pose pose)
	{
		var kps = pose.keyPoints;
		if (kps == null || kps.Length == 0)
		{
			lastBoxWidth = 0f; lastBoxHeight = 0f; lastMeanConfidence = 0f;
			return;
		}
		float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
		float confSum = 0f;
		int used = 0;
		foreach (var k in kps)
		{
			confSum += k.z;
			if (k.z < KeyPointMinConfidence) continue;
			used++;
			if (k.x < minX) minX = k.x;
			if (k.x > maxX) maxX = k.x;
			if (k.y < minY) minY = k.y;
			if (k.y > maxY) maxY = k.y;
		}
		lastMeanConfidence = confSum / kps.Length;
		lastBoxWidth = used >= 2 ? maxX - minX : 0f;
		lastBoxHeight = used >= 2 ? maxY - minY : 0f;
	}

	/// <summary>進定位校準關：清空舊骨架、重置追蹤器(等待 A-pose)、開始餵影像。</summary>
	public void BeginAPoseCalibration()
	{
		_trackingEnabled = false;
		ClearPoseState();
		if (subject != null) subject.ResetLock();
		if (videoPose != null) videoPose.Reset(fovy: 120, detect_apose: detectAPose);
		_trackingEnabled = true;
		Debug.Log("[VideoPose] 開始追蹤，等待 A-pose");
	}

	/// <summary>回首頁：停止餵影像、清空骨架，下一輪到校準關再開始。</summary>
	public void StopTracking()
	{
		_trackingEnabled = false;
		if (videoPose != null) videoPose.Reset(fovy: 120, detect_apose: detectAPose);
		ClearPoseState();
		if (subject != null) subject.ResetLock();
		Debug.Log("[VideoPose] 停止追蹤");
	}

	private void ClearPoseState()
	{
		videoPoseData = "";
		_poseCountSinceReset = 0;
		lastBoxWidth = 0f; lastBoxHeight = 0f; lastMeanConfidence = 0f;
		_lastSeenCounter = _poseCounter;
		timeSinceLastPose = float.MaxValue;
	}

	private void OnPose(Pose pose)
	{
		if (!_trackingEnabled) return;   // 停止追蹤後才送達的舊姿勢一律丟掉
		_poseCounter++;
		_poseCountSinceReset++;
		UpdateKeyPointStats(pose);
		if (subject != null) subject.OnPoseKeypoints(pose.keyPoints);
		videoPoseAvatar.poses.Enqueue(pose);
		var poseJson = JsonUtility.ToJson(pose);

		var parsedPose = JsonUtility.FromJson<Pose>(poseJson);
		List<Vector3> globalTransformsList = new List<Vector3>();
		foreach (var globalTransform in parsedPose.globalTransforms)
		{
			Vector3 position = new Vector3(globalTransform.m03, globalTransform.m13, globalTransform.m23);
			//Debug.Log(position);
			globalTransformsList.Add(position);
		}
		/*for(int i = 0; i < globalTransformsList.Count; i++)
		{
				var globalTransform = globalTransformsList[i];
		}*/

		string tmp = string.Join(", ", globalTransformsList);

		tmp = "[" + tmp + "]";
		videoPoseData = tmp;
	}
}
