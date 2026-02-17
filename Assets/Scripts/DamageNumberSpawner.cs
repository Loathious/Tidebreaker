using UnityEngine;
using TMPro;

public class DamageNumberSpawner : MonoBehaviour
{
    [SerializeField] private GameObject damageNumberPrefab;
    [SerializeField] private Canvas canvas;
    
    private Health health;
    private Camera mainCamera;
    
    void Start()
    {
        health = GetComponent<Health>();
        mainCamera = Camera.main;
        
        if (canvas == null)
        {
            canvas = FindObjectOfType<Canvas>();
        }
        
        if (health != null)
        {
            health.OnDamageTaken.AddListener(OnDamage);
        }
        
        if (damageNumberPrefab == null)
        {
            CreateDamageNumberPrefab();
        }
    }
    
    void CreateDamageNumberPrefab()
    {
        GameObject prefab = new GameObject("DamageNumber");
        TextMeshProUGUI text = prefab.AddComponent<TextMeshProUGUI>();
        text.fontSize = 16;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.outlineWidth = 0.2f;
        text.outlineColor = Color.black;
        
        RectTransform rect = prefab.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(100, 50);
        
        prefab.AddComponent<DamageNumber>();
        damageNumberPrefab = prefab;
    }
    
    void OnDamage(float damage)
    {
        if (damageNumberPrefab != null && canvas != null)
        {
            GameObject numberObj = Instantiate(damageNumberPrefab, canvas.transform);
            DamageNumber damageNum = numberObj.GetComponent<DamageNumber>();
            if (damageNum != null)
            {
                damageNum.Initialize((int)damage, transform.position, mainCamera);
            }
        }
    }
}
