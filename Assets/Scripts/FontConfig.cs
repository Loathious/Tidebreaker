using UnityEngine;
using TMPro;

/// <summary>
/// ScriptableObject that maps font roles to TMP font assets.
/// Create one via Assets → Create → Game → Font Config, place it in
/// Assets/Resources/ as "FontConfig", then assign fonts in the Inspector.
///
/// Attach <see cref="FontTag"/> to any text component to override which role it uses.
/// </summary>
[CreateAssetMenu(fileName = "FontConfig", menuName = "Game/Font Config")]
public class FontConfig : ScriptableObject
{
    [Header("Global Default")]
    [Tooltip("Applied to all text that has no specific FontTag role.")]
    public TMP_FontAsset defaultFont;

    [Header("Roles")]
    [Tooltip("Title screen, main menu heading.")]
    public TMP_FontAsset titleFont;

    [Tooltip("In-game HUD: hotbar numbers, objective text, health bars.")]
    public TMP_FontAsset hudFont;

    [Tooltip("NPC dialogue boxes and story panels.")]
    public TMP_FontAsset dialogFont;

    [Tooltip("Damage numbers and floating notifications.")]
    public TMP_FontAsset floatingTextFont;

    [Tooltip("Game-over and victory banners.")]
    public TMP_FontAsset bannerFont;

    // ── Singleton-style access via Resources ──────────────────────────────────
    private static FontConfig _instance;

    public static FontConfig Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = Resources.Load<FontConfig>("FontConfig");
            return _instance;
        }
    }

    /// <summary>Returns the font for the given role, falling back to defaultFont.</summary>
    public TMP_FontAsset GetFont(FontRole role)
    {
        TMP_FontAsset f = role switch
        {
            FontRole.Title        => titleFont,
            FontRole.HUD          => hudFont,
            FontRole.Dialog       => dialogFont,
            FontRole.FloatingText => floatingTextFont,
            FontRole.Banner       => bannerFont,
            _                     => null,
        };
        return f != null ? f : defaultFont;
    }
}

/// <summary>Which font role a text element should use.</summary>
public enum FontRole
{
    Default,
    Title,
    HUD,
    Dialog,
    FloatingText,
    Banner,
}
