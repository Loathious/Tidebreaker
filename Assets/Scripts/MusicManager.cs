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
        // A duplicate from a newly-loaded scene removes only its own component,
        // never the whole GameObject — the level manager / HitStop share this
        // object and must survive scene-to-scene transitions.
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance  = this;
        DontDestroyOnLoad(gameObject);
        _source   = GetComponent<AudioSource>();

        _source.clip       = musicClip;
        _source.loop       = true;
        _source.volume     = volume;
        _source.playOnAwake = false;
        _source.spatialBlend = 0f;
    }

    void Start()
    {
        if (_source != null && _source.clip != null) _source.Play();
    }

    /// <summary>
    /// Reconfigures the (possibly persistent) music manager for a new level and
    /// starts the ambient track. Level managers call this in Start() so the right
    /// music plays even though the MusicManager singleton survives scene loads.
    /// </summary>
    public void ConfigureAndPlay(AudioClip ambient, AudioClip combat)
    {
        musicClip       = ambient;
        combatMusicClip = combat;
        _source.clip    = ambient;
        _source.loop    = true;
        _source.volume  = volume;
        if (ambient != null) { _source.Stop(); _source.Play(); }
    }

    /// <summary>Smoothly sets the music volume at runtime.</summary>
    public void SetVolume(float v) { volume = Mathf.Clamp01(v); _source.volume = volume; }

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
