using UnityEngine;

/// <summary>
/// Camera shake that plays NICELY with CameraFollow.
///
/// Old version captured the start-of-game position and snapped the camera back
/// to it during shake — so any time the player had moved, taking damage felt
/// like a teleport back to spawn.
///
/// New version applies a small offset on top of whatever CameraFollow set this
/// frame, and undoes that offset the next frame before adding a fresh one.
/// Order is enforced via DefaultExecutionOrder so this always runs after
/// CameraFollow's LateUpdate (CameraFollow is at default 0; this is at 100).
/// </summary>
[DefaultExecutionOrder(100)]
public class CameraShake : MonoBehaviour
{
    [SerializeField] private float defaultShakeDuration = 0.12f;
    [SerializeField] private float defaultShakeAmount   = 0.06f;   // toned down — was 0.10

    private Vector3 _appliedOffset;
    private float   _shakeAmount;
    private float   _shakeTimer;

    void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Health playerHealth = player.GetComponent<Health>();
            if (playerHealth != null)
                playerHealth.OnDamageTaken.AddListener(OnPlayerDamaged);
        }
    }

    void OnPlayerDamaged(float damage)
    {
        Shake(defaultShakeAmount, defaultShakeDuration);
    }

    /// <summary>Triggers a camera shake. Multiple calls stack to the strongest.</summary>
    public void Shake(float amount, float duration)
    {
        _shakeAmount = Mathf.Max(_shakeAmount, amount);
        _shakeTimer  = Mathf.Max(_shakeTimer, duration);
    }

    void LateUpdate()
    {
        // STEP 1: undo last frame's shake offset so we don't accumulate
        // (CameraFollow ran already this frame and may or may not have overwritten
        //  position — either way, removing our prior offset is safe.)
        transform.position -= _appliedOffset;
        _appliedOffset = Vector3.zero;

        // STEP 2: if a shake is active, compute and apply a fresh offset
        if (_shakeTimer > 0f)
        {
            // Smoothstep falloff so the shake eases out instead of cutting
            float t = Mathf.Clamp01(_shakeTimer / Mathf.Max(defaultShakeDuration, 0.0001f));
            float falloff = t * t;

            Vector2 jitter = Random.insideUnitCircle * (_shakeAmount * falloff);
            _appliedOffset = new Vector3(jitter.x, jitter.y, 0f);
            transform.position += _appliedOffset;

            _shakeTimer -= Time.deltaTime;
            if (_shakeTimer <= 0f) _shakeAmount = 0f;
        }
    }
}
