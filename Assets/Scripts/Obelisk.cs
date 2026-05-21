using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using TMPro;

/// <summary>
/// An ancient Obelisk in the Desert level (Level 4). Three of them must be
/// activated to open the pyramid. Each obelisk stays locked until the
/// DesertManager reports its challenge complete; the player then presses E.
/// Builds its own pillar + crystal visuals procedurally.
/// </summary>
public class Obelisk : MonoBehaviour
{
    [Tooltip("Obelisk number 1-3.")]
    public int index = 1;

    [Header("Audio")]
    public AudioClip activateClip;

    private bool _challengeDone;
    private bool _activated;
    private bool _playerNearby;

    private SpriteRenderer _crystal;
    private Light2D        _light;
    private TextMeshPro    _hint;

    private static readonly Color Locked   = new Color(0.3f, 0.3f, 0.35f);
    private static readonly Color Ready    = new Color(1f, 0.85f, 0.3f);
    private static readonly Color Active   = new Color(0.5f, 1f, 1f);

    public bool IsActivated => _activated;

    void Awake()
    {
        BuildVisual();
        BuildHint();
    }

    void Update()
    {
        if (_activated) return;

        // Idle pulse once the challenge is done
        if (_challengeDone && _crystal != null)
        {
            float p = 0.6f + 0.4f * Mathf.Sin(Time.time * 4f);
            _crystal.color = Color.Lerp(Ready * 0.6f, Ready, p);
            if (_light != null) _light.intensity = 0.6f + 0.5f * p;
        }

        if (_challengeDone && _playerNearby && Input.GetKeyDown(KeyCode.E))
            Activate();
    }

    /// <summary>Called by DesertManager when this obelisk's challenge is solved.</summary>
    public void SetChallengeComplete()
    {
        if (_challengeDone || _activated) return;
        _challengeDone = true;
        if (_crystal != null) _crystal.color = Ready;
        if (_light != null) { _light.color = Ready; _light.intensity = 1f; }
        if (_hint != null && _playerNearby)
        {
            _hint.text = "Press E to activate the Obelisk";
            _hint.gameObject.SetActive(true);
        }
        StartCoroutine(ReadyFlash());
    }

    private void Activate()
    {
        _activated = true;
        if (_hint != null) _hint.gameObject.SetActive(false);
        if (_crystal != null) _crystal.color = Active;
        if (_light != null)
        {
            _light.color = Active;
            _light.intensity = 2f;
            _light.pointLightOuterRadius = 9f;
        }
        if (activateClip != null) SettingsManager.PlaySfxAt(activateClip, transform.position, 1f);

        StartCoroutine(ActivateBeam());
        (LevelManagerBase.Current as DesertManager)?.OnObeliskActivated(this);
    }

    private IEnumerator ActivateBeam()
    {
        // A bright beam shoots up from the crystal
        GameObject beam = new GameObject("Beam");
        beam.transform.SetParent(transform, false);
        beam.transform.localPosition = new Vector3(0f, 4f, 0f);
        var sr = beam.AddComponent<SpriteRenderer>();
        sr.sprite       = ProceduralSprite.Box(2, 64, Active);
        sr.sortingOrder = 6;
        beam.transform.localScale = new Vector3(0.5f, 0f, 1f);

        float t = 0f;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            float k = t / 0.6f;
            beam.transform.localScale = new Vector3(0.5f + 0.5f * Mathf.Sin(t * 20f), k, 1f);
            sr.color = new Color(Active.r, Active.g, Active.b, 0.8f * (1f - k * 0.5f));
            yield return null;
        }
        // Fade out
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime;
            sr.color = new Color(Active.r, Active.g, Active.b, 0.4f * (1f - t));
            yield return null;
        }
        Destroy(beam);
    }

    private IEnumerator ReadyFlash()
    {
        if (_crystal == null) yield break;
        for (int i = 0; i < 3; i++)
        {
            _crystal.color = Color.white;
            yield return new WaitForSeconds(0.1f);
            _crystal.color = Ready;
            yield return new WaitForSeconds(0.1f);
        }
    }

    // â”€â”€ Build visuals â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private void BuildVisual()
    {
        // Stone pillar
        GameObject pillar = new GameObject("Pillar");
        pillar.transform.SetParent(transform, false);
        var psr = pillar.AddComponent<SpriteRenderer>();
        psr.sprite       = ProceduralSprite.Box(14, 64, new Color(0.55f, 0.45f, 0.32f));
        psr.sortingOrder = 4;
        pillar.transform.localPosition = new Vector3(0f, 2f, 0f);

        // Darker base
        GameObject baseGO = new GameObject("Base");
        baseGO.transform.SetParent(transform, false);
        var bsr = baseGO.AddComponent<SpriteRenderer>();
        bsr.sprite       = ProceduralSprite.Box(24, 12, new Color(0.4f, 0.32f, 0.22f));
        bsr.sortingOrder = 5;
        baseGO.transform.localPosition = new Vector3(0f, 0.2f, 0f);

        // Crystal on top
        GameObject crystalGO = new GameObject("Crystal");
        crystalGO.transform.SetParent(transform, false);
        _crystal = crystalGO.AddComponent<SpriteRenderer>();
        _crystal.sprite       = ProceduralSprite.Circle(12, Locked);
        _crystal.color        = Locked;
        _crystal.sortingOrder = 6;
        crystalGO.transform.localPosition = new Vector3(0f, 4f, 0f);

        // Light
        GameObject lightGO = new GameObject("ObeliskLight");
        lightGO.transform.SetParent(transform, false);
        lightGO.transform.localPosition = new Vector3(0f, 4f, 0f);
        _light = lightGO.AddComponent<Light2D>();
        _light.lightType = Light2D.LightType.Point;
        _light.color = Locked;
        _light.intensity = 0.3f;
        _light.pointLightOuterRadius = 4f;

        // Interaction trigger
        var col = gameObject.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size   = new Vector2(4.5f, 6f);
        col.offset = new Vector2(0f, 2.5f);
    }

    private void BuildHint()
    {
        GameObject hintGO = new GameObject("ObeliskHint");
        hintGO.transform.SetParent(transform, false);
        hintGO.transform.localPosition = new Vector3(0f, 6f, 0f);
        hintGO.SetActive(false);

        _hint = hintGO.AddComponent<TextMeshPro>();
        _hint.text      = "Complete the trial";
        _hint.fontSize  = 2f;
        _hint.color     = new Color(1f, 0.95f, 0.7f);
        _hint.alignment = TextAlignmentOptions.Center;
        var rt = _hint.rectTransform;
        rt.sizeDelta = new Vector2(10f, 2f);

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/PressStart2P-Regular SDF")
                          ?? Resources.Load<TMP_FontAsset>("PressStart2P-Regular SDF");
        if (font != null) _hint.font = font;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        _playerNearby = true;
        if (_hint == null) return;
        _hint.text = _activated ? "Obelisk activated"
                   : _challengeDone ? "Press E to activate the Obelisk"
                   : "This obelisk is sealed â€” complete its trial";
        _hint.gameObject.SetActive(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        _playerNearby = false;
        if (_hint != null) _hint.gameObject.SetActive(false);
    }
}
