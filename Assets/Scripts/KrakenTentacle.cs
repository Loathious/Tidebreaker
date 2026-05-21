using System.Collections;
using UnityEngine;

/// <summary>
/// One destructible Kraken tentacle (Final Boss, Phase 1).
/// The player shoots arrows to destroy it; it periodically slaps the platforms.
/// When destroyed it reports back to the KrakenBoss.
/// </summary>
[RequireComponent(typeof(Health))]
public class KrakenTentacle : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float startHealth   = 120f;
    [SerializeField] private int   slapDamage    = 20;
    [SerializeField] private float attackInterval = 3.5f;
    [SerializeField] private float strikeRadius   = 3.5f;

    [Header("Audio")]
    public AudioClip attackClip;
    public AudioClip hurtClip;
    public AudioClip deathClip;

    private Health         _health;
    private SpriteAnimator _anim;
    private SpriteRenderer _sr;
    private Transform      _player;
    private Health         _playerHealth;
    private KrakenBoss     _boss;

    private float _timer;
    private bool  _isDead;
    private bool  _attacking;
    private Color _origColor = Color.white;

    public void Init(KrakenBoss boss, float hp)
    {
        _boss = boss;
        startHealth = hp;
    }

    void Awake()
    {
        _health = GetComponent<Health>();
        _anim   = GetComponent<SpriteAnimator>();
        _sr     = GetComponent<SpriteRenderer>();

        // Projectile.OnTriggerEnter2D skips isTrigger=true colliders,
        // so player arrows would pass through without dealing damage.
        // Force non-trigger so the tentacle is hittable by arrows.
        foreach (BoxCollider2D c in GetComponents<BoxCollider2D>())
            c.isTrigger = false;
    }

    void Start()
    {
        _health.SetMaxHealth(startHealth);

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) { _player = p.transform; _playerHealth = p.GetComponent<Health>(); }

        if (_sr != null) _origColor = _sr.color;
        _health.OnDeath.AddListener(OnDeath);
        _health.OnDamageTaken.AddListener(_ => OnHurt());
        _timer = Random.Range(1.5f, attackInterval);
        _anim?.Play("idle");
    }

    void Update()
    {
        if (_isDead || _attacking) return;
        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            _timer = attackInterval;
            StartCoroutine(Slap());
        }
    }

    private IEnumerator Slap()
    {
        _attacking = true;
        _anim?.Play("attack", true);
        if (attackClip != null) SettingsManager.PlaySfxAt(attackClip, transform.position, 0.85f);

        // Wind-up, then the strike
        yield return new WaitForSeconds(0.55f);
        Camera.main?.GetComponent<CameraShake>()?.Shake(0.13f, 0.16f);

        if (_playerHealth != null && _player != null && !_playerHealth.IsDead &&
            Vector2.Distance(transform.position, _player.position) <= strikeRadius)
        {
            _playerHealth.TakeDamage(slapDamage);
        }

        yield return new WaitForSeconds(0.6f);
        if (!_isDead) _anim?.Play("idle");
        _attacking = false;
    }

    private void OnHurt()
    {
        if (_isDead) return;
        if (hurtClip != null) SettingsManager.PlaySfxAt(hurtClip, transform.position, 0.5f);
        StartCoroutine(HitFlash());
    }

    private IEnumerator HitFlash()
    {
        if (_sr == null) yield break;
        _sr.color = new Color(1f, 0.5f, 0.5f);
        yield return new WaitForSeconds(0.08f);
        if (!_isDead && _sr != null) _sr.color = _origColor;
    }

    private void OnDeath()
    {
        if (_isDead) return;
        _isDead = true;
        StopAllCoroutines();
        foreach (Collider2D c in GetComponents<Collider2D>()) c.enabled = false;
        if (deathClip != null) SettingsManager.PlaySfxAt(deathClip, transform.position, 0.9f);
        if (_boss != null) _boss.OnTentacleDestroyed();
        StartCoroutine(DeathFade());
    }

    private IEnumerator DeathFade()
    {
        float t = 0f;
        Vector3 baseScale = transform.localScale;
        while (t < 0.9f)
        {
            t += Time.deltaTime;
            if (_sr != null)
                _sr.color = new Color(_origColor.r, _origColor.g, _origColor.b, 1f - t / 0.9f);
            transform.localScale = baseScale * (1f - 0.5f * (t / 0.9f));
            yield return null;
        }
        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, strikeRadius);
    }
}
