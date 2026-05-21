using System.Collections;
using UnityEngine;

/// <summary>
/// Handles player sword attacks. Left-click to attack.
/// Enemies only take damage if the player is FACING them.
/// Hit feedback: red flash + blood particles + knockback (no hitmarker).
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] private float attackRange       = 4f;
    [SerializeField] private LayerMask enemyLayers;
    [SerializeField] private float knockbackForce = 9f;
    [SerializeField] private float knockbackUp    = 0.6f;

    [Header("Attack Timing (tweak for feel)")]
    [Tooltip("Seconds between attacks. Lower = faster spam.")]
    [SerializeField] private float attackCooldown = 0.32f;
    [Tooltip("Delay from click to damage window. Should be less than attackCooldown.")]
    [SerializeField] private float damageDelay    = 0.10f;

    [Header("Hit Feel")]
    [SerializeField] private float hitStopDuration     = 0.06f;
    [SerializeField] private float cameraShakeStrength = 0.12f;
    [SerializeField] private float cameraShakeDuration = 0.1f;

    [Header("Audio")]
    [SerializeField] private AudioClip swingClip;
    [SerializeField] private AudioClip hitClip;
    [SerializeField] [Range(0f, 1f)] private float swingVolume = 0.7f;
    [SerializeField] [Range(0f, 1f)] private float hitVolume   = 1f;

    private static readonly int IsHoldingSwordHash = Animator.StringToHash("isHoldingSword");
    private static readonly int IsAttackingHash    = Animator.StringToHash("isAttacking");

    private ItemData       _equippedItem;
    private int            _currentUses;
    private bool           _isAttacking;
    private float          _cooldown;
    private Animator       _animator;
    private SpriteRenderer _sr;
    private CameraShake    _cameraShake;
    private AudioSource    _audio;
    private PlayerController _playerController;
    private Health         _selfHealth;

    void Start()
    {
        _animator         = GetComponent<Animator>();
        _sr               = GetComponent<SpriteRenderer>();
        _cameraShake      = Camera.main?.GetComponent<CameraShake>();
        _playerController = GetComponent<PlayerController>();
        _selfHealth       = GetComponent<Health>();

        _audio             = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f;

        Inventory.Instance?.OnItemEquipped.AddListener(OnItemEquipped);

        // Restore equipped item and durability when loading into a new scene
        // (Inventory persists via DontDestroyOnLoad but PlayerCombat is scene-bound)
        ItemData carried = Inventory.Instance?.GetEquippedItem();
        if (carried != null)
        {
            _equippedItem = carried;
            _currentUses  = PlayerPrefs.GetInt("WeaponCurrentUses", 0);
            _animator?.SetBool(IsHoldingSwordHash, true);
        }
    }

    void Update()
    {
        if (_selfHealth != null && _selfHealth.IsDead) return;
        if (_cooldown > 0f) _cooldown -= Time.deltaTime;

        // Allow attacking even without a weapon when clicking on diamond rocks
        bool hasDiamondRockTarget = false;
        if (_equippedItem == null && Input.GetMouseButtonDown(0))
        {
            Vector2 mw = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            foreach (Collider2D c in Physics2D.OverlapPointAll(mw))
                if (c.GetComponent<DiamondRock>() != null) { hasDiamondRockTarget = true; break; }
        }

        if ((_equippedItem != null || hasDiamondRockTarget) && !_isAttacking && _cooldown <= 0f && Input.GetMouseButtonDown(0))
        {
            // Don't attack if the dialog is open (click was used by DialogUI)
            DialogUI dialog = FindFirstObjectByType<DialogUI>();
            if (dialog != null && dialog.IsOpen) return;

            StartCoroutine(Attack());
        }
    }

    IEnumerator Attack()
    {
        _isAttacking = true;
        _cooldown    = attackCooldown;

        // Trigger animation — check parameter exists first
        if (_animator != null)
        {
            bool hasParam = false;
            foreach (var p in _animator.parameters)
                if (p.name == "isAttacking") { hasParam = true; break; }
            if (hasParam) _animator.SetTrigger(IsAttackingHash);
        }

        // Swing sound on every attack
        if (swingClip != null) _audio.PlayOneShot(swingClip, swingVolume * SettingsManager.SfxVol);

        // Capture click point at the moment of click (before damage delay)
        Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        yield return new WaitForSeconds(damageDelay);

        // ── Hit detection (range-based melee sweep) ──────────────────────────
        // All enemies within attackRange of the PLAYER are candidates; the closest
        // one on the click-side (left/right) takes damage. No need to click precisely
        // on the collider — just be in range and face the enemy.
        int allLayers = ~0;
        Vector2 playerPos = transform.position;

        // Determine which side the player is attacking toward
        float clickDirX = mouseWorld.x - playerPos.x;

        Collider2D[] candidates = Physics2D.OverlapCircleAll(playerPos, attackRange, allLayers);

        Collider2D best     = null;
        float      bestDist = float.MaxValue;
        Vector2    hitPoint = mouseWorld;

        foreach (Collider2D col in candidates)
        {
            if (col.gameObject == gameObject) continue;
            if (col.GetComponent<Health>() == null) continue;

            // Use bounds.center for position — handles enemies whose sprite pivot is not centered.
            Vector2 enemyCenter = col.bounds.center;
            float enemyDirX = enemyCenter.x - playerPos.x;
            bool wrongSide  = Mathf.Abs(clickDirX) > 0.3f && Mathf.Abs(enemyDirX) > 0.5f
                              && Mathf.Sign(enemyDirX) != Mathf.Sign(clickDirX);
            if (wrongSide) continue;

            float dist = Vector2.Distance(playerPos, enemyCenter);
            if (dist < bestDist)
            {
                bestDist = dist;
                best     = col;
                hitPoint = col.bounds.center;   // visual feedback at the enemy
            }
        }

        // If nothing on click-side, fall back to closest enemy anywhere in range
        if (best == null)
        {
            foreach (Collider2D col in candidates)
            {
                if (col.gameObject == gameObject) continue;
                if (col.GetComponent<Health>() == null) continue;
                float dist = Vector2.Distance(playerPos, (Vector2)col.bounds.center);
                if (dist < bestDist) { bestDist = dist; best = col; hitPoint = col.bounds.center; }
            }
        }

        // ── Check for diamond rocks at the exact click position ──────────────
        // Rocks still require clicking on them directly; enemies use range-based.
        bool hitRock = false;
        Collider2D[] mouseHits = Physics2D.OverlapPointAll(mouseWorld, allLayers);
        foreach (Collider2D col in mouseHits)
        {
            DiamondRock rock = col.GetComponent<DiamondRock>();
            if (rock == null) continue;
            if (Vector2.Distance(transform.position, col.transform.position) > attackRange) continue;
            HitSpark.Spawn(col.bounds.center);
            if (hitClip != null) _audio.PlayOneShot(hitClip, hitVolume * SettingsManager.SfxVol);
            rock.Hit();
            hitRock = true;
            break;
        }

        // ── Apply hit on enemy ────────────────────────────────────────────────
        if (!hitRock && best != null)
        {
            Health hp = best.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                // Blood burst at enemy center (no hitmarker — blood + knockback is the feedback)
                SpawnBloodAtPoint(hitPoint);

                hp.TakeDamage(_equippedItem.damage);

                // Knockback surviving enemies
                if (!hp.IsDead)
                {
                    Vector2 flat     = ((Vector3)best.bounds.center - transform.position).normalized;
                    Vector2 knockDir = new Vector2(flat.x, knockbackUp).normalized * knockbackForce;

                    ZombieAI zombie = best.GetComponent<ZombieAI>();
                    SpiderAI spider = best.GetComponent<SpiderAI>();
                    if (zombie != null)
                        zombie.Knockback(knockDir);
                    else if (spider != null)
                        spider.Knockback(knockDir);
                    else
                        best.GetComponent<Rigidbody2D>()?.AddForce(knockDir, ForceMode2D.Impulse);
                }

                if (hitClip != null) _audio.PlayOneShot(hitClip, hitVolume * SettingsManager.SfxVol);

                if (hitStopDuration > 0f)
                    StartCoroutine(HitStop(hitStopDuration));

                _cameraShake?.Shake(cameraShakeStrength, cameraShakeDuration);

                // Durability
                if (_equippedItem != null && _equippedItem.maxUses > 0)
                {
                    _currentUses++;
                    PlayerPrefs.SetInt("WeaponCurrentUses", _currentUses);
                    if (_currentUses >= _equippedItem.maxUses)
                    {
                        PlayerPrefs.DeleteKey("WeaponCurrentUses");
                        ShowSwordBreakEffect();
                        int slot = Inventory.Instance != null ? Inventory.Instance.GetEquippedSlot() : 0;
                        Inventory.Instance?.RemoveItem(slot);
                    }
                }
            }
        }

        yield return new WaitForSeconds(attackCooldown - damageDelay);
        _isAttacking = false;
    }

    IEnumerator HitStop(float duration)
    {
        float original = Time.timeScale;
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = original;
    }

    private void ShowSwordBreakEffect()
    {
        StartCoroutine(SwordBreakCoroutine());
    }

    private IEnumerator SwordBreakCoroutine()
    {
        // Flash player red
        if (_sr != null)
        {
            Color orig = _sr.color;
            for (int i = 0; i < 4; i++)
            {
                _sr.color = new Color(1f, 0.3f, 0.3f);
                yield return new WaitForSecondsRealtime(0.07f);
                _sr.color = orig;
                yield return new WaitForSecondsRealtime(0.07f);
            }
        }

        // Show "SWORD BROKE!" in screen-space
        Canvas c = null;
        foreach (Canvas cv in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (cv.renderMode == RenderMode.ScreenSpaceOverlay) { c = cv; break; }
        if (c == null) yield break;

        GameObject go = new GameObject("SwordBrokeText");
        go.transform.SetParent(c.transform, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.3f, 0.6f); rt.anchorMax = new Vector2(0.7f, 0.75f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var tmp = go.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text      = "SWORD BROKE!";
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.fontSize  = 14f;
        tmp.color     = new Color(1f, 0.3f, 0.3f, 1f);
        tmp.outlineWidth = 0.22f;
        tmp.outlineColor = new Color32(0, 0, 0, 255);
        var font = UnityEngine.Resources.Load<TMPro.TMP_FontAsset>("Fonts & Materials/PressStart2P-Regular SDF")
                ?? UnityEngine.Resources.Load<TMPro.TMP_FontAsset>("PressStart2P-Regular SDF");
        if (font != null) tmp.font = font;

        float t = 0f;
        while (t < 2f)
        {
            t += Time.unscaledDeltaTime;
            float a = t < 0.3f ? (t / 0.3f) : (t > 1.5f ? (1f - (t - 1.5f) / 0.5f) : 1f);
            tmp.color = new Color(1f, 0.3f, 0.3f, Mathf.Clamp01(a));
            yield return null;
        }
        Destroy(go);
    }

    public void PlaySwingSound()
    {
        if (swingClip != null) _audio.PlayOneShot(swingClip, swingVolume * SettingsManager.SfxVol);
    }

    private void SpawnBloodAtPoint(Vector3 pos)
    {
        var go = new GameObject("BloodFX");
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        go.AddComponent<BloodParticleSetup>();
        ps.Emit(8);
        Destroy(go, 2f);
    }

    void OnItemEquipped(ItemData item)
    {
        _equippedItem = item;
        _currentUses  = 0;
        PlayerPrefs.DeleteKey("WeaponCurrentUses");
        if (_equippedItem == null) { StopAllCoroutines(); _isAttacking = false; }
        _animator?.SetBool(IsHoldingSwordHash, _equippedItem != null);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
