using System.Collections;
using UnityEngine;

/// <summary>
/// Spawns a white "+" hit marker at a fixed world position that fades out over 0.7 seconds.
/// Uses SpriteRenderer so it is guaranteed to be visible in URP 2D.
/// Call HitSpark.Spawn(worldPosition) — no prefab or asset required.
/// </summary>
public class HitSpark : MonoBehaviour
{
    private const float Duration  = 0.7f;
    private const float DriftY    = 0.2f;

    // Half-extents of each arm of the "+" in world units
    private const float ArmLength    = 0.10f;
    private const float ArmThickness = 0.025f;

    /// <summary>Spawns a hit marker at the given world position.</summary>
    public static void Spawn(Vector2 position)
    {
        GameObject go = new GameObject("HitMarker");
        go.transform.position = new Vector3(position.x, position.y, 0f);
        go.AddComponent<HitSpark>().Initialize();
    }

    void Initialize()
    {
        // Horizontal arm
        MakeBar(new Vector3(ArmLength * 2f, ArmThickness, 1f));
        // Vertical arm
        MakeBar(new Vector3(ArmThickness, ArmLength * 2f, 1f));

        StartCoroutine(Fade());
    }

    void MakeBar(Vector3 scale)
    {
        GameObject bar = new GameObject("Arm");
        bar.transform.SetParent(transform, false);
        bar.transform.localScale = scale;

        SpriteRenderer sr = bar.AddComponent<SpriteRenderer>();
        sr.sprite       = WhiteSprite();
        sr.color        = Color.white;
        sr.sortingOrder = 300;
    }

    IEnumerator Fade()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        Vector3 origin = transform.position;
        float elapsed  = 0f;

        while (elapsed < Duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / Duration;

            transform.position = origin + Vector3.up * (DriftY * t);

            Color c = new Color(1f, 1f, 1f, 1f - t);
            foreach (SpriteRenderer r in renderers) r.color = c;

            yield return null;
        }

        Destroy(gameObject);
    }

    // ---- shared 1×1 white sprite (allocated once, never leaked) ----
    private static Sprite _cachedSprite;
    static Sprite WhiteSprite()
    {
        if (_cachedSprite != null) return _cachedSprite;

        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point
        };
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();

        _cachedSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return _cachedSprite;
    }
}
