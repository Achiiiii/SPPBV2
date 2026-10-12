using System;
using System.Linq;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using SPPB.Core;

public struct ImageBuffer
{
	public ulong id;
	public Color32[] data;
	public int width;
	public int height;
	public ImageBuffer(ulong id, Color32[] data, int width, int height)
	{
		this.id = id;
		this.data = data;
		this.width = width;
		this.height = height;
	}
}

public delegate void OnNewFrame(ImageBuffer buffer);

class TransformTexture
{
	private Material transform_material;
	public CustomRenderTexture rt { get; private set; }
	private Texture2D reader;
	public int width { get; private set; }
	public int height { get; private set; }

	public TransformTexture(Texture source, int degree, Vector2 flip = new Vector2()) : this(source, getMatrix(degree, flip)) { }
	/// <summary>轉換矩陣 (row0 = x,y ; row1 = z,w)：輸出影像 UV → 來源(相機)UV = M*(uv-0.5)+0.5。</summary>
	public Vector4 matrix { get; private set; }

	/// <summary>把「輸出影像 UV」(= 送給 VideoPose 的影像，u=x/W、v=y/H) 換算成來源相機 UV。</summary>
	public Vector2 ToSourceUV(Vector2 uv)
	{
		Vector2 p = uv - new Vector2(0.5f, 0.5f);
		return new Vector2(matrix.x * p.x + matrix.y * p.y, matrix.z * p.x + matrix.w * p.y) + new Vector2(0.5f, 0.5f);
	}

	TransformTexture(Texture source, Vector4 transform)
	{
		matrix = transform;
		(width, height) = transform[0] != 0 ? (source.width, source.height) : (source.height, source.width);

		// if(transform == new Vector4(1, 0, 0, 1))
		// {
		// 	rt = new CustomRenderTexture(width, height, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
		// 	{
		// 		name = "TransformTexture",
		// 		depth = 0,
		// 		initializationMode = CustomRenderTextureUpdateMode.Realtime,
		// 		initializationTexture = source
		// 	};
		// 	reader = new Texture2D(width, height, TextureFormat.ARGB32, false);	
		// 	return;
		// }

		/*
		transform_material = Resources.Load<Material>("Materials/Transform");
		transform_material.mainTexture = source;
		transform_material.SetVector("_Transform", transform);
		rt = new CustomRenderTexture(width, height, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
		{
			name = "TransformTexture",
			depth = 0,
			initializationMaterial = transform_material,
			initializationMode = CustomRenderTextureUpdateMode.Realtime,
			initializationSource = CustomRenderTextureInitializationSource.Material,
		};
		/*/
		transform_material = Resources.Load<Material>("Materials/CRTTransform");
		transform_material.mainTexture = source;
		transform_material.SetVector("_Transform", transform);
		rt = new CustomRenderTexture(width, height, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
		{
			name = "TransformTexture",
			depth = 0,
			material = transform_material,
			updateMode = CustomRenderTextureUpdateMode.Realtime,
			// initializationTexture = source,
			// initializationMode = CustomRenderTextureUpdateMode.OnLoad,
			// initializationSource = CustomRenderTextureInitializationSource.TextureAndColor,
		};//*/
		reader = new Texture2D(width, height, TextureFormat.ARGB32, false);
	}
	public Color32[] GetPixels32()
	{
		// unsafe{
		// 	var request = AsyncGPUReadback.Request(rt);
		// 	request.WaitForCompletion();
		// 	return request.GetData<Color32>().ToArray();
		// }
		RenderTexture.active = rt;
		reader.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
		return reader.GetPixels32();
	}

	private static Vector4 degree2Matrix(int degrees)
	{
		double rad = degrees / 180.0 * Math.PI;
		float c = (float)Math.Round(Math.Cos(rad)), s = (float)Math.Round(Math.Sin(rad));
		return new Vector4(c, s, -s, c);
	}
	private static Vector4 getMatrix(int degrees, Vector2 flip)
	{
		Vector4 flip_matrix = new Vector4(flip.x, flip.y, flip.x, flip.y);
		Vector4 matrix = degree2Matrix(degrees);
		return Vector4.Scale(matrix, flip_matrix);
	}
}

abstract class IInput
{
	public event OnNewFrame OnNewFrameEvent;
	protected void OnNewFrame(ImageBuffer ibuf)
	{
		OnNewFrameEvent?.Invoke(ibuf);
	}
	protected TransformTexture transformed;
	public Texture texture => transformed.rt;
	public Vector2 flip;
	public virtual IEnumerator Start() { yield return null; }
	public virtual void Update() { }
	public virtual void Stop() { }
	/// <summary>對焦目標：ImageBuffer 像素座標（與 VideoPose keyPoints 同座標）；null = 沒有目標。</summary>
	public Func<Vector2?> focusTarget;
	public bool flipFocusY;
	public virtual bool IsFocusPointSupported => false;
	public virtual void TickFocus() { }
	public virtual bool RequestFocus(string reason) => false;
	public virtual bool IsFocusing => false;
	public virtual void ResetFocusBaseline() { }
	public virtual void OnPause(bool paused) { }
}

class CameraInput : IInput
{
	private int id;
	private Vector2 res;
	private bool front, wide;
	private WebCamTexture webcam = null;
	private bool _focusSupported;
	private CameraFocusAssist focus;   // 唯一會寫 autoFocusPoint 的地方

