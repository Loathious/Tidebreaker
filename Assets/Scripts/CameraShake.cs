using UnityEngine;

public class CameraShake : MonoBehaviour
{
    [SerializeField] private float shakeDuration = 0.15f;
    [SerializeField] private float shakeAmount = 0.1f;
    
    private Vector3 originalPosition;
    private float shakeTimer;
    
    void Start()
    {
        originalPosition = transform.localPosition;
        
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Health playerHealth = player.GetComponent<Health>();
            if (playerHealth != null)
            {
                playerHealth.OnDamageTaken.AddListener(OnPlayerDamaged);
            }
        }
    }
    
    void OnPlayerDamaged(float damage)
    {
        shakeTimer = shakeDuration;
    }
    
    void LateUpdate()
    {
        if (shakeTimer > 0)
        {
            transform.localPosition = originalPosition + Random.insideUnitSphere * shakeAmount;
            shakeTimer -= Time.deltaTime;
        }
        else
        {
            transform.localPosition = originalPosition;
        }
    }
}
