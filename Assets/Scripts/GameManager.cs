using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    [Header("References")]
    [SerializeField] private Health playerHealth;
    [SerializeField] private GameObject gameOverUI;
    [SerializeField] private TextMeshProUGUI gameOverText;
    [SerializeField] private Button restartButton;
    [SerializeField] private Image screenTint;
    [SerializeField] private HealthBar[] healthBarsToHide;
    
    [Header("Death Settings")]
    [SerializeField] private float restartButtonDelay = 1f;
    [SerializeField] private Color deathTintColor = new Color(1f, 0f, 0f, 0.3f);
    [SerializeField] private float tintDuration = 0.5f;
    
    private bool isGameOver = false;
    
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
        if (gameOverUI != null)
        {
            gameOverUI.SetActive(false);
        }
        
        if (screenTint != null)
        {
            screenTint.color = new Color(1f, 0f, 0f, 0f);
        }
        
        if (playerHealth != null)
        {
            playerHealth.OnDeath.AddListener(OnPlayerDeath);
        }
        
        if (restartButton != null)
        {
            restartButton.onClick.AddListener(RestartGame);
        }
        
        Time.timeScale = 1f;
    }
    
    void OnPlayerDeath()
    {
        if (isGameOver) return;
        
        isGameOver = true;
        Time.timeScale = 0f;
        
        foreach (HealthBar healthBar in healthBarsToHide)
        {
            if (healthBar != null)
            {
                healthBar.Hide();
            }
        }
        
        if (screenTint != null)
        {
            StartCoroutine(FadeTint());
        }
        
        if (gameOverUI != null)
        {
            gameOverUI.SetActive(true);
            
            CanvasGroup canvasGroup = gameOverUI.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameOverUI.AddComponent<CanvasGroup>();
            }
            
            canvasGroup.alpha = 0f;
            StartCoroutine(FadeInUI(canvasGroup));
        }
        
        if (gameOverText != null)
        {
            gameOverText.transform.localScale = Vector3.zero;
            StartCoroutine(ScaleInText(gameOverText.gameObject, 0.2f));
        }
        
        if (restartButton != null)
        {
            restartButton.gameObject.SetActive(false);
            StartCoroutine(ShowRestartButton());
        }
    }
    
    System.Collections.IEnumerator FadeTint()
    {
        float duration = tintDuration;
        float elapsed = 0f;
        Color startColor = new Color(1f, 0f, 0f, 0f);
        
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            Color color = deathTintColor;
            color.a = Mathf.Lerp(0f, deathTintColor.a, elapsed / duration);
            screenTint.color = color;
            yield return null;
        }
        
        screenTint.color = deathTintColor;
    }
    
    System.Collections.IEnumerator FadeInUI(CanvasGroup canvasGroup)
    {
        float duration = 0.5f;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / duration);
            yield return null;
        }
        
        canvasGroup.alpha = 1f;
    }
    
    System.Collections.IEnumerator ScaleInText(GameObject obj, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        
        float duration = 0.5f;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            float scale = Mathf.Sin(t * Mathf.PI * 0.5f);
            obj.transform.localScale = Vector3.one * scale * 1.2f;
            yield return null;
        }
        
        obj.transform.localScale = Vector3.one;
    }
    
    System.Collections.IEnumerator ShowRestartButton()
    {
        yield return new WaitForSecondsRealtime(restartButtonDelay);
        
        restartButton.gameObject.SetActive(true);
        restartButton.transform.localScale = Vector3.zero;
        
        float duration = 0.3f;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            float scale = Mathf.Sin(t * Mathf.PI * 0.5f);
            restartButton.transform.localScale = Vector3.one * scale * 1.1f;
            yield return null;
        }
        
        restartButton.transform.localScale = Vector3.one;
    }
    
    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
