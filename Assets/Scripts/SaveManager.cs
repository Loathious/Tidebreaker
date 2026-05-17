using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent save/load manager. Stores scene, health, and equipped item
/// in PlayerPrefs so the player can resume from the main menu.
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    const string KEY_HAS_SAVE   = "HasSave";
    const string KEY_SCENE      = "SaveScene";
    const string KEY_HEALTH     = "SaveHealth";
    const string KEY_POS_X      = "SavePosX";
    const string KEY_POS_Y      = "SavePosY";
    const string KEY_ITEM_NAME  = "SaveItemName";
    const string KEY_ITEM_USES  = "SaveItemUses";

    public bool HasSave => PlayerPrefs.GetInt(KEY_HAS_SAVE, 0) == 1;
    public string SavedScene => PlayerPrefs.GetString(KEY_SCENE, "Village");
    public bool IsLoadingFromSave { get; private set; }

    public void ConfirmLoadApplied() { IsLoadingFromSave = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoSpawn()
    {
        if (Instance != null) return;
        var go = new GameObject("__SaveManager");
        Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<SaveManager>();
    }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Save ─────────────────────────────────────────────────────────────────
    public void SaveGame()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == "MainMenu" || sceneName == "SplashScene") return;

        PlayerPrefs.SetString(KEY_SCENE, sceneName);
        PlayerPrefs.SetInt(KEY_HAS_SAVE, 1);

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Health hp = player.GetComponent<Health>() ?? player.GetComponentInChildren<Health>();
            if (hp != null) PlayerPrefs.SetFloat(KEY_HEALTH, hp.CurrentHealth);

            Vector3 pos = player.transform.position;
            PlayerPrefs.SetFloat(KEY_POS_X, pos.x);
            PlayerPrefs.SetFloat(KEY_POS_Y, pos.y);
        }

        if (Inventory.Instance != null)
        {
            ItemData equipped = Inventory.Instance.GetEquippedItem();
            string itemName = equipped != null ? equipped.name : "";
            PlayerPrefs.SetString(KEY_ITEM_NAME, itemName);
            PlayerPrefs.SetInt(KEY_ITEM_USES, PlayerPrefs.GetInt("WeaponCurrentUses", 0));
        }

        PlayerPrefs.Save();
        ShowSaveNotification();
    }

    // ── Load ─────────────────────────────────────────────────────────────────
    /// <summary>Loads the saved scene. Call ApplySavedState() from the scene manager's Start().</summary>
    public void LoadGame()
    {
        if (!HasSave) return;
        Time.timeScale = 1f;
        IsLoadingFromSave = true;
        // Clear inventory so the saved item can be properly restored
        Inventory.Instance?.ClearAll();
        SceneManager.LoadScene(SavedScene);
    }

    /// <summary>
    /// Called by scene managers (GameManager, CaveManager) in Start() to restore
    /// health and inventory from a loaded save. Does nothing if no save exists.
    /// </summary>
    public void ApplySavedState()
    {
        if (!HasSave) return;

        // Restore health
        float savedHealth = PlayerPrefs.GetFloat(KEY_HEALTH, 100f);
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Health hp = player.GetComponent<Health>() ?? player.GetComponentInChildren<Health>();
            hp?.SetCurrentHealth(savedHealth);
        }

        // Restore equipped weapon by name (scene assets are now in memory)
        string itemName = PlayerPrefs.GetString(KEY_ITEM_NAME, "");
        if (!string.IsNullOrEmpty(itemName) && Inventory.Instance != null)
        {
            // Inventory should already be empty (cleared above)
            ItemData item = FindItemByUnityName(itemName);
            if (item != null)
            {
                Inventory.Instance.AddItem(item);
                Inventory.Instance.ToggleEquip(0);
                int savedUses = PlayerPrefs.GetInt(KEY_ITEM_USES, 0);
                PlayerPrefs.SetInt("WeaponCurrentUses", savedUses);
            }
        }
    }

    // ── Delete ───────────────────────────────────────────────────────────────
    public void DeleteSave()
    {
        PlayerPrefs.DeleteKey(KEY_HAS_SAVE);
        PlayerPrefs.DeleteKey(KEY_SCENE);
        PlayerPrefs.DeleteKey(KEY_HEALTH);
        PlayerPrefs.DeleteKey(KEY_POS_X);
        PlayerPrefs.DeleteKey(KEY_POS_Y);
        PlayerPrefs.DeleteKey(KEY_ITEM_NAME);
        PlayerPrefs.DeleteKey(KEY_ITEM_USES);
        // Clear per-run unlocks so new runs start without prior-session gear
        PlayerPrefs.DeleteKey("PlayerHasArmor");
        PlayerPrefs.DeleteKey("PlayerHasBow");
        PlayerPrefs.DeleteKey("WeaponCurrentUses");
        PlayerPrefs.Save();
    }

    // ── Accessors (legacy compat) ─────────────────────────────────────────────
    public Vector3 GetSavedPosition() => new Vector3(
        PlayerPrefs.GetFloat(KEY_POS_X, 0f),
        PlayerPrefs.GetFloat(KEY_POS_Y, 0f), 0f);

    public float GetSavedHealth() => PlayerPrefs.GetFloat(KEY_HEALTH, 100f);

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static ItemData FindItemByUnityName(string unityName)
    {
        // Resources.FindObjectsOfTypeAll finds assets already loaded into memory
        // (scene assets are loaded at this point since the scene just loaded)
        foreach (ItemData id in Resources.FindObjectsOfTypeAll<ItemData>())
            if (id != null && id.name == unityName) return id;
        return null;
    }

    private void ShowSaveNotification()
    {
        StartCoroutine(SaveNotificationCoroutine());
    }

    private System.Collections.IEnumerator SaveNotificationCoroutine()
    {
        Canvas canvas = null;
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c.renderMode == RenderMode.ScreenSpaceOverlay) { canvas = c; break; }
        if (canvas == null) yield break;

        var go = new GameObject("SaveNotification");
        go.transform.SetParent(canvas.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 30f);
        rt.sizeDelta = new Vector2(250f, 30f);

        var tmp = go.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text      = "Game Saved";
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.fontSize  = 9f;
        tmp.color     = new Color(0.4f, 1f, 0.4f, 0f);
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = new Color32(0, 0, 0, 200);
        FontEnforcer.ApplyTo(tmp);

        float t = 0f;
        while (t < 0.4f) { t += Time.unscaledDeltaTime; tmp.color = new Color(0.4f, 1f, 0.4f, t / 0.4f); yield return null; }
        yield return new WaitForSecondsRealtime(1.4f);
        t = 0f;
        while (t < 0.5f) { t += Time.unscaledDeltaTime; tmp.color = new Color(0.4f, 1f, 0.4f, 1f - t / 0.5f); yield return null; }
        Destroy(go);
    }
}
