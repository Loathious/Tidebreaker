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
            canvas = Object.FindFirstObjectByType<Canvas>();
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
        // DamageNumber.Awake() adds its own TextMeshPro in world-space — do NOT add a
        // TextMeshProUGUI here; Unity forbids two Graphic components on one GameObject
        // and AddComponent<TextMeshPro>() would throw a NullReferenceException.
        GameObject prefab = new GameObject("DamageNumber");
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
