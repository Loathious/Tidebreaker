using UnityEngine;

public class CharacterShadow : MonoBehaviour
{
    [SerializeField] private Color shadowColor = new Color(0, 0, 0, 0.5f);
    [SerializeField] private Vector2 shadowSize = new Vector2(0.8f, 0.1f);
    [SerializeField] private Vector2 shadowOffset = new Vector2(0, -0.5f);
    
    private GameObject shadowObject;
    private SpriteRenderer shadowRenderer;
    
    void Start()
    {
        CreateShadow();
    }
    
    void CreateShadow()
    {
        shadowObject = new GameObject("Shadow");
        shadowObject.transform.SetParent(transform);
        shadowObject.transform.localPosition = new Vector3(shadowOffset.x, shadowOffset.y, 0.01f);
        
        shadowRenderer = shadowObject.AddComponent<SpriteRenderer>();
        shadowRenderer.sortingOrder = -1;
        
        Texture2D shadowTexture = new Texture2D(8, 1);
        for (int x = 0; x < 8; x++)
        {
            float alpha = 1f - (Mathf.Abs(x - 4f) / 4f);
            Color pixelColor = shadowColor;
            pixelColor.a *= alpha;
            shadowTexture.SetPixel(x, 0, pixelColor);
        }
        shadowTexture.filterMode = FilterMode.Point;
        shadowTexture.Apply();
        
        Sprite shadowSprite = Sprite.Create(shadowTexture, new Rect(0, 0, 8, 1), new Vector2(0.5f, 0.5f), 8f);
        shadowRenderer.sprite = shadowSprite;
        
        shadowObject.transform.localScale = new Vector3(shadowSize.x, shadowSize.y, 1);
    }
}
