using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shapes the PolygonCollider2D to exactly match the attached sprite's physics outline.
/// Runs in the Editor (via ExecuteAlways) so the collider updates immediately when the sprite changes.
/// The sprite must have Read/Write enabled in its import settings.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(PolygonCollider2D))]
public class SpriteCollider : MonoBehaviour
{
    private SpriteRenderer   _spriteRenderer;
    private PolygonCollider2D _collider;
    private Sprite           _lastSprite;

    void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _collider       = GetComponent<PolygonCollider2D>();
        ApplySpriteShape();
    }

    void Update()
    {
        // In Edit mode only: re-apply when the sprite asset changes
        if (!Application.isPlaying && _spriteRenderer != null && _spriteRenderer.sprite != _lastSprite)
            ApplySpriteShape();
    }

    /// <summary>Reads the sprite's physics shape paths and applies them to the PolygonCollider2D.</summary>
    public void ApplySpriteShape()
    {
        if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_collider       == null) _collider       = GetComponent<PolygonCollider2D>();

        Sprite sprite = _spriteRenderer?.sprite;
        if (sprite == null) return;

        int pathCount = sprite.GetPhysicsShapeCount();
        if (pathCount == 0)
        {
            Debug.LogWarning($"[SpriteCollider] '{name}': sprite has no physics shapes. " +
                             "Open the Sprite Editor → Physics Shape and generate or draw the outline.", this);
            return;
        }

        _lastSprite          = sprite;
        _collider.pathCount  = pathCount;

        List<Vector2> points = new List<Vector2>();
        for (int i = 0; i < pathCount; i++)
        {
            points.Clear();
            sprite.GetPhysicsShape(i, points);
            _collider.SetPath(i, points);
        }
    }
}
