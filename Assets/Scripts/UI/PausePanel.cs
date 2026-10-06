using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SPPB.Core;

namespace SPPB.UI
{
    /// <summary>
    /// Pause Panel - Displays pause options
    /// </summary>
    public class PausePanel : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private GameObject _panel;           // Pause panel
        [SerializeField] private Image _frameImage;           // Frame image
        [SerializeField] private Button _resumeButton;        // Resume button
        [SerializeField] private Button _exitButton;          // Exit button

        private void Awake()
        {
            // Bind button events
            if (_resumeButton != null)
            {
                _resumeButton.onClick.AddListener(OnResumeClicked);
            }

            if (_exitButton != null)
            {
                _exitButton.onClick.AddListener(OnExitClicked);
            }

            ApplyYesNoLabels();

            // Hide panel on initialization
            Hide();
        }

        /// <summary>
        /// 選項改成文字「是／否」：原本的兩個圖示按鈕圖片大小不一、排版不齊 → 隱藏，改用程式產生的同尺寸圓角按鈕。
        /// 是＝離開測驗（左，白底深字），否＝繼續測驗（右，橘底白字）。字型沿用面板上的中文字型。
        /// </summary>
        private void ApplyYesNoLabels()
        {
            if (_exitButton == null || _resumeButton == null) return;
            Transform parent = _exitButton.transform.parent;
            var font = _panel != null ? _panel.GetComponentInChildren<TextMeshProUGUI>(true) : null;

            _exitButton.gameObject.SetActive(false);
            _resumeButton.gameObject.SetActive(false);

            // 原本按鈕的垂直位置（錨點在面板左上）；兩顆按鈕在面板水平中線左右對稱
            var parentRt = (RectTransform)parent;
            float y = ((RectTransform)_resumeButton.transform).anchoredPosition.y;
            float cx = parentRt.rect.width > 0f ? parentRt.rect.width * 0.5f : 185.5f;

            var yes = CreateButton(parent, "YesButton", "是", Color.white, new Color(0.2f, 0.2f, 0.2f, 1f), font, new Vector2(cx - 70f, y));
            var no = CreateButton(parent, "NoButton", "否", new Color(1f, 0.42f, 0.1f, 1f), Color.white, font, new Vector2(cx + 70f, y));
            yes.onClick.AddListener(OnExitClicked);
            no.onClick.AddListener(OnResumeClicked);
        }

        private const float ButtonW = 120f, ButtonH = 48f;
        private static Sprite s_roundSprite;

        private static Button CreateButton(Transform parent, string name, string label, Color bg, Color fg, TextMeshProUGUI fontSource, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);   // 與原按鈕相同：錨點在面板左上
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(ButtonW, ButtonH);

            var img = go.GetComponent<Image>();
            img.sprite = s_roundSprite != null ? s_roundSprite : (s_roundSprite = CreateRoundSprite());
            img.type = Image.Type.Sliced;
            img.color = bg;

            go.AddComponent<ButtonTrigger>();   // 手掌游標停留 2 秒按下（與其他按鈕一致）

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            if (fontSource != null) { tmp.font = fontSource.font; tmp.fontSharedMaterial = fontSource.fontSharedMaterial; }
            tmp.text = label;
            tmp.color = fg;
            tmp.fontSize = 26;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return go.GetComponent<Button>();
        }

        /// <summary>程式產生的圓角矩形（9-slice），不需額外圖檔。</summary>
        private static Sprite CreateRoundSprite()
        {
            const int n = 64, r = 24;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(r - (x + 0.5f), (x + 0.5f) - (n - r)));
                    float dy = Mathf.Max(0f, Mathf.Max(r - (y + 0.5f), (y + 0.5f) - (n - r)));
                    float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        }

        // 暫停時 Time.timeScale = 0 → 2D 物理停止，手掌游標碰按鈕的觸發不會發生。
        // 面板開著時改成手動推進 2D 物理（用真實時間），關閉時還原。
        private SimulationMode2D _prevSimMode;
        private bool _manualSim;

        private void Update()
        {
            if (_manualSim) Physics2D.Simulate(Time.unscaledDeltaTime);
        }

        /// <summary>
        /// Show pause panel
        /// </summary>
        public void Show()
        {
            if (_panel != null)
            {
                _panel.SetActive(true);
            }

            // Pause game time
            Time.timeScale = 0f;
            if (!_manualSim)
            {
                _prevSimMode = Physics2D.simulationMode;
                Physics2D.simulationMode = SimulationMode2D.Script;
                _manualSim = true;
            }
        }

        /// <summary>
        /// Hide pause panel
        /// </summary>
        public void Hide()
        {
            if (_panel != null)
            {
                _panel.SetActive(false);
            }

            // Resume game time
            Time.timeScale = 1f;
            if (_manualSim)
            {
                Physics2D.simulationMode = _prevSimMode;
                _manualSim = false;
            }
        }

        /// <summary>
        /// Resume button clicked
        /// </summary>
        private void OnResumeClicked()
        {
            Hide();
        }

        /// <summary>
        /// Exit button clicked
        /// </summary>
        private void OnExitClicked()
        {
            Hide();

            // Return to home page
            if (UIManager.Instance != null)
            {
                UIManager.Instance.GoHome();
            }
        }
    }
}
