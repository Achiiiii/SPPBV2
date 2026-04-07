using System.Collections;
using UnityEngine;

public class LipSyncTest : MonoBehaviour, IMorphSource
{
	private AudioInputManager audioInputManager;
	private FaceFilter faceFilter = new FaceFilter();
	private int viseme = -1;
	private float[] emo = new float[6];
	// private short[] data;

	IEnumerator Start()
	{
		yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
		Voice2Face.Voice2Face_Init();
		audioInputManager = GetComponent<AudioInputManager>();
		audioInputManager.OnAudio += (short[] data)=>{
			// this.data = data;
			Voice2Face.Voice2Face_Process(data, data.Length, ref viseme, emo);
		};
	}
	public void OnDestroy(){
		Voice2Face.Voice2Face_Deinit();
	}
	public float[] GetMorphs()
	{
		return faceFilter.Run(viseme, emo);
	}
	// public void OnGUI()
	// {
	// 	GUIStyle style = new GUIStyle(GUI.skin.label){fontSize = 80};
	// 	style.normal.textColor = Color.green;
	// 	GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
	// 	if(data!=null)
	// 		GUILayout.Label(data[0].ToString(), style);
	// 	GUILayout.Label(viseme.ToString(), style);
	// 	GUILayout.EndVertical();
	// }
}
