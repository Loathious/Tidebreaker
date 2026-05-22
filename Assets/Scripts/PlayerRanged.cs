using System.Collections;
using UnityEngine;

/// <summary>
/// Bow / arrow ranged attack for the player. Obtained inside the Desert pyramid
/// (Level 4) and required to destroy the Kraken's tentacles (Level 5).
///
/// Hold right-click to draw/aim the bow. Release right-click to fire.
/// A 4-frame bow draw animation plays while holding. The bow stays fully
/// drawn until you release, letting you aim carefully.
/// Active only once "PlayerHasBow" is set.
/// </summary>
public class PlayerRanged : MonoBehaviour
{
    [Header("Arrow")]
    public Sprite arrowSprite;
    public float  arrowSpeed   = 18f;
    public float  arrowDamage  = 25f;
    public float  fireCooldown = 0.7f;

    [Header("Bow visual (4 draw frames)")]
    public Sprite[] bowFrames;

    [Header("Audio")]
    public AudioClip drawClip;
    public AudioClip shootClip;
    public AudioClip arrowHitClip;
    public AudioClip arrowMissClip;

    private SpriteRenderer _bowVisual;
    private float _cooldown;
    private bool  _inUse;    // true while holding or firing the bow
    private AudioSource _audio;

    public bool HasBow => PlayerPrefs.GetInt("PlayerHasBow", 0) == 1;

    /// <summary>Grants the bow permanently (called by the pyramid reward).</summary>
    public static void Grant()
    {
        PlayerPrefs.SetInt("PlayerHasBow", 1);
        PlayerPrefs.Save();
    }

    void Start()
    {
        _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake  = false;
        _audio.spatialBlend = 0f;
        BuildBowVisual();
    }

    void OnDisable()
    {
        // Reset state so the bow isn't locked if the component is disabled
        // (e.g. player death) while mid-draw.
        _inUse = false;
        if (_bowVisual != null) _bowVisual.enabled = false;
    }

    void Update()
    {
        if (_cooldown > 0f) _cooldown -= Time.deltaTime;
        if (!HasBow || _inUse) return;

        // Right-click hold to draw; release to fire.
        if (Input.GetMouseButtonDown(1) && _cooldown <= 0f)
        {
            DialogUI dialog = FindFirstObjectByType<DialogUI>();
            if (dialog != null && dialog.IsOpen) return;
            StartCoroutine(AimAndFireRoutine());
        }
    }

    /// <summary>
    /// Single coroutine that handles the full bow lifecycle:
    ///  1. Draw animation (frames 0-2) while button is held, tracking the mouse.
    ///  2. Hold at fully-drawn frame, continuously tracking the mouse.
    ///  3. On release: show the release frame (frame 3) and fire the arrow.
    /// </summary>
    private IEnumerator AimAndFireRoutine()
    {
        _inUse = true;

        if (_bowVisual != null)
            _bowVisual.enabled = true;

        if (drawClip != null)
            _audio.PlayOneShot(drawClip, 0.7f * SettingsManager.SfxVol);

        // ── Draw animation (frames 0–2) ─────────────────────────────────────────
        int drawFrameCount = Mathf.Min(3, bowFrames != null ? bowFrames.Length : 0);
        bool released = false;

        for (int i = 0; i < drawFrameCount && !released; i++)
        {
            if (bowFrames != null && _bowVisual != null)
                _bowVisual.sprite = bowFrames[i];

            float elapsed = 0f;
            while (elapsed < 0.08f && !released)
            {
                elapsed += Time.deltaTime;
                UpdateBowAim();
                yield return null;

                // Early release: exit draw loop and go straight to firing
                if (!Input.GetMouseButton(1))
                    released = true;
            }
        }

        // ── Hold fully drawn — aim until button is released ──────────────────────
        if (!released)
        {
            // Stay on the last draw frame
            if (bowFrames != null && _bowVisual != null && drawFrameCount > 0)
                _bowVisual.sprite = bowFrames[drawFrameCount - 1];

            while (Input.GetMouseButton(1))
            {
                UpdateBowAim();
                yield return null;
            }
        }

        // ── Fire ─────────────────────────────────────────────────────────────────
        // Capture aim direction at the moment of release
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;
        Vector2 dir = ((Vector2)(mouseWorld - transform.position)).normalized;
        if (dir.sqrMagnitude < 0.001f) dir = Vector2.right;

        // Show release frame (frame 3) if available
        if (_bowVisual != null && bowFrames != null && bowFrames.Length >= 4)
            _bowVisual.sprite = bowFrames[3];

        // Spawn arrow
        Vector3 origin = transform.position + (Vector3)(dir * 0.5f) + Vector3.up * 0.1f;
        Projectile arrow = Projectile.Spawn(origin, dir, arrowSpeed, arrowDamage,
                                            false, arrowSprite, 0f, 0.12f, 60);
        if (arrow != null)
        {
            arrow.faceVelocity = true;
            arrow.hitClip      = arrowHitClip;
            arrow.missClip     = arrowMissClip;
        }
        if (shootClip != null)
            _audio.PlayOneShot(shootClip, 0.9f * SettingsManager.SfxVol);

        _cooldown = fireCooldown;

        // Brief release pause before hiding bow
        yield return new WaitForSeconds(0.12f);

        if (_bowVisual != null) _bowVisual.enabled = false;
        _inUse = false;
    }

    /// <summary>Rotates the bow sprite to track the mouse cursor.</summary>
    private void UpdateBowAim()
    {
        if (_bowVisual == null) return;
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;
        Vector2 dir = ((Vector2)(mouseWorld - transform.position)).normalized;
        if (dir.sqrMagnitude < 0.001f) dir = Vector2.right;

        _bowVisual.flipX = dir.x < 0f;
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        _bowVisual.transform.localRotation =
            Quaternion.Euler(0, 0, dir.x < 0f ? ang + 180f : ang);
    }

    private void BuildBowVisual()
    {
        GameObject go = new GameObject("BowVisual");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0.35f, 0f, 0f);
        _bowVisual = go.AddComponent<SpriteRenderer>();
        _bowVisual.sortingOrder = 12;
        if (bowFrames != null && bowFrames.Length > 0) _bowVisual.sprite = bowFrames[0];
        _bowVisual.enabled = false;
    }
}
