using UnityEngine;

/// <summary>
/// A platform that moves between two points and carries the player.
/// Used for the Desert platform puzzle (Obelisk 2) and the Ocean arena.
/// Carries the player by adding the platform's frame delta to the player's
/// Rigidbody2D — no parenting, so the player's flip/physics stay intact.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class MovingPlatform : MonoBehaviour
{
    [Header("Path (local offsets from start)")]
    public Vector2 pointA = Vector2.zero;
    public Vector2 pointB = new Vector2(5f, 0f);
    public float   speed  = 2f;
    public float   waitTime = 0.5f;

    private Vector3 _origin;
    private Vector3 _prevPos;
    private float   _waitTimer;
    private bool    _towardB = true;
    private Rigidbody2D _riderRb;

    void Start()
    {
        _origin  = transform.position;
        _prevPos = transform.position;
        transform.position = _origin + (Vector3)pointA;

        if (gameObject.layer == 0) gameObject.layer = LayerMask.NameToLayer("Ground");

        // A kinematic rigidbody gives smooth interpolated motion
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType      = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    void FixedUpdate()
    {
        Vector3 target = _origin + (Vector3)(_towardB ? pointB : pointA);

        if (_waitTimer > 0f)
        {
            _waitTimer -= Time.fixedDeltaTime;
        }
        else
        {
            transform.position = Vector3.MoveTowards(transform.position, target,
                                                     speed * Time.fixedDeltaTime);
            if (Vector3.Distance(transform.position, target) < 0.01f)
            {
                _towardB   = !_towardB;
                _waitTimer = waitTime;
            }
        }

        // Carry the rider
        Vector3 delta = transform.position - _prevPos;
        if (_riderRb != null && delta.sqrMagnitude > 0f)
            _riderRb.position += (Vector2)delta;

        _prevPos = transform.position;
    }

    void OnCollisionStay2D(Collision2D col)
    {
        if (!col.gameObject.CompareTag("Player")) return;
        foreach (ContactPoint2D c in col.contacts)
        {
            if (c.normal.y < -0.5f)   // player is on top
            {
                _riderRb = col.rigidbody;
                return;
            }
        }
    }

    void OnCollisionExit2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Player")) _riderRb = null;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 o = Application.isPlaying ? _origin : transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(o + (Vector3)pointA, 0.2f);
        Gizmos.DrawWireSphere(o + (Vector3)pointB, 0.2f);
        Gizmos.DrawLine(o + (Vector3)pointA, o + (Vector3)pointB);
    }
}
