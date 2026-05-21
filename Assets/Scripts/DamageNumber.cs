using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Floating damage number spawned at the hit point, drifts upward and fades.
/// Call DamageNumber.Spawn(worldPos, damage) from any hurt handler.
/// </summary>
public class DamageNumber : MonoBehaviour
{
    private TextMeshPro _tmp;
    private const float FloatDist = 1.0f;
    private const float Duration  = 0.72f;

    void Awake()
    {
        _tmp = GetComponent<TextMeshPro>();
        if (_tmp == null) _tmp = gameObject.AddComponent<TextMeshPro>();
        _tmp.alignment    = TextAlignmentOptions.Center;
        _tmp.sortingOrder = 20;
    }

    private void Setup(float damage, bool isCrit)
    {
        _tmp.text     = isCrit ? $"<b>{Mathf.RoundToInt(damage)}!</b>" : Mathf.RoundToInt(damage).ToString();
        _tmp.color    = isCrit ? new Color(1f, 0.9f, 0.1f) : Color.white;
        _tmp.fontSize = isCrit ? 4.5f : 3.2f;
        FontEnforcer.ApplyTo(_tmp);
        StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        Vector3 endPos   = startPos + Vector3.up * FloatDist
                         + Vector3.right * UnityEngine.Random.Range(-0.3f, 0.3f);

        while (elapsed < Duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / Duration;

            // Float up ease-out
            transform.position = Vector3.Lerp(startPos, endPos, 1f - Mathf.Pow(1f - t, 3f));

            // Scale: pop up, hold, slight shrink
            float sc = t < 0.2f ? Mathf.Lerp(0f, 1.25f, t / 0.2f)
                                 : Mathf.Lerp(1.25f, 0.95f, (t - 0.2f) / 0.8f);
            transform.localScale = Vector3.one * sc;

            // Fade out last 30%
            Color c = _tmp.color;
            c.a = t > 0.7f ? 1f - (t - 0.7f) / 0.3f : 1f;
            _tmp.color = c;

            yield return null;
        }
        Destroy(gameObject);
    }

    /// <summary>Legacy API used by pre-existing DamageNumberSpawner components in scenes.</summary>
    public void Initialize(int damage, Vector3 worldPos, Camera cam)
    {
        transform.position = worldPos + Vector3.up * 0.4f;
        Setup(damage, false);
    }

    /// <summary>Spawns a floating damage number at worldPos.</summary>
    public static void Spawn(Vector3 worldPos, float damage, bool isCrit = false)
    {
        if (damage <= 0f) return;
        GameObject go = new GameObject("DmgNum");
        go.transform.position = worldPos + Vector3.up * 0.4f;
        DamageNumber dn = go.AddComponent<DamageNumber>();
        dn.Setup(damage, isCrit);
    }
}
