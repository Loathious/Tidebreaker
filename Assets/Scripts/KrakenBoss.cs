using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Kraken — final boss (Level 5). From spelmanus: 1000 HP, three phases.
///  • Phase 1 (100–70%): three tentacles attack — the player must destroy all 3.
///  • Phase 2 (70–30%):  the body is vulnerable; energy blasts + water waves.
///  • Phase 3 (30–0%):   the heart is exposed — strike it until the Kraken dies.
/// </summary>
[RequireComponent(typeof(Health))]
public class KrakenBoss : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float maxHealth   = 1000f;
    [SerializeField] private int   energyDamage = 25;
    [SerializeField] private int   waveDamage   = 25;

    [Header("Tentacles (assigned by builder)")]
    public List<KrakenTentacle> tentacles = new List<KrakenTentacle>();
    [SerializeField] private float tentacleHealth = 120f;

    [Header("Body")]
    [Tooltip("Collider enabled only from Phase 2 on, so the body can't be hit early.")]
    public Collider2D bodyCollider;

    [Header("Audio")]
    public AudioClip talkClip;
    public AudioClip hurtClip;
    public AudioClip energyClip;
    public AudioClip waveClip;
    public AudioClip loseTentacleClip;
    public AudioClip deathClip;

    private Health         _health;
    private SpriteAnimator _anim;
    private SpriteRenderer _sr;
    private Transform      _player;
    private Health         _playerHealth;
    private BossHealthBar  _bar;

    private int   _phase;                 // 0 = dormant, 1/2/3 active
    private int   _tentaclesRemaining;
    private bool  _isDead;
    private float _attackTimer;
    private Color _origColor = Color.white;
    private Sprite _energySprite;
    private Vector3 _baseScale;
    private int   _heartStrikes;        // Phase 3: counts player hits on the exposed heart

    public bool IsDefeated => _isDead;

    void Awake()
    {
        _health = GetComponent<Health>();
        _anim   = GetComponent<SpriteAnimator>();
        _sr     = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        _health.SetMaxHealth(maxHealth);
        _baseScale = transform.localScale;

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) { _player = p.transform; _playerHealth = p.GetComponent<Health>(); }

        if (_sr != null) _origColor = _sr.color;
        _health.OnDeath.AddListener(OnDeath);
        _health.OnDamageTaken.AddListener(OnHurt);

        _energySprite = ProceduralSprite.Circle(10, new Color(0.7f, 0.3f, 1f));
        _anim?.Play("idle");

        // Body collider must be non-trigger: Projectile.OnTriggerEnter2D skips
        // trigger colliders, so player arrows would never register hits otherwise.
        if (bodyCollider != null)
        {
            bodyCollider.isTrigger = false;
            bodyCollider.enabled   = false;   // still hidden until Phase 2
        }

        // Hide / disable tentacles until Phase 1 begins
        foreach (var t in tentacles)
            if (t != null) t.gameObject.SetActive(false);
    }

    /// <summary>Called by OceanManager after the intro cutscene.</summary>
    public void BeginFight()
    {
        if (_phase != 0) return;
        StartCoroutine(EnterPhase1());
    }

    void Update()
    {
        if (_isDead || _player == null) return;

        // Auto-start if no manager triggered the intro (e.g. testing the scene)
        if (_phase == 0 &&
            Vector2.Distance(transform.position, _player.position) < 18f)
            BeginFight();

        if (_phase >= 2)
        {
            _attackTimer -= Time.deltaTime;
            if (_attackTimer <= 0f)
            {
                _attackTimer = _phase == 3 ? 2.4f : 3.2f;
                StartCoroutine(PhaseAttack());
            }
        }
        RefreshBar();
    }

    // ── Phase 1 ───────────────────────────────────────────────────────────────
    private IEnumerator EnterPhase1()
    {
        _phase = 1;
        LevelManagerBase.Current?.NotifyCombatStarted();
        _bar = BossHealthBar.Create("THE KRAKEN");
        _bar?.SetPhase("PHASE 1 — DESTROY THE TENTACLES");

        _tentaclesRemaining = 0;
        foreach (var t in tentacles)
        {
            if (t == null) continue;
            t.gameObject.SetActive(true);
            t.Init(this, tentacleHealth);
            _tentaclesRemaining++;
        }

        // If the builder somehow assigned none, skip straight to phase 2
        if (_tentaclesRemaining == 0)
            yield return StartCoroutine(EnterPhase2());
    }

    public void OnTentacleDestroyed()
    {
        if (_phase != 1) return;
        _tentaclesRemaining = Mathf.Max(0, _tentaclesRemaining - 1);
        if (loseTentacleClip != null)
            AudioSource.PlayClipAtPoint(loseTentacleClip, transform.position, 0.9f);
        Camera.main?.GetComponent<CameraShake>()?.Shake(0.2f, 0.25f);

        if (_tentaclesRemaining <= 0)
            StartCoroutine(EnterPhase2());
    }

    // ── Phase 2 ───────────────────────────────────────────────────────────────
    private IEnumerator EnterPhase2()
    {
        _phase = 2;
        _health.SetCurrentHealth(maxHealth * 0.7f);
        if (bodyCollider != null) bodyCollider.enabled = true;
        _bar?.SetPhase("PHASE 2 — STRIKE THE KRAKEN");
        _attackTimer = 2f;

        // Roar flash
        for (int i = 0; i < 4; i++)
        {
            if (_sr != null) _sr.color = new Color(1f, 0.6f, 1f);
            yield return new WaitForSeconds(0.08f);
            if (_sr != null) _sr.color = _origColor;
            yield return new WaitForSeconds(0.08f);
        }
    }

    // ── Phase 3 ───────────────────────────────────────────────────────────────
    private IEnumerator EnterPhase3()
    {
        _phase = 3;
        _heartStrikes = 0;
        _bar?.SetPhase("PHASE 3 — STRIKE THE HEART (0/10)");
        transform.localScale = _baseScale * 1.06f;

        for (int i = 0; i < 5; i++)
        {
            if (_sr != null) _sr.color = new Color(1f, 0.3f, 0.3f);
            yield return new WaitForSeconds(0.07f);
            if (_sr != null) _sr.color = _origColor;
            yield return new WaitForSeconds(0.07f);
        }
        Camera.main?.GetComponent<CameraShake>()?.Shake(0.3f, 0.4f);

        // Descend so the exposed heart is within reach of the player on platforms
        Vector3 target = new Vector3(transform.position.x, 2f, transform.position.z);
        float elapsed = 0f;
        Vector3 start = transform.position;
        while (elapsed < 1.5f)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(start, target, elapsed / 1.5f);
            yield return null;
        }
        transform.position = target;
    }

    // ── Attacks (Phase 2 & 3) ─────────────────────────────────────────────────
    private IEnumerator PhaseAttack()
    {
        if (_isDead) yield break;

        // Phase 3 attacks more aggressively
        int pick = Random.Range(0, _phase == 3 ? 3 : 2);
        if (pick == 0)      yield return EnergyBarrage();
        else if (pick == 1) yield return WaveAttack();
        else                yield return EnergyBarrage();
    }

    private IEnumerator EnergyBarrage()
    {
        if (energyClip != null) AudioSource.PlayClipAtPoint(energyClip, transform.position, 0.8f);
        int shots = _phase == 3 ? 5 : 3;
        for (int i = 0; i < shots; i++)
        {
            if (_isDead || _player == null) break;
            Vector3 origin = transform.position + new Vector3(0f, -0.5f, 0f);
            Vector2 aim = ((Vector2)(_player.position - origin)).normalized;
            Projectile.Spawn(origin, aim, 8.5f, energyDamage, true, _energySprite, 0f, 0.2f, 70);
            yield return new WaitForSeconds(0.35f);
        }
    }

    private IEnumerator WaveAttack()
    {
        if (waveClip != null) AudioSource.PlayClipAtPoint(waveClip, transform.position, 0.9f);
        if (_player == null) yield break;

        // A wide water wave sweeps horizontally across the player's height
        GameObject wave = new GameObject("KrakenWave");
        float startX = _player.position.x + (Random.value > 0.5f ? 16f : -16f);
        float dir    = startX > _player.position.x ? -1f : 1f;
        wave.transform.position = new Vector3(startX, _player.position.y, 0f);

        var sr = wave.AddComponent<SpriteRenderer>();
        sr.sprite       = ProceduralSprite.Box(8, 40, new Color(0.4f, 0.7f, 1f, 0.55f));
        sr.sortingOrder = 80;
        wave.transform.localScale = new Vector3(1.4f, 1.4f, 1f);

        float life = 0f;
        bool hit = false;
        while (life < 4f && wave != null)
        {
            life += Time.deltaTime;
            wave.transform.position += new Vector3(dir * 11f * Time.deltaTime, 0f, 0f);
            if (!hit && _playerHealth != null && _player != null && !_playerHealth.IsDead &&
                Mathf.Abs(wave.transform.position.x - _player.position.x) < 0.8f &&
                Mathf.Abs(wave.transform.position.y - _player.position.y) < 3f)
            {
                _playerHealth.TakeDamage(waveDamage);
                hit = true;
            }
            yield return null;
        }
        if (wave != null) Destroy(wave);
    }

    // ── Health ────────────────────────────────────────────────────────────────
    private void OnHurt(float dmg)
    {
        if (_isDead) return;
        if (hurtClip != null) AudioSource.PlayClipAtPoint(hurtClip, transform.position, 0.5f);
        StartCoroutine(HitFlash());

        if (_phase == 2 && _health.CurrentHealth <= maxHealth * 0.3f)
        {
            StartCoroutine(EnterPhase3());
            return;
        }

        // Phase 3 — track the 10 heart strikes required to finish the Kraken
        if (_phase == 3)
        {
            _heartStrikes++;
            _bar?.SetPhase($"PHASE 3 — STRIKE THE HEART ({_heartStrikes}/10)");
            if (_heartStrikes >= 10 && !_isDead)
            {
                _health.SetCurrentHealth(0f);
                _health.OnDeath.Invoke();   // force the death event chain
            }
        }
    }

    private IEnumerator HitFlash()
    {
        if (_sr == null) yield break;
        Color flash = new Color(1f, 0.55f, 0.55f);
        _sr.color = flash;
        yield return new WaitForSeconds(0.07f);
        if (!_isDead && _sr != null) _sr.color = _origColor;
    }

    private void RefreshBar()
    {
        if (_bar == null) return;
        float ratio;
        if (_phase == 1)
            ratio = Mathf.Lerp(0.7f, 1f, _tentaclesRemaining / Mathf.Max(1f, (float)tentacles.Count));
        else
            ratio = _health.CurrentHealth / maxHealth;
        _bar.SetHealth(ratio);
    }

    private void OnDeath()
    {
        if (_isDead) return;
        _isDead = true;
        _phase  = 4;
        StopAllCoroutines();
        if (deathClip != null) AudioSource.PlayClipAtPoint(deathClip, transform.position, 1f);
        _bar?.Dismiss();

        foreach (Collider2D c in GetComponents<Collider2D>()) c.enabled = false;
        if (bodyCollider != null) bodyCollider.enabled = false;

        (LevelManagerBase.Current as OceanManager)?.OnKrakenDefeated();
        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        Camera.main?.GetComponent<CameraShake>()?.Shake(0.4f, 1.2f);
        float t = 0f;
        while (t < 2.5f)
        {
            t += Time.deltaTime;
            if (_sr != null)
            {
                float flicker = 0.5f + 0.5f * Mathf.Sin(t * 30f);
                _sr.color = new Color(1f, flicker, flicker, 1f - t / 2.5f);
            }
            transform.localScale = _baseScale * (1f + 0.1f * Mathf.Sin(t * 8f)) * (1f - 0.2f * (t / 2.5f));
            yield return null;
        }
        Destroy(gameObject);
    }
}
