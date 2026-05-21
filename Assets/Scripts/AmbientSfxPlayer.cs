using UnityEngine;

/// <summary>
/// Plays random one-shot ambient sounds at random intervals â€” cave drips,
/// jungle calls, desert wind, storm thunder. Adds atmosphere to each level.
/// Clips are assigned by the level builder.
/// </summary>
public class AmbientSfxPlayer : MonoBehaviour
{
    [Tooltip("Pool of ambient clips; one is picked at random each time.")]
    public AudioClip[] clips;

    [Header("Timing")]
    public float minInterval = 4f;
    public float maxInterval = 11f;

    [Range(0f, 1f)] public float volume = 0.5f;

    private float _timer;

    void Start()
    {
        _timer = Random.Range(minInterval, maxInterval);
    }

    void Update()
    {
        if (clips == null || clips.Length == 0) return;

        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            _timer = Random.Range(minInterval, maxInterval);
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip != null)
            {
                Vector3 at = Camera.main != null ? Camera.main.transform.position : transform.position;
                SettingsManager.PlaySfxAt(clip, at, volume);
            }
        }
    }
}
