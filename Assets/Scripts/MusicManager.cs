using UnityEngine;

/// <summary>
/// Plays background music for the scene. Loops automatically.
/// Attach to any persistent GameObject in the scene.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [SerializeField] private AudioClip musicClip;
    [SerializeField] private AudioClip combatMusicClip;
    [SerializeField] [Range(0f, 1f)] private float volume = 0.5f;

    private AudioSource _source;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance  = this;
        DontDestroyOnLoad(gameObject);
        _source   = GetComponent<AudioSource>();

        _source.clip       = musicClip;
        _source.loop       = true;
        _source.volume     = volume;
        _source.playOnAwake = false;
        _source.spatialBlend = 0f;
    }

    void Start() => _source.Play();

    /// <summary>Smoothly sets the music volume at runtime.</summary>
    public void SetVolume(float v) => _source.volume = Mathf.Clamp01(v);

    /// <summary>Stops the music immediately.</summary>
    public void Stop() => _source.Stop();

    /// <summary>Resumes playback from where it stopped.</summary>
    public void Resume() { if (!_source.isPlaying) _source.Play(); }

    /// <summary>Switches to combat music, restarting the audio source.</summary>
    public void SwitchToCombat()
    {
        _source.Stop();
        _source.clip = combatMusicClip;
        _source.Play();
    }

    /// <summary>Switches back to the ambient music clip, restarting the audio source.</summary>
    public void SwitchToAmbient()
    {
        _source.Stop();
        _source.clip = musicClip;
        _source.Play();
    }
}
