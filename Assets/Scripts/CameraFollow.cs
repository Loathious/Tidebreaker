using System.Collections;
using UnityEngine;

/// <summary>
/// Smooth camera follow with:
/// — level bounds clamping (stops at map edges)
/// — dialogue speaker lock (camera eases to speaker, releases after dialogue)
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("Follow")]
    public Transform target;
    public Vector3   offset;
    [Range(0f, 20f)]
    public float smoothSpeed = 8f; // 0 = instant (old behaviour)

    [Header("Level Bounds")]
    public bool  useBounds = false;
    public float minX = -100f;
    public float maxX =  100f;
    public float minY = -50f;
    public float maxY =  50f;

    [Header("Speaker Lock")]
    [SerializeField] private float speakerLockSpeed = 4f;

    // ── Private ───────────────────────────────────────────────────────────────
    private Transform  _lockedSpeaker;
    private bool       _speakerLocked;
    private bool       _frozen;
    private Camera     _cam;

    void Start()
    {
        _cam = GetComponent<Camera>();
        if (target == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
        }
        if (target != null)
            transform.position = TargetPosition(target);
    }

    /// <summary>Stops the camera from following any target (called on player death).</summary>
    public void Freeze() => _frozen = true;
    public void Unfreeze() => _frozen = false;

    void LateUpdate()
    {
        if (_frozen) return;

        // If the serialized target was destroyed (e.g., DontDestroyOnLoad player swap on
        // scene restart), find the living player automatically.
        if (target == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
        }

        Transform followTarget = _speakerLocked && _lockedSpeaker != null
            ? _lockedSpeaker
            : target;

        if (followTarget == null) return;

        Vector3 desired  = TargetPosition(followTarget);
        float   speed    = _speakerLocked ? speakerLockSpeed : smoothSpeed;

        Vector3 smoothed = smoothSpeed > 0f
            ? Vector3.Lerp(transform.position, desired, speed * Time.deltaTime)
            : desired;

        transform.position = smoothed;
    }

    // ── Speaker lock ──────────────────────────────────────────────────────────
    /// <summary>Locks the camera onto a speaker transform during dialogue.</summary>
    public void LockToSpeaker(Transform speaker)
    {
        if (speaker == null) return;
        _lockedSpeaker = speaker;
        _speakerLocked = true;
    }

    /// <summary>Returns camera to following the player target.</summary>
    public void ReleaseSpeakerLock()
    {
        _speakerLocked = false;
        _lockedSpeaker = null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private Vector3 TargetPosition(Transform t)
    {
        float x = t.position.x + offset.x;
        float y = t.position.y + offset.y;

        if (useBounds && _cam != null)
        {
            float halfH = _cam.orthographicSize;
            float halfW = halfH * _cam.aspect;
            x = Mathf.Clamp(x, minX + halfW, maxX - halfW);
            y = Mathf.Clamp(y, minY + halfH, maxY - halfH);
        }

        return new Vector3(x, y, -10f);
    }

    void OnDrawGizmosSelected()
    {
        if (!useBounds) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(new Vector3(minX, minY), new Vector3(maxX, minY));
        Gizmos.DrawLine(new Vector3(maxX, minY), new Vector3(maxX, maxY));
        Gizmos.DrawLine(new Vector3(maxX, maxY), new Vector3(minX, maxY));
        Gizmos.DrawLine(new Vector3(minX, maxY), new Vector3(minX, minY));
    }
}
