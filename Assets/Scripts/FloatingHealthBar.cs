using UnityEngine;

/// <summary>
/// Small world-space health bar that floats above an enemy. Built procedurally
/// from two sprites (dark backing + coloured fill) — no prefab required.
/// Hides itself while the enemy is at full health and after death.
/// </summary>
[RequireComponent(typeof(Health))]
public class FloatingHealthBar : MonoBehaviour
{
    [Header("Layout")]
    public float width      = 1.1f;
    public float height     = 0.16f;
    public float yOffset    = 0.85f;
    public Color fillColor  = new Color(0.85f, 0.2f, 0.2f);
    public int   sortingOrder = 200;

    private Health         _health;
    private Transform      _root;
    private Transform      _fill;
    private SpriteRenderer _bgSr;
    private SpriteRenderer _fillSr;
    private float          _ratio = 1f;
    private bool           _everDamaged;

    void Start()
    {
        _health = GetComponent<Health>();
        Build();
        if (_health != null)
        {
            _health.OnHealthChanged.AddListener(OnHealthChanged);
            _health.OnDeath.AddListener(() => { if (_root != null) _root.gameObject.SetActive(false); });
        }
        SetVisible(false);
    }

    private void Build()
    {
        _root = new GameObject("HealthBar").transform;
        _root.SetParent(transform, false);
        _root.localPosition = new Vector3(0f, yOffset, 0f);

        _bgSr = MakeBar(_root, ProceduralSprite.White(), new Color(0.05f, 0.05f, 0.05f, 0.85f),
                        width, height, sortingOrder);

        GameObject fillGO = new GameObject("Fill");
        _fill = fillGO.transform;
        _fill.SetParent(_root, false);
        _fillSr = fillGO.AddComponent<SpriteRenderer>();
        _fillSr.sprite       = ProceduralSprite.White();
        _fillSr.color        = fillColor;
        _fillSr.sortingOrder = sortingOrder + 1;
        // Anchor fill to the left so it shrinks toward the left edge
        _fill.localScale    = new Vector3(width - 0.04f, height - 0.04f, 1f);
        _fill.localPosition = Vector3.zero;
    }

    private static SpriteRenderer MakeBar(Transform parent, Sprite spr, Color col,
                                          float w, float h, int order)
    {
        GameObject go = new GameObject("BG");
        go.transform.SetParent(parent, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = spr;
        sr.color        = col;
        sr.sortingOrder = order;
        go.transform.localScale = new Vector3(w, h, 1f);
        return sr;
    }

    private void OnHealthChanged(float current, float max)
    {
        _ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        if (_ratio < 0.999f) _everDamaged = true;
        SetVisible(_everDamaged && current > 0f);

        if (_fill != null)
        {
            float fullW = width - 0.04f;
            float w     = fullW * _ratio;
            _fill.localScale    = new Vector3(w, height - 0.04f, 1f);
            // keep left edge fixed
            _fill.localPosition = new Vector3(-(fullW - w) * 0.5f, 0f, 0f);
        }
        if (_fillSr != null)
            _fillSr.color = Color.Lerp(new Color(0.9f, 0.1f, 0.1f), fillColor, _ratio);
    }

    private void SetVisible(bool v)
    {
        if (_root != null) _root.gameObject.SetActive(v);
    }

    void LateUpdate()
    {
        // Counter the parent's flip so the bar never appears mirrored
        if (_root == null) return;
        Vector3 ls = transform.lossyScale;
        float sx = Mathf.Abs(ls.x) < 0.0001f ? 1f : 1f / ls.x;
        float sy = Mathf.Abs(ls.y) < 0.0001f ? 1f : 1f / ls.y;
        _root.localScale = new Vector3(sx, sy, 1f);
    }
}
