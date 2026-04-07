using System;
using System.Runtime.InteropServices;
 
[Serializable]
public unsafe struct DevicesInfo
{
	[Serializable]
	public struct DeviceInfo
	{
		public fixed byte id[256];
		public fixed byte name[256];
		public int isDefault;
		private int pad1;
		private fixed int pad2[256];
		//[Serializable]
		//public struct NativeDataFormat {
		//	public int format;
		//	public int channels;
		//	public int sampleRate;
		//	public int flags;
		//};
		//public ReadOnlySpan<NativeDataFormat> nativeDataFormats
		//{
		//	get
		//	{
		//		fixed (int* p = pad2)
		//		{
		//			var buf = MemoryMarshal.Cast<int, byte>(new System.ReadOnlySpan<int>(p, 256));
		//			return MemoryMarshal.Cast<byte, NativeDataFormat>(buf);
		//		}
		//	}
		//}
		public string getName()
		{
			fixed (byte* n = name)
			{
				return Marshal.PtrToStringUTF8((IntPtr)n);
			}
		}
	}
	public Span<DeviceInfo> PlaybackDeviceInfos;
	public Span<DeviceInfo> CaptureDeviceInfos;
}

public class MiniAudio
{
	public delegate void AudioDataCallback(Span<short> data);
	public delegate void AudioCallback(short[] data);
	#if UNITY_IOS && !UNITY_EDITOR
		private const string lib = "__Internal";
	#else
		private const string lib = "MiniAudio";
	#endif
	[DllImport(lib)]
	public static extern DevicesInfo GetDeviceInfo();
	[DllImport(lib)]
	private static extern void SetAudioCallback(AudioDataCallback callback);
	[DllImport(lib)]
	public static extern int StartMicrophone(int id, int samplerate, int channels);
	[DllImport(lib)]
	public static extern int StopMicrophone();
	private static AudioCallback callback=null;
	[AOT.MonoPInvokeCallback(typeof(AudioCallback))]
	private static void OnAudioData(Span<short> data)
	{
		callback?.Invoke(data.ToArray());
	}
	public static void SetAudioCallback(AudioCallback callback)
	{
		MiniAudio.callback = callback;
		SetAudioCallback(OnAudioData);
	}
}
