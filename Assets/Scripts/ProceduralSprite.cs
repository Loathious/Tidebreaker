using UnityEngine;

/// <summary>
/// Generates small solid-colour sprites at runtime (coconuts, energy balls,
/// glow dots, square particles). Avoids needing extra art assets for tiny FX.
/// All sprites use Point filtering and 16 px-per-unit to match the pixel-art look.
/// </summary>
public static class ProceduralSprite
{
    /// <summary>A filled circle sprite of the given pixel diameter.</summary>
    public static Sprite Circle(int diameter, Color color)
    {
        diameter = Mathf.Max(2, diameter);
        Texture2D tex = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode   = TextureWrapMode.Clamp
        };
        float r = diameter / 2f;
        Vector2 c = new Vector2(r - 0.5f, r - 0.5f);
        for (int y = 0; y < diameter; y++)
            for (int x = 0; x < diameter; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c);
                tex.SetPixel(x, y, d <= r ? color : Color.clear);
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, diameter, diameter),
                             new Vector2(0.5f, 0.5f), 16f);
    }

    /// <summary>A solid filled square/rectangle sprite.</summary>
    public static Sprite Box(int w, int h, Color color)
    {
        w = Mathf.Max(1, w); h = Mathf.Max(1, h);
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode   = TextureWrapMode.Clamp
        };
        Color[] px = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = color;
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
    }

    private static Sprite _white;
    /// <summary>Shared 1×1 white sprite (for tints, bars, flashes).</summary>
    public static Sprite White()
    {
        if (_white != null) return _white;
        _white = Box(1, 1, Color.white);
        return _white;
    }
}
