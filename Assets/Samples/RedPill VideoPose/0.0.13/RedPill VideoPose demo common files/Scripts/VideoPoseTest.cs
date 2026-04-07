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
	public bool detectAPose = false;
	public string videoPoseData;
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
		inputManager.OnNewFrame += (ImageBuffer buffer) =>
		{
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
	private void OnPose(Pose pose)
	{
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
