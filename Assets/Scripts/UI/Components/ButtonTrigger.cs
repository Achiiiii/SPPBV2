using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class ButtonTrigger : MonoBehaviour
{
    Button btn;
    Transform transform;
    RectTransform rect;
    public AudioSource audioSource;

    void Start()
    {
        rect = gameObject.GetComponent<RectTransform>();
        btn = gameObject.GetComponent<Button>();
        transform = gameObject.GetComponent<Transform>();
        BoxCollider2D boxCollider = gameObject.AddComponent<BoxCollider2D>();
        float pivotX = rect.pivot.x;
        float pivotY = rect.pivot.y;
        float offsetX = 0;
        float offsetY = 0;
        switch (pivotX)
        {
            case 0:
                offsetX = rect.sizeDelta.x / 2;
                break;
            case 0.5f:
                offsetX = 0;
                break;
            case 1:
                offsetX = rect.sizeDelta.x / 2 * -1;
                break;
        }
        switch (pivotY)
        {
            case 0:
                offsetY = rect.sizeDelta.y / 2;
                break;
            case 0.5f:
                offsetY = 0;
                break;
            case 1:
                offsetY = rect.sizeDelta.y / 2 * -1;
                break;
        }
        boxCollider.offset = new Vector2(offsetX, offsetY);
        boxCollider.isTrigger = true;
        boxCollider.size = new Vector2(rect.sizeDelta.x, rect.sizeDelta.y);
        btn.onClick.AddListener(ClickAudio);
    }
    [Tooltip("游標需停留在按鈕上多久才按下（秒）")]
    public float dwellSeconds = 2f;
    [Tooltip("停留期間按鈕放大到的倍率")]
    public float hoverScale = 1.1f;

    private bool _hovering;
    private bool _fired;
    private float _dwellTimer;
    private DwellRing _ring;
    private Graphic _pointerGraphic;   // 游標圖（停在按鈕上時變色，讓使用者知道「選到了」）
    // 游標可能同時碰到兩個按鈕：用共用計數，全部離開才還原顏色
    private static int s_hoverCount;
    private static Color s_pointerBaseColor = Color.white;
    [Tooltip("游標停在按鈕上時的顏色")]
    public Color pointerHoverColor = new Color(1f, 0.72f, 0.2f, 1f);

    // 游標碰到按鈕：開始計時並在游標上顯示進度圈；滿 dwellSeconds 才按下，移開就歸零（避免手經過時誤觸）
    private void OnTriggerEnter2D(Collider2D other)
    {
        _hovering = true;
        _fired = false;
        _dwellTimer = 0f;
        _ring = DwellRing.For(other.transform);
        _ring.Show(0f);
        if (_pointerGraphic == null)
        {
            _pointerGraphic = other.GetComponent<Graphic>();
            if (_pointerGraphic != null)
            {
                if (s_hoverCount++ == 0) s_pointerBaseColor = _pointerGraphic.color;
                _pointerGraphic.color = pointerHoverColor;
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        ResetDwell();
    }

    private void Update()
    {
        if (!_hovering || _fired) return;
        if (btn != null && !btn.interactable) { ResetDwell(); return; }

        _dwellTimer += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(_dwellTimer / Mathf.Max(0.1f, dwellSeconds));
        if (_ring != null) _ring.Show(t);
        transform.localScale = Vector3.one * Mathf.Lerp(1f, hoverScale, t);

        if (t >= 1f)
        {
            _fired = true;   // 同一次停留只觸發一次，需移開再回來才會再按
            transform.localScale = Vector3.one;
            if (_ring != null) _ring.Hide();
            btn.onClick.Invoke();
        }
    }

    private void OnDisable()
    {
        ResetDwell();   // 換頁時按鈕被隱藏，可能收不到 TriggerExit
    }

    private void ResetDwell()
    {
        _hovering = false;
        _dwellTimer = 0f;
        if (transform != null) transform.localScale = Vector3.one;
        if (_ring != null) _ring.Hide();
        if (_pointerGraphic != null)
        {
            if (--s_hoverCount <= 0) { s_hoverCount = 0; _pointerGraphic.color = s_pointerBaseColor; }
            _pointerGraphic = null;
        }
    }
    private void ClickAudio()
    {
        if (audioSource != null)
            audioSource.Play();
    }
}
