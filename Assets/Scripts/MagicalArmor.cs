using System.Collections;
using UnityEngine;

/// <summary>
/// Magical Armor — obtained inside the Desert pyramid (Level 4).
/// From spelmanus: "blocks one attack every 10 seconds".
/// Health.TakeDamage() calls TryAbsorb(); when the armor is ready it eats the
/// hit and goes on cooldown. A blue aura around the player shows when it is
/// charged. Persists between levels via the PlayerPrefs flag "PlayerHasArmor".
/// </summary>
public class MagicalArmor : MonoBehaviour
{
    [SerializeField] private float cooldown = 10f;

    private bool  _ready;
    private float _cooldownTimer;
    private SpriteRenderer _aura;

    public bool IsActive => PlayerPrefs.GetInt("PlayerHasArmor", 0) == 1;
    public bool IsReady  => IsActive && _ready;

    /// <summary>Grants the armor permanently (called by the pyramid reward).</summary>
    public static void Grant()
    {
        PlayerPrefs.SetInt("PlayerHasArmor", 1);
        PlayerPrefs.Save();
    }

    void Start()
    {
        _ready = true;
        BuildAura();
        RefreshAura();
    }

    void Update()
    {
        if (!IsActive) { if (_aura != null) _aura.enabled = false; return; }

        if (!_ready)
        {
            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer <= 0f)
            {
                _ready = true;
                RefreshAura();
                StartCoroutine(ReadyPulse());
            }
        }
    }

    /// <summary>
    /// Called by Health.TakeDamage(). Returns true if the hit was absorbed.
    /// </summary>
    public bool TryAbsorb()
    {
        if (!IsActive || !_ready) return false;
        _ready = false;
        _cooldownTimer = cooldown;
        RefreshAura();
        StartCoroutine(BlockFlash());
        return true;
    }

    // ── Visuals ───────────────────────────────────────────────────────────────
    private void BuildAura()
    {
        GameObject go = new GameObject("ArmorAura");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = new Vector3(2.2f, 2.6f, 1f);
        _aura = go.AddComponent<SpriteRenderer>();
        _aura.sprite       = ProceduralSprite.Circle(24, Color.white);
        _aura.color        = new Color(0.4f, 0.7f, 1f, 0.22f);
        _aura.sortingOrder = -1;
    }

    private void RefreshAura()
    {
        if (_aura == null) return;
        _aura.enabled = IsActive;
        _aura.color = _ready
            ? new Color(0.4f, 0.7f, 1f, 0.22f)
            : new Color(0.4f, 0.4f, 0.5f, 0.08f);
    }

    private IEnumerator BlockFlash()
    {
        if (_aura == null) yield break;
        for (int i = 0; i < 3; i++)
        {
            _aura.color = new Color(0.7f, 0.9f, 1f, 0.7f);
            yield return new WaitForSeconds(0.05f);
            _aura.color = new Color(0.4f, 0.4f, 0.5f, 0.08f);
            yield return new WaitForSeconds(0.05f);
        }
    }

    private IEnumerator ReadyPulse()
    {
        if (_aura == null) yield break;
        float t = 0f;
        while (t < 0.4f)
        {
            t += Time.deltaTime;
            float k = Mathf.Sin(t / 0.4f * Mathf.PI);
            _aura.color = new Color(0.5f, 0.8f, 1f, 0.22f + 0.4f * k);
            yield return null;
        }
        RefreshAura();
    }
}