	public override bool IsFocusPointSupported => webcam != null && _focusSupported;

	public override void TickFocus() => focus?.Tick();

	/// focusTarget（ImageBuffer 像素座標）→ autoFocusPoint 座標。
	/// WebCamTexture 的 uv 跟 autoFocusPoint 是同一套座標（感光元件方向、原點左下），所以只要反推 TransformTexture。
	Vector2? FocusTargetUV()
	{
		var p = focusTarget?.Invoke();
		if (!p.HasValue || transformed == null) return null;
		var uv = new Vector2(p.Value.x / transformed.width, p.Value.y / transformed.height);
		if (flipFocusY) uv.y = 1f - uv.y;
		return transformed.ToSourceUV(uv);
	}

	public override bool RequestFocus(string reason)
	{
		var uv = FocusTargetUV();
		if (focus == null || !uv.HasValue) return false;
		focus.FocusAt(uv.Value, reason);
		return true;
	}

	public override bool IsFocusing => focus != null && focus.IsScanning;
	public override void ResetFocusBaseline() => focus?.ResetBaseline();

	public override void OnPause(bool paused)
	{
		if (paused) focus?.OnPause();
	}

	public CameraInput(Vector2 res, bool front = true, bool wide = true)
	{
		this.front = front;
		this.wide = wide;
		this.res = res;
		id = -1;
	}
	public CameraInput(int id)
	{
		this.id = id;
	}
	public override IEnumerator Start()
	{
		yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
#if UNITY_ANDROID && !UNITY_EDITOR
		// Android：RequestUserAuthorization 不會等使用者回應。全新安裝時權限對話框還沒按就去抓相機，
		// devices 會是空的而丟例外、相機起不來(需重開 App)。改為等到使用者允許相機權限。
		if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
		{
			UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
			float waited = 0f;
			while (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
			{
				yield return new WaitForSeconds(0.5f);
				waited += 0.5f;
				if (waited >= 5f)   // 使用者拒絕或關掉對話框：每 5 秒再詢問一次
				{
					waited = 0f;
					Debug.LogWarning("CameraInput: 尚未取得相機權限，重新詢問");
					UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
				}
			}
		}
#endif
		// 權限剛允許時相機清單可能還是空的，等到有相機再繼續
		while (WebCamTexture.devices.Length == 0)
			yield return new WaitForSeconds(0.5f);
		var deviceName = WebCamTexture.devices.Last().name;
		if (id < 0)
		{
			var devices = WebCamTexture.devices.Where(device => device.kind == WebCamKind.UltraWideAngle || device.kind == WebCamKind.WideAngle);
			devices = front ?
				devices.OrderByDescending(device => device.isFrontFacing) :
				devices.OrderBy(device => device.isFrontFacing);
			devices = wide ?
				((IOrderedEnumerable<WebCamDevice>)devices).ThenByDescending(device => device.kind) :
				((IOrderedEnumerable<WebCamDevice>)devices).ThenBy(device => device.kind);
			var devicesArray = devices.ToArray();
			deviceName = devicesArray.First().name;
		}
		else
		{
			if (id >= WebCamTexture.devices.Length)
			{
				id = WebCamTexture.devices.Length - 1;
				Debug.LogError($"CameraInput: invalid camera id, fallback to {id}");
			}
			deviceName = WebCamTexture.devices[id].name;
		}
		_focusSupported = WebCamTexture.devices.Any(d => d.name == deviceName && d.isAutoFocusPointSupported);
		// 不設 autoFocusPoint：保持 null 才是連續自動對焦（設了就變單次對焦，之後不再追焦）
		webcam = new WebCamTexture(deviceName, (int)res.x, (int)res.y, 30)
		{
			name = "WebCamTexture",
			filterMode = FilterMode.Point,
		};
		webcam.Play();
		while (webcam.width == 16 && webcam.height == 16)
			yield return null;
		Debug.Log($"WebCamTexture: {deviceName}, {webcam.width}x{webcam.height}, 支援指定對焦點={_focusSupported}, 旋轉={webcam.videoRotationAngle}, 垂直鏡像={webcam.videoVerticallyMirrored}");
		// #if UNITY_ANDROID && !UNITY_EDITOR
		// 	transformed = new TransformTexture(webcam, (webcam.videoRotationAngle+180)%360, new Vector2(-1, webcam.videoVerticallyMirrored ? 1 : -1) * flip);
		// #else
		// 	transformed = new TransformTexture(webcam, webcam.videoRotationAngle, new Vector2(-1, webcam.videoVerticallyMirrored ? 1 : -1) * flip);
		// #endif
		transformed = new TransformTexture(webcam, webcam.videoRotationAngle, new Vector2(-1, webcam.videoVerticallyMirrored ? 1 : -1) * flip);
		focus = new CameraFocusAssist(webcam, FocusTargetUV);
	}
	public override void Update()
	{
		try
		{
			// if (webcam.didUpdateThisFrame)
			var pixels = transformed.GetPixels32();
			// 清晰度取樣要在送出前：訂閱端（SubjectLock）會把受測者以外區域塗黑
			if (webcam.didUpdateThisFrame)
				focus?.OnCameraFrame(pixels, transformed.width, transformed.height);
			OnNewFrame(new ImageBuffer(webcam.updateCount, pixels, transformed.width, transformed.height));
			webcam.IncrementUpdateCount();
			// 這裡不可以設 autoFocusPoint：定期重設會讓鏡頭一直重新掃描（見 CameraFocusAssist）
		}
		catch (NullReferenceException e)
		{
			Debug.Log(e);
		}
	}
	public override void Stop()
	{
		focus?.Dispose();
		focus = null;
		webcam?.Stop();
	}
}

class VideoInput : IInput
{
	private MonoBehaviour monoBehaviour;
	private VideoClip clip;
	private VideoPlayer player;
	public VideoInput(MonoBehaviour monoBehaviour, VideoClip clip)
	{
		this.monoBehaviour = monoBehaviour;
		this.clip = clip;
	}
	public override IEnumerator Start()
	{
		player = monoBehaviour.gameObject.AddComponent<VideoPlayer>();
		player.clip = clip;
		player.targetCamera = null;
		player.isLooping = true;
		player.sendFrameReadyEvents = true;
		player.renderMode = VideoRenderMode.APIOnly;
		player.timeUpdateMode = VideoTimeUpdateMode.DSPTime;
		player.skipOnDrop = false;
		player.SetDirectAudioMute(0, true);
		player.frameReady += (VideoPlayer source, long frameIdx) => OnNewFrame(new ImageBuffer((ulong)frameIdx, transformed.GetPixels32(), (int)player.width, (int)player.height));
		player.Prepare();
		player.Play();
		while (!player.isPrepared)
			yield return null;
		transformed = new TransformTexture(player.texture, 0, new Vector2(1, -1));
	}
	public override void Stop()
	{
		player.Stop();
		MonoBehaviour.Destroy(player);
	}
}

class ImageInput : IInput
{
	public ImageInput(Texture2D image) => transformed = new TransformTexture(image, 0, new Vector2(1, -1));
	public override void Update() => OnNewFrame(new ImageBuffer(0, transformed.GetPixels32(), transformed.width, transformed.height));
}

public class InputManager : MonoBehaviour
{
	public Texture2D image;
	public VideoClip clip;
	public int cameraId = -1;
	public Vector2 requestCameraResolution = new Vector2(640, 480);
	public bool front = true;
	public bool wide = true;
	public Vector2 flip = Vector2.one;
	public RawImage rawImage;
	private IInput input;
	public event OnNewFrame OnNewFrame;

