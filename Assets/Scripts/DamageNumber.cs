using UnityEngine;
using TMPro;

public class DamageNumber : MonoBehaviour
{
    [SerializeField] private float lifetime = 1f;
    [SerializeField] private float moveSpeed = 1f;
    
    private TextMeshProUGUI text;
    private float timer;
    
    void Awake()
    {
        text = GetComponent<TextMeshProUGUI>();
    }
    
    public void Initialize(int damage, Vector3 worldPosition, Camera cam)
    {
        text.text = damage.ToString();
        
        Vector3 screenPos = cam.WorldToScreenPoint(worldPosition);
        transform.position = screenPos;
        
        timer = lifetime;
    }
    
    void Update()
    {
        transform.position += Vector3.up * moveSpeed * Time.deltaTime * 100f;
        
        timer -= Time.deltaTime;
        float alpha = Mathf.Clamp01(timer / lifetime);
        Color color = text.color;
        color.a = alpha;
        text.color = color;
        
        if (timer <= 0)
        {
            Destroy(gameObject);
        }
    }
}
