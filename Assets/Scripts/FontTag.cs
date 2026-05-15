using UnityEngine;
using TMPro;

/// <summary>
/// Attach to any GameObject with a TMP_Text component to pin it to a specific
/// font role from <see cref="FontConfig"/>.  If FontConfig has no asset for the
/// role, falls back to FontConfig.defaultFont, then to the PressStart2P SDF font.
///
/// Usage: Add component → pick Role in Inspector → done.
/// FontEnforcer will skip any text that already has a FontTag applied.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class FontTag : MonoBehaviour
{
    [SerializeField] private FontRole role = FontRole.Default;

    [Tooltip("Optional: directly assign a font asset here to override the FontConfig role lookup.")]
    [SerializeField] private TMP_FontAsset overrideFont;

    void Awake() => Apply();

    void OnValidate() => Apply();

    public void Apply()
    {
        TMP_Text t = GetComponent<TMP_Text>();
        if (t == null) return;

        TMP_FontAsset font = overrideFont;

        if (font == null && FontConfig.Instance != null)
            font = FontConfig.Instance.GetFont(role);

        if (font == null)
            font = FontEnforcer.Font;

        if (font != null && t.font != font)
            t.font = font;
    }
}
