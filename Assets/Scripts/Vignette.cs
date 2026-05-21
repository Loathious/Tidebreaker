using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cheap UI vignette — adds soft dark corners around the gameplay area for
/// cinematic depth, AND pulses red when the player is on low health.
/// Procedurally generates a radial-gradient texture at startup, so no asset
/// import is required.
/// </summary>
public class Vignette : MonoBehaviour
{
    [Header("Strength")]
    [SerializeField] [Range(0f, 1f)] private float intensity   = 0.45f; // how dark corners get normally
    [SerializeField] [Range(0.05f, 1f)] private float innerRadius = 0.35f;
    [SerializeField] private int textureSize = 256;

    [Header("Low Health")]
    [Tooltip("Health fraction below which the red vignette starts to ramp in (0..1).")]
    [SerializeField, Range(0f, 1f)] private float lowHealthThreshold = 0.4f;
    [SerializeField] private Color lowHealthColor = new Color(0.85f, 0.05f, 0.05f, 1f);
    [Tooltip("Pulse speed at the low-health threshold (calm danger)")]
    [SerializeField] private float pulseSpeed    = 2.4f;
    [Tooltip("Pulse speed when health is near zero (frantic danger)")]
    [SerializeField] private float pulseSpeedMax = 9f;

    private Image  _vignetteImg;
    private Health _playerHealth;
    private Color  _normalColor;

    void Start()
    {
        Canvas canvas = null;
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.renderMode != RenderMode.ScreenSpaceOverlay &&
                c.renderMode != RenderMode.ScreenSpaceCamera) continue;
            if (c.name == "GameCanvas") { canvas = c; break; }
            if (canvas == null) canvas = c;
        }
        if (canvas == null) return;

        // Create the overlay image
        GameObject go = new GameObject("Vignette");
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling(); // draw on top of everything

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _vignetteImg = go.AddComponent<Image>();
        _vignetteImg.raycastTarget = false;
        _vignetteImg.sprite        = BuildVignetteSprite();
        _normalColor               = new Color(0f, 0f, 0f, intensity);
        _vignetteImg.color         = _normalColor;

        // Hook player health for the red low-HP pulse
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            _playerHealth = player.GetComponent<Health>() ?? player.GetComponentInChildren<Health>();
    }

    void Update()
    {
        if (_vignetteImg == null) return;
        if (_playerHealth == null || _playerHealth.MaxHealth <= 0f)
        {
            _vignetteImg.color = _normalColor;
            return;
        }

        float frac = _playerHealth.CurrentHealth / _playerHealth.MaxHealth;

        if (frac >= lowHealthThreshold)
        {
            _vignetteImg.color = _normalColor;
            return;
        }

        // 0 = at threshold (no red), 1 = at zero health (full red)
        float danger = 1f - (frac / lowHealthThreshold);
        // Pulse speed scales from pulseSpeed (calm) to pulseSpeedMax (frantic) as HP drops
        float dynSpeed = Mathf.Lerp(pulseSpeed, pulseSpeedMax, danger);
        float pulse    = 0.6f + 0.4f * (Mathf.Sin(Time.time * dynSpeed) * 0.5f + 0.5f);

        // Blend from neutral dark vignette toward red as danger rises
        Color blended = Color.Lerp(_normalColor, lowHealthColor, danger);
        // Scale alpha up with danger for a stronger red wash
        blended.a = Mathf.Lerp(_normalColor.a, 0.75f, danger) * pulse;

        _vignetteImg.color = blended;
    }

    /// <summary>Builds a soft radial-falloff texture: opaque corners, transparent center.</summary>
    Sprite BuildVignetteSprite()
    {
        Texture2D tex = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;

        Vector2 center = new Vector2(textureSize * 0.5f, textureSize * 0.5f);
        float maxDist = textureSize * 0.5f;

        Color[] pixels = new Color[textureSize * textureSize];
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                // Smooth falloff from innerRadius (0 alpha) to 1.0 (full alpha)
                float a = Mathf.SmoothStep(0f, 1f,
                            Mathf.InverseLerp(innerRadius, 1f, dist));
                pixels[y * textureSize + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex,
                             new Rect(0, 0, textureSize, textureSize),
                             new Vector2(0.5f, 0.5f),
                             100f);
    }
}