	[Header("自動對焦")]
	[Tooltip("對焦目標換算時把 y 上下翻轉。實機確認對焦區域上下顛倒時才勾（見 Docs/CameraAutoFocus.md 7.3）。開相機時套用")]
	public bool flipFocusY = false;
	/// <summary>畫面中出現人時（或人離開 PersonGoneS 秒後再出現）自動對人身上對焦一次。測驗計時中由 TestPage 關閉。</summary>
	[NonSerialized] public bool focusOnPersonAppear = true;
	/// <summary>對焦目標：回傳 ImageBuffer 像素座標，null = 目前沒有目標。在主執行緒呼叫。</summary>
	public Func<Vector2?> focusTargetProvider;

	const float PersonGoneS = 2f;   // 人消失超過這麼久，下次出現時再對焦一次
	float lastPersonTime = -10f;
	bool personArmed = true;

	void OnEnable() => StartCoroutine(Initialize());
	IEnumerator Initialize()
	{
		IInput input = null;
		if (image != null)
			input = new ImageInput(image);
		else if (clip != null)
			input = new VideoInput(this, clip);
		else if (cameraId < 0)
			input = new CameraInput(requestCameraResolution, front, wide);
		else
			input = new CameraInput(cameraId);
		input.OnNewFrameEvent += (ImageBuffer ibuf) => OnNewFrame?.Invoke(ibuf);
		input.flip = flip;
		input.flipFocusY = flipFocusY;
		input.focusTarget = FocusTargetPixel;
		yield return input.Start();

		if (rawImage != null)
		{
			rawImage.texture = input.texture;
			if (rawImage.gameObject.TryGetComponent<AspectRatioFitter>(out var fitter))
			{
				fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
				fitter.aspectRatio = (float)rawImage.texture.width / rawImage.texture.height;
			}
			rawImage.uvRect = new Rect(0, 1, 1, -1);
		}
		this.input = input;
	}
	void OnDisable()
	{
		input?.Stop();
		input = null;
	}

