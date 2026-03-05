using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Spawns a small animated hit marker at a fixed world position that fades out over 1 second.
/// Call HitSpark.Spawn(worldPosition) — no prefab required.
/// </summary>
public class HitSpark : MonoBehaviour
{
    private const float FadeDuration  = 0.9f;
    private const float DriftSpeed    = 0.3f;   // slow upward drift
    private const float StartFontSize = 1.6f;

    /// <summary>Spawns a hit marker at the given world position.</summary>
    public static void Spawn(Vector2 position)
    {
        GameObject go = new GameObject("HitMarker");
        go.transform.position = new Vector3(position.x, position.y, 0f);
        go.AddComponent<HitSpark>().Initialize();
    }

    void Initialize()
    {
        // World-space TextMeshPro — visible without a Canvas
        TextMeshPro tmp = gameObject.AddComponent<TextMeshPro>();
        tmp.text      = "✦";
        tmp.fontSize  = StartFontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;

        // Draw on top of sprites
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr) mr.sortingOrder = 200;

        StartCoroutine(Animate(tmp));
    }

    IEnumerator Animate(TextMeshPro tmp)
    {
        Vector3 origin = transform.position;
        float elapsed  = 0f;

        while (elapsed < FadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / FadeDuration;

            // Fixed world position + slight drift upward
            transform.position = origin + Vector3.up * (DriftSpeed * t);

            // Fade alpha from 1 → 0, shrink slightly
            tmp.color    = new Color(1f, 1f, 1f, 1f - t);
            tmp.fontSize = Mathf.Lerp(StartFontSize, StartFontSize * 0.6f, t);

            yield return null;
        }

        Destroy(gameObject);
    }
}

