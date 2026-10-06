using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游標上的停留進度圈（體感按鈕用）。掛在游標(pointer)物件上，第一次使用時自動建立。
/// 底圈半透明 + 前景圈依進度順時針填滿。
/// </summary>
public class DwellRing : MonoBehaviour
{
    public float size = 60f;
    public float thickness = 0.18f;   // 圈寬佔半徑比例
    public Color fillColor = new Color(40f / 255f, 120f / 255f, 1f, 1f);   // 與游標手掌外框同藍色
    public Color backColor = new Color(1f, 1f, 1f, 0.25f);

    private Image _back, _fill;
    private static Sprite _ringSprite;

    public static DwellRing For(Transform pointer)
    {
        var ring = pointer.GetComponent<DwellRing>();
        return ring != null ? ring : pointer.gameObject.AddComponent<DwellRing>();
    }

    /// <summary>顯示進度（0~1）。</summary>
    public void Show(float progress)
    {
        if (_fill == null) Build();
        _back.gameObject.SetActive(true);
        _fill.gameObject.SetActive(true);
        _fill.fillAmount = Mathf.Clamp01(progress);
    }

    public void Hide()
    {
        if (_fill == null) return;
        _back.gameObject.SetActive(false);
        _fill.gameObject.SetActive(false);
        _fill.fillAmount = 0f;
    }

    private void Build()
    {
        if (_ringSprite == null) _ringSprite = CreateRingSprite(thickness);
        _back = CreatePart("DwellRingBack", backColor, false);
        _fill = CreatePart("DwellRingFill", fillColor, true);
    }

    private Image CreatePart(string name, Color color, bool filled)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.sprite = _ringSprite;
        img.color = color;
        img.raycastTarget = false;
        if (filled)
        {
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillOrigin = (int)Image.Origin360.Top;
            img.fillClockwise = true;
            img.fillAmount = 0f;
        }
        go.SetActive(false);
        return img;
    }

    /// <summary>程式產生的圓環貼圖（不需額外圖檔）。</summary>
    private static Sprite CreateRingSprite(float thickness)
    {
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        float outer = n * 0.5f - 1f, inner = outer * (1f - thickness), c = n * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                float a = Mathf.Clamp01(outer - d) * Mathf.Clamp01(d - inner);   // 內外邊緣反鋸齒
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
    }
}
