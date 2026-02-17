using UnityEngine;

public class HitStop : MonoBehaviour
{
    public static HitStop Instance { get; private set; }
    
    [SerializeField] private float hitStopDuration = 0.08f;
    
    private float hitStopTimer;
    private float originalTimeScale;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Health playerHealth = player.GetComponent<Health>();
            if (playerHealth != null)
            {
                playerHealth.OnDamageTaken.AddListener(OnDamage);
            }
        }
    }
    
    void OnDamage(float damage)
    {
        TriggerHitStop();
    }
    
    public void TriggerHitStop()
    {
        if (hitStopTimer <= 0)
        {
            hitStopTimer = hitStopDuration;
            originalTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
    }
    
    void Update()
    {
        if (hitStopTimer > 0)
        {
            hitStopTimer -= Time.unscaledDeltaTime;
            if (hitStopTimer <= 0)
            {
                Time.timeScale = originalTimeScale;
            }
        }
    }
}
