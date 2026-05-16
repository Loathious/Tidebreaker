using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight code-driven frame animator. Holds named clips (arrays of sprites)
/// and plays them on the attached SpriteRenderer. Used by every new enemy / boss
/// so each sprite has proper per-state animations (idle / walk / attack / hurt / die)
/// without needing fragile Animator Controller assets.
///
/// Enemy AI scripts call Play("walk"), Play("attack"), etc.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteAnimator : MonoBehaviour
{
    [System.Serializable]
    public class Clip
    {
        public string    name;
        public Sprite[]  frames;
        public float     fps  = 8f;
        public bool      loop = true;
    }

    [Tooltip("All animation states for this sprite.")]
    public List<Clip> clips = new List<Clip>();

    [Tooltip("Clip played automatically on Start.")]
    public string defaultClip = "idle";

    private SpriteRenderer _sr;
    private Clip  _current;
    private int   _frame;
    private float _timer;
    private bool  _finished;

    /// <summary>True once a non-looping clip has shown its last frame.</summary>
    public bool   IsFinished  => _finished;
    public string CurrentClip => _current != null ? _current.name : "";

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        if (_current == null && !string.IsNullOrEmpty(defaultClip))
            Play(defaultClip);
    }

    /// <summary>Adds or replaces a clip at runtime.</summary>
    public void AddClip(string name, Sprite[] frames, float fps = 8f, bool loop = true)
    {
        if (frames == null || frames.Length == 0) return;
        clips.RemoveAll(c => c.name == name);
        clips.Add(new Clip { name = name, frames = frames, fps = fps, loop = loop });
    }

    /// <summary>Returns true if a clip with this name exists and has frames.</summary>
    public bool HasClip(string name)
    {
        Clip c = clips.Find(x => x.name == name);
        return c != null && c.frames != null && c.frames.Length > 0;
    }

    /// <summary>Starts playing the named clip. No-op if already playing it (unless forceRestart).</summary>
    public void Play(string name, bool forceRestart = false)
    {
        if (_current != null && _current.name == name && !forceRestart) return;

        Clip c = clips.Find(x => x.name == name);
        if (c == null || c.frames == null || c.frames.Length == 0) return;

        _current  = c;
        _frame    = 0;
        _timer    = 0f;
        _finished = false;
        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        if (_sr != null) _sr.sprite = c.frames[0];
    }

    void Update()
    {
        if (_current == null || _current.frames.Length <= 1) return;
        if (_finished) return;

        _timer += Time.deltaTime;
        float frameTime = 1f / Mathf.Max(1f, _current.fps);
        if (_timer < frameTime) return;

        _timer -= frameTime;
        _frame++;

        if (_frame >= _current.frames.Length)
        {
            if (_current.loop)
            {
                _frame = 0;
            }
            else
            {
                _frame    = _current.frames.Length - 1;
                _finished = true;
            }
        }

        if (_sr != null) _sr.sprite = _current.frames[_frame];
    }
}
