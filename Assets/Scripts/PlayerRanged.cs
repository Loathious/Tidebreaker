using System.Collections;
using UnityEngine;

/// <summary>
/// Bow / arrow ranged attack for the player. Obtained inside the Desert pyramid
/// (Level 4) and required to destroy the Kraken's tentacles (Level 5).
///
/// Right-click to fire an arrow toward the mouse. A 4-frame bow draw animation
/// plays on a child SpriteRenderer. Active only once "PlayerHasBow" is set.
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
    private bool  _firing;
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

    void Update()
    {
        if (_cooldown > 0f) _cooldown -= Time.deltaTime;
        if (!HasBow || _firing) return;

        // Right-click to fire (left-click stays melee). Ignore clicks on dialog.
        if (Input.GetMouseButtonDown(1) && _cooldown <= 0f)
        {
            DialogUI dialog = FindFirstObjectByType<DialogUI>();
            if (dialog != null && dialog.IsOpen) return;
            StartCoroutine(FireRoutine());
        }
    }

    private IEnumerator FireRoutine()
    {
        _firing   = true;
        _cooldown = fireCooldown;

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;
        Vector2 dir = ((Vector2)(mouseWorld - transform.position)).normalized;
        if (dir.sqrMagnitude < 0.001f) dir = Vector2.right;

        // Draw animation (bow frames 1-3)
        if (_bowVisual != null && bowFrames != null && bowFrames.Length > 0)
        {
            _bowVisual.enabled = true;
            _bowVisual.flipX   = dir.x < 0f;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            _bowVisual.transform.localRotation = Quaternion.Euler(0, 0, dir.x < 0f ? ang + 180f : ang);
        }
        if (drawClip != null) _audio.PlayOneShot(drawClip, 0.7f * SettingsManager.SfxVol);

        int drawCount = Mathf.Min(3, bowFrames != null ? bowFrames.Length : 0);
        for (int i = 0; i < drawCount; i++)
        {
            if (_bowVisual != null) _bowVisual.sprite = bowFrames[i];
            yield return new WaitForSeconds(0.08f);
        }

        // Fire the arrow
        Vector3 origin = transform.position + (Vector3)(dir * 0.5f) + Vector3.up * 0.1f;
        Projectile arrow = Projectile.Spawn(origin, dir, arrowSpeed, arrowDamage,
                                            false, arrowSprite, 0f, 0.12f, 60);
        if (arrow != null)
        {
            arrow.faceVelocity = true;
            arrow.hitClip      = arrowHitClip;
            arrow.missClip     = arrowMissClip;
        }
        if (shootClip != null) _audio.PlayOneShot(shootClip, 0.9f * SettingsManager.SfxVol);

        // Release frame (bow frame 4)
        if (_bowVisual != null && bowFrames != null && bowFrames.Length >= 4)
            _bowVisual.sprite = bowFrames[3];

        yield return new WaitForSeconds(0.12f);
        if (_bowVisual != null) _bowVisual.enabled = false;
        _firing = false;
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
