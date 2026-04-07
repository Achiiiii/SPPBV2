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
