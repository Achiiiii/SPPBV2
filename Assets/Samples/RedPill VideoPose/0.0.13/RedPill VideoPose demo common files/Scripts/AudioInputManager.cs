using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class AudioInputManager : MonoBehaviour
{
	public int id=0;
	public delegate void OnAudioDelegate(short[] data);
	public event OnAudioDelegate OnAudio;

	IEnumerator Start()
	{
		yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
		var deviceInfo = MiniAudio.GetDeviceInfo().CaptureDeviceInfos.ToArray();
		MiniAudio.SetAudioCallback(OnAudioCallback);
		var res = MiniAudio.StartMicrophone(id, 16000, 1);
		Debug.Log($"StartMicrophone {id}({deviceInfo[id].getName()}): {res==0}");
	}
	private void OnAudioCallback(short[] data)
	{
		OnAudio?.Invoke(data);
	}

	void OnDestroy()
	{
		MiniAudio.StopMicrophone();
	}
}
