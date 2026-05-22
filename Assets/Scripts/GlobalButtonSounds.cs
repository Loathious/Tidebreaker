using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays a click sound whenever any UI Button in the scene is pressed.
/// Add this component to the GameManager (or any persistent object) and
/// assign clickClip in the Inspector (or let JoaBuildTools wire it).
///
/// Works by scanning all Buttons at startup and adding an AudioSource-backed
/// listener. Re-scans after a short delay to catch buttons spawned at runtime.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class GlobalButtonSounds : MonoBehaviour
{
    [SerializeField] public AudioClip clickClip;
    [SerializeField] [Range(0f, 1f)] private float volume = 0.7f;

    private AudioSource _src;

    private void Awake()
    {
        _src               = GetComponent<AudioSource>();
        _src.playOnAwake   = false;
        _src.spatialBlend  = 0f; // 2D sound
    }

    private void Start()
    {
        HookAllButtons();
        // Re-scan after one frame to catch buttons enabled slightly later.
        StartCoroutine(LateHook());
    }

    private IEnumerator LateHook()
    {
        yield return null;
        HookAllButtons();
    }

    private void HookAllButtons()
    {
        foreach (Button btn in FindObjectsByType<Button>(FindObjectsInactive.Include,
                                                        FindObjectsSortMode.None))
        {
            // Avoid adding the listener more than once.
            btn.onClick.RemoveListener(PlayClick);
            btn.onClick.AddListener(PlayClick);
        }
    }

    private void PlayClick()
    {
        if (clickClip != null && _src != null)
            _src.PlayOneShot(clickClip, volume);
    }
}
