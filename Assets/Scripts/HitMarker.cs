using System.Collections;
using UnityEngine;

/// <summary>
/// Spawns a white "+" hit marker that sticks to a target transform and fades out.
/// Procedurally built from two SpriteRenderer quads — no assets required.
/// </summary>
public class HitMarker : MonoBehaviour
{
    private const float Duration      = 0.7f;
    private const float ArmHalfLength = 0.18f;  // world-units, half-length of each arm
    private const float ArmHalfWidth  = 0.04f;  // world-units, half-width of each arm
    private const float PunchScale    = 1.8f;

    private Transform _target;
    private Vector3   _localOffset;

    /// <summary>Spawns a hit marker at worldPosition that follows target.</summary>
    public static void Spawn(Vector3 worldPosition, Transform target)
    {
        GameObject root         = new GameObject("HitMarker");
        root.transform.position = worldPosition;

        HitMarker hm    = root.AddComponent<HitMarker>();
        hm._target      = target;
        // Store offset in target local-space so the marker tracks correctly as the zombie moves.
        // worldPosition is bounds.center, which accounts for the collider's offset from the transform.
        hm._localOffset = target != null ? target.InverseTransformPoint(worldPosition) : Vector3.zero;
        hm.Init();
    }

    void Init()
    {
        // Horizontal arm
        CreateArm(new Vector3(ArmHalfLength * 2f, ArmHalfWidth * 2f, 1f));
        // Vertical arm
        CreateArm(new Vector3(ArmHalfWidth * 2f, ArmHalfLength * 2f, 1f));
        StartCoroutine(Animate());
    }

    void CreateArm(Vector3 worldSize)
    {
        GameObject arm = new GameObject("Arm");
        arm.transform.SetParent(transform, false);

        // Build a 1-pixel white texture at PPU=100 so localScale == world size directly
        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        Sprite spr = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);

        SpriteRenderer sr = arm.AddComponent<SpriteRenderer>();
        sr.sprite       = spr;
        sr.color        = new Color(1f, 1f, 1f, 0.5f); // 50% opacity sword slash
        sr.sortingOrder = 9999;

        // With PPU=100, 1 pixel = 0.01 world units, so sprite is 0.01×0.01 world units.
        // Scale it up to the desired world size.
        arm.transform.localScale = worldSize * 100f;
    }

    IEnumerator Animate()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        float elapsed = 0f;

        while (elapsed < Duration)
        {
            elapsed += Time.unscaledDeltaTime;

            if (_target != null)
                transform.position = _target.TransformPoint(_localOffset);

            // Punch in, then shrink out
            float scale;
            if (elapsed < 0.1f)
                scale = Mathf.Lerp(PunchScale, 1f, elapsed / 0.1f);
            else
                scale = Mathf.Lerp(1f, 0.4f, (elapsed - 0.1f) / (Duration - 0.1f));

            transform.localScale = Vector3.one * scale;

            // Stay at 50% opacity then fade out
            float t        = elapsed / Duration;
            float maxAlpha = 0.5f;
            float alpha    = t < 0.25f ? maxAlpha : Mathf.Lerp(maxAlpha, 0f, (t - 0.25f) / 0.75f);
            Color c        = new Color(1f, 1f, 1f, alpha);
            foreach (SpriteRenderer r in renderers) r.color = c;

            yield return null;
        }

        Destroy(gameObject);
    }
}