	public bool IsFocusPointSupported => input != null && input.IsFocusPointSupported;
	/// <summary>對目前的目標對焦一次，約 4 秒後自動切回連續對焦。沒有目標時回傳 false。不要每一幀都呼叫。</summary>
	public bool RequestFocus(string reason) => input != null && input.RequestFocus(reason);
	/// <summary>剛觸發對焦，鏡頭可能還在掃描（畫面可能糊）。</summary>
	public bool IsFocusing => input != null && input.IsFocusing;
	/// <summary>畫面內容大幅改變（頭部轉動）時呼叫，清晰度參考值重新累積。</summary>
	public void ResetFocusBaseline() => input?.ResetFocusBaseline();

	void Update()
	{
		if (input == null) return;
		float now = Time.unscaledTime;
		if (FocusTargetPixel().HasValue)
		{
			// 測驗計時中不因「人出現」設點，但仍當作已看到，測驗結束時不補觸發
			if (personArmed && (!focusOnPersonAppear || RequestFocus("person-appeared"))) personArmed = false;
			lastPersonTime = now;
		}
		else if (now - lastPersonTime > PersonGoneS) personArmed = true;
		input.TickFocus();   // 從 Update 呼叫，不要從 OnRenderObject（每台 Camera 都會叫一次）
	}

	Vector2? FocusTargetPixel() => focusTargetProvider?.Invoke();

	void OnApplicationPause(bool paused) => input?.OnPause(paused);
	void OnRenderObject() => input?.Update();
}
