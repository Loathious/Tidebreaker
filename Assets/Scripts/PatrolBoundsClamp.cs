using UnityEngine;

/// <summary>
/// Clamps a GameObject's position to a world-space X range each frame.
/// Uses Collider2D.bounds.center when available so the clamp operates on the
/// visual center even if the sprite pivot is not at the transform origin.
/// Call SetClamping(false) to suspend clamping temporarily (e.g. during a jump).
/// </summary>
public class PatrolBoundsClamp : MonoBehaviour
{
    private float      _minX;
    private float      _maxX;
    private bool       _active;
    private bool       _clamping = true;
    private Collider2D _col;

    public void Init(float minX, float maxX)
    {
        _minX   = minX;
        _maxX   = maxX;
        _col    = GetComponent<Collider2D>();
        _active = true;
    }

    public void SetClamping(bool enabled) { _clamping = enabled; }

    void LateUpdate()
    {
        if (!_active || !_clamping) return;

        // Operate on the visual center (collider bounds) so enemies with off-center
        // sprite pivots are clamped correctly.
        float centerX = _col != null ? _col.bounds.center.x : transform.position.x;

        if (centerX < _minX || centerX > _maxX)
        {
            float clampedCenter = Mathf.Clamp(centerX, _minX, _maxX);
            float delta = clampedCenter - centerX;

            Vector3 p = transform.position;
            p.x += delta;

            var rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                rb.position = new Vector2(p.x, p.y);
            }
            else
            {
                transform.position = p;
            }
        }
    }
}
