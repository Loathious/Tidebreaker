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

        // Restore health and position
        float savedHealth = PlayerPrefs.GetFloat(KEY_HEALTH, 100f);
        float savedPosX   = PlayerPrefs.GetFloat(KEY_POS_X, float.MinValue);
        float savedPosY   = PlayerPrefs.GetFloat(KEY_POS_Y, float.MinValue);
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Health hp = player.GetComponent<Health>() ?? player.GetComponentInChildren<Health>();
            hp?.SetCurrentHealth(savedHealth);

            // Only warp to saved position when one was actually recorded
            if (savedPosX > float.MinValue + 1f)
            {
                var rb = player.GetComponent<Rigidbody2D>();
                Vector3 dest = new Vector3(savedPosX, savedPosY, 0f);
                player.transform.position = dest;
                if (rb != null) { rb.position = dest; rb.linearVelocity = Vector2.zero; }
            }
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
        // Search all assets currently loaded in memory (scene references, etc.)
        foreach (ItemData id in Resources.FindObjectsOfTypeAll<ItemData>())
            if (id != null && id.name == unityName) return id;

        // Also try Resources folder (assets placed in Assets/Resources/)
        ItemData loaded = Resources.Load<ItemData>(unityName);
        if (loaded != null) return loaded;

        // Fuzzy fallback: match by itemName field or partial name match
        foreach (ItemData id in Resources.FindObjectsOfTypeAll<ItemData>())
        {
            if (id == null) continue;
            if (string.Equals(id.itemName, unityName, System.StringComparison.OrdinalIgnoreCase)) return id;
            if (id.name.IndexOf(unityName, System.StringComparison.OrdinalIgnoreCase) >= 0) return id;
        }

        // Runtime fallback for Diamond Sword so the player never loads weaponless
        if (unityName.IndexOf("Diamond", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            var ds = ScriptableObject.CreateInstance<ItemData>();
            ds.name          = "DiamondSword";
            ds.itemName      = "Diamond Sword";
            ds.damage        = 35;
            ds.maxUses       = 0;
            ds.itemType      = ItemType.Weapon;
            ds.attackCooldown = 0.32f;
            return ds;
        }

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
        go.transform.SetAsLastSibling();
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot     = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-10f, 10f);
        rt.sizeDelta = new Vector2(130f, 20f);

        var tmp = go.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text      = "Game Saved";
        tmp.alignment = TMPro.TextAlignmentOptions.Right;
        tmp.fontSize  = 6.5f;
        tmp.color     = new Color(0.55f, 1f, 0.55f, 0f);
        tmp.outlineWidth = 0.15f;
        tmp.outlineColor = new Color32(0, 0, 0, 160);
        FontEnforcer.ApplyTo(tmp);

        float t = 0f;
        while (t < 0.35f) { t += Time.unscaledDeltaTime; tmp.color = new Color(0.55f, 1f, 0.55f, t / 0.35f); yield return null; }
        yield return new WaitForSecondsRealtime(1.2f);
        t = 0f;
        while (t < 0.6f) { t += Time.unscaledDeltaTime; tmp.color = new Color(0.55f, 1f, 0.55f, 1f - t / 0.6f); yield return null; }
        Destroy(go);
    }
}
