using UnityEngine;

/// <summary>
/// Drop this on an empty GameObject in the Jungle scene to define the boss arena bounds.
/// JungleManager reads this to set the monkey boss's patrol limits.
///
/// How to use:
///   1. Create an empty GameObject in the Jungle scene.
///   2. Add this component (it auto-adds a BoxCollider2D trigger).
///   3. Move and scale the GameObject in the Scene view to cover the arena.
///   4. JungleManager automatically picks it up — no other wiring needed.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class BossArenaBounds : MonoBehaviour
{
    void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    /// <summary>Returns the world-space left and right X limits for patrol clamping.</summary>
    public void GetBoundsX(out float minX, out float maxX)
    {
        BoxCollider2D bc = GetComponent<BoxCollider2D>();
        float halfW = bc.size.x * Mathf.Abs(transform.lossyScale.x) * 0.5f;
        float cx    = transform.position.x + bc.offset.x * transform.lossyScale.x;
        minX = cx - halfW;
        maxX = cx + halfW;
    }

    void OnDrawGizmosSelected()
    {
        BoxCollider2D bc = GetComponent<BoxCollider2D>();
        if (bc == null) return;

        float halfW = bc.size.x * Mathf.Abs(transform.lossyScale.x) * 0.5f;
        float halfH = bc.size.y * Mathf.Abs(transform.lossyScale.y) * 0.5f;
        float cx    = transform.position.x + bc.offset.x * transform.lossyScale.x;
        float cy    = transform.position.y + bc.offset.y * transform.lossyScale.y;

        Vector3 center = new Vector3(cx, cy, transform.position.z);
        Vector3 size   = new Vector3(halfW * 2f, halfH * 2f, 0.1f);

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
        Gizmos.DrawCube(center, size);
        Gizmos.color = new Color(1f, 0.55f, 0f, 1f);
        Gizmos.DrawWireCube(center, size);

#if UNITY_EDITOR
        UnityEditor.Handles.color = new Color(1f, 0.55f, 0f, 1f);
        UnityEditor.Handles.Label(center + Vector3.up * (halfH + 0.3f), "Boss Arena");
#endif
    }

    void OnDrawGizmos()
    {
        BoxCollider2D bc = GetComponent<BoxCollider2D>();
        if (bc == null) return;
        float halfW = bc.size.x * Mathf.Abs(transform.lossyScale.x) * 0.5f;
        float halfH = bc.size.y * Mathf.Abs(transform.lossyScale.y) * 0.5f;
        float cx    = transform.position.x + bc.offset.x * transform.lossyScale.x;
        float cy    = transform.position.y + bc.offset.y * transform.lossyScale.y;
        Vector3 center = new Vector3(cx, cy, transform.position.z);
        Vector3 size   = new Vector3(halfW * 2f, halfH * 2f, 0.1f);
        Gizmos.color = new Color(1f, 0.55f, 0f, 0.35f);
        Gizmos.DrawWireCube(center, size);
    }
}
