using SPPB.Utils;
using UnityEngine;
using UnityEngine.Events;



namespace SPPB.Core
{
    public class NuwaManager : Singleton<NuwaManager>
    {
        private NuwaEventTrigger _trigger;
        void Start()
        {
            _trigger = GetComponent<NuwaEventTrigger>();

        }

        public void NuwaTTS(string text, UnityAction<bool> callback = null)
        {
            if (text == "")
                return;
            StopNuwaTTS();
            if (callback != null)
            {
                // Wrap callback to ensure it runs on Unity main thread.
                // NuwaEventTrigger.onTTSComplete fires from Android Java thread,
                // which causes "Graphics device is null" if we directly touch UI/Graphics.
                _trigger.onTTSComplete.AddListener((value) =>
                {
                    UnityMainThreadDispatcher.Instance.Enqueue(() => callback(value));
                });
            }
            if (Lang.IsEn) SpeakWithLocale(text, englishLocale);
            else Nuwa.startTTS(text);
        }

        [Tooltip("英文語音的語系代碼（傳給 NuwaRobotAPI.startTTS(text, locale)）")]
        public string englishLocale = "en_US";

        /// <summary>
        /// 指定語系念出文字。Unity 外掛只包了 startTTS(text)（系統預設中文），
        /// 這裡直接取外掛裡的 NuwaRobotAPI 實例（com.u2a.sdk.NuwaPlugin.mRobot）呼叫 startTTS(text, locale)。
        /// 失敗時退回預設的 startTTS。
        /// </summary>
        private void SpeakWithLocale(string text, string locale)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var plugin = new AndroidJavaClass("com.u2a.sdk.NuwaPlugin"))
                using (var robot = plugin.GetStatic<AndroidJavaObject>("mRobot"))
                {
                    if (robot != null)
                    {
                        robot.Call("startTTS", text, locale);
                        return;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[NuwaManager] 指定語系 TTS 失敗，改用預設：{e.Message}");
            }
#endif
            Nuwa.startTTS(text);
        }

        public void StopNuwaTTS()
        {
            if (_trigger == null)
                return;
            _trigger.onTTSComplete.RemoveAllListeners();
            Nuwa.stopTTS();
        }
    }
}
