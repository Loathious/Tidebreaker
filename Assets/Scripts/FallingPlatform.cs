using System.Collections;
using UnityEngine;

/// <summary>
/// A platform that shakes, turns red and drops shortly after the player steps on
/// it. Used heavily in the Ocean boss arena (Level 5) where "platforms fall".
/// Optionally respawns at its origin so the arena stays playable.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class FallingPlatform : MonoBehaviour
{
    [Header("Sprites")]
    public Sprite normalSprite;
    public Sprite warningSprite;

    [Header("Timing")]
    public float triggerDelay = 0.6f;   // shake time before falling
    public float fallGravity  = 2.5f;
    public bool  respawns     = true;
    public float respawnTime  = 4f;

    [Header("Audio")]
    public AudioClip fallClip;

    private SpriteRenderer _sr;
    private BoxCollider2D  _col;
    private Rigidbody2D    _rb;
    private Vector3        _origin;
    private bool           _triggered;

    void Awake()
    {
        _sr  = GetComponent<SpriteRenderer>();
        _col = GetComponent<BoxCollider2D>();
        _origin = transform.position;

        _rb = GetComponent<Rigidbody2D>();
        if (_rb == null) _rb = gameObject.AddComponent<Rigidbody2D>();
        _rb.bodyType     = RigidbodyType2D.Kinematic;
        _rb.gravityScale = 0f;

        if (normalSprite != null) _sr.sprite = normalSprite;
        if (gameObject.layer == 0) gameObject.layer = LayerMask.NameToLayer("Ground");
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (_triggered) return;
        if (!col.gameObject.CompareTag("Player")) return;

        // Only trigger when the player lands on TOP of the platform
        foreach (ContactPoint2D c in col.contacts)
        {
            if (c.normal.y < -0.5f) { StartCoroutine(FallSequence()); return; }
        }
    }

    private IEnumerator FallSequence()
    {
        _triggered = true;

        // Shake + turn red
        Vector3 basePos = transform.position;
        float t = 0f;
        bool red = false;
        while (t < triggerDelay)
        {
            t += Time.deltaTime;
            transform.position = basePos + new Vector3(Random.Range(-0.05f, 0.05f), 0f, 0f);
            if (!red && t > triggerDelay * 0.35f && warningSprite != null)
            {
                _sr.sprite = warningSprite;
                red = true;
            }
            yield return null;
        }
        transform.position = basePos;

        // Drop
        if (fallClip != null) AudioSource.PlayClipAtPoint(fallClip, transform.position, 0.8f);
        _rb.bodyType     = RigidbodyType2D.Dynamic;
        _rb.gravityScale = fallGravity;
        _col.enabled     = false;   // player falls through once it drops

        if (respawns)
        {
            yield return new WaitForSeconds(respawnTime);
            yield return Respawn(basePos);
        }
        else
        {
            yield return new WaitForSeconds(3f);
            Destroy(gameObject);
        }
    }

    private IEnumerator Respawn(Vector3 pos)
    {
        _rb.linearVelocity = Vector2.zero;
        _rb.bodyType     = RigidbodyType2D.Kinematic;
        _rb.gravityScale = 0f;
        transform.position = pos;
        transform.rotation = Quaternion.identity;
        if (normalSprite != null) _sr.sprite = normalSprite;

        // Fade back in
        float t = 0f;
        Color baseCol = Color.white;
        while (t < 0.4f)
        {
            t += Time.deltaTime;
            _sr.color = new Color(baseCol.r, baseCol.g, baseCol.b, t / 0.4f);
            yield return null;
        }
        _sr.color = baseCol;
        _col.enabled = true;
        _triggered   = false;
    }
}
