using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One-shot editor tool that wires up all the missing UI panels and
/// component references in the Village scene.
/// Run via: Tools → Village Scene Setup
/// </summary>
public static class VillageSceneSetup
{
    [MenuItem("Tools/Village Scene Setup")]
    public static void Setup()
    {
        if (!Application.isEditor)
        {
            Debug.LogError("Run this from the Editor only.");
            return;
        }

        var gameCanvas = FindOrCreate("GameCanvas");
        var canvasComp = gameCanvas.GetComponent<Canvas>();
        if (canvasComp == null) canvasComp = gameCanvas.AddComponent<Canvas>();
        canvasComp.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasComp.sortingOrder = 100;

        var scaler = gameCanvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode          = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution  = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight   = 0.5f;

        if (gameCanvas.GetComponent<GraphicRaycaster>() == null)
            gameCanvas.AddComponent<GraphicRaycaster>();

        // ── 1. Player Health Bar ─────────────────────────────────────────────
        var healthBarGO = FindOrCreateChild(gameCanvas, "PlayerHealthBar");
        SetAnchor(healthBarGO, new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -10), new Vector2(220, 30));

        var hbBg = healthBarGO.GetComponent<Image>() ?? healthBarGO.AddComponent<Image>();
        hbBg.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);

        var fillGO   = FindOrCreateChild(healthBarGO, "Fill");
        SetAnchorStretch(fillGO, new Vector2(0, 0), new Vector2(1, 1), new Vector2(4, 4), new Vector2(-4, -4));
        var fillImg  = fillGO.GetComponent<Image>() ?? fillGO.AddComponent<Image>();
        fillImg.color      = new Color(0.9f, 0.15f, 0.15f, 1f);
        fillImg.type       = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = 0;

        var hpTextGO = FindOrCreateChild(healthBarGO, "HPText");
        SetAnchorStretch(hpTextGO, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var hpTxt = hpTextGO.GetComponent<TextMeshProUGUI>() ?? hpTextGO.AddComponent<TextMeshProUGUI>();
        hpTxt.text      = "100";
        hpTxt.fontSize  = 14;
        hpTxt.alignment = TextAlignmentOptions.Center;
        hpTxt.color     = Color.white;

        // ── 2. Hotbar ────────────────────────────────────────────────────────
        var hotbarGO = FindOrCreateChild(gameCanvas, "Hotbar");
        SetAnchor(hotbarGO, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(60, 60));
        if (hotbarGO.GetComponent<HotbarUI>() == null) hotbarGO.AddComponent<HotbarUI>();

        // ── 3. Objective Panel (top-right) ───────────────────────────────────
        var objPanel = FindOrCreateChild(gameCanvas, "ObjectivePanel");
        SetAnchor(objPanel, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-260, -10), new Vector2(250, 120));
        var objBg = objPanel.GetComponent<Image>() ?? objPanel.AddComponent<Image>();
        objBg.color = new Color(0, 0, 0, 0.5f);

        var objTextGO = FindOrCreateChild(objPanel, "ObjectiveText");
        SetAnchorStretch(objTextGO, Vector2.zero, Vector2.one, new Vector2(6, 6), new Vector2(-6, -6));
        var objTxt = objTextGO.GetComponent<TextMeshProUGUI>() ?? objTextGO.AddComponent<TextMeshProUGUI>();
        objTxt.text      = "";
        objTxt.fontSize  = 13;
        objTxt.alignment = TextAlignmentOptions.TopLeft;
        objTxt.color     = Color.white;

        // ── 4. Tutorial Hint (bottom-centre) ─────────────────────────────────
        var tutGO = FindOrCreateChild(gameCanvas, "TutorialHint");
        SetAnchor(tutGO, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 80), new Vector2(400, 40));
        var tutTxt = tutGO.GetComponent<TextMeshProUGUI>() ?? tutGO.AddComponent<TextMeshProUGUI>();
        tutTxt.text      = "";
        tutTxt.fontSize  = 18;
        tutTxt.alignment = TextAlignmentOptions.Center;
        tutTxt.color     = new Color(1f, 0.95f, 0.5f, 1f);

        // ── 5. Message (centre screen) ───────────────────────────────────────
        var msgGO = FindOrCreateChild(gameCanvas, "MessageText");
        SetAnchor(msgGO, new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.6f), Vector2.zero, new Vector2(600, 60));
        var msgTxt = msgGO.GetComponent<TextMeshProUGUI>() ?? msgGO.AddComponent<TextMeshProUGUI>();
        msgTxt.text      = "";
        msgTxt.fontSize  = 22;
        msgTxt.fontStyle = FontStyles.Bold;
        msgTxt.alignment = TextAlignmentOptions.Center;
        msgTxt.color     = new Color(1f, 0.9f, 0.3f, 1f);

        // ── 6. ObjectiveUI component ─────────────────────────────────────────
        var objUiGO = FindOrCreate("ObjectiveUI");
        var objUi   = objUiGO.GetComponent<ObjectiveUI>() ?? objUiGO.AddComponent<ObjectiveUI>();
        SetField(objUi, "objectiveText",    objTxt);
        SetField(objUi, "tutorialHintText", tutTxt);
        SetField(objUi, "messageText",      msgTxt);

        // ── 7. Dialog Panel ──────────────────────────────────────────────────
        var dialogPanel = FindOrCreateChild(gameCanvas, "DialogPanel");
        dialogPanel.SetActive(false);
        SetAnchor(dialogPanel, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 160));
        var dpBg = dialogPanel.GetComponent<Image>() ?? dialogPanel.AddComponent<Image>();
        dpBg.color = new Color(0.05f, 0.05f, 0.1f, 0.92f);

        var nameGO = FindOrCreateChild(dialogPanel, "SpeakerName");
        SetAnchor(nameGO, new Vector2(0, 1), new Vector2(0.4f, 1), new Vector2(10, -50), new Vector2(-10, 40));
        var nameTxt = nameGO.GetComponent<TextMeshProUGUI>() ?? nameGO.AddComponent<TextMeshProUGUI>();
        nameTxt.text      = "Villager";
        nameTxt.fontSize  = 16;
        nameTxt.fontStyle = FontStyles.Bold;
        nameTxt.color     = new Color(1f, 0.9f, 0.4f, 1f);

        var bodyGO = FindOrCreateChild(dialogPanel, "DialogBody");
        SetAnchor(bodyGO, Vector2.zero, Vector2.one, new Vector2(10, 10), new Vector2(-10, -50));
        var bodyTxt = bodyGO.GetComponent<TextMeshProUGUI>() ?? bodyGO.AddComponent<TextMeshProUGUI>();
        bodyTxt.text             = "";
        bodyTxt.fontSize         = 14;
        bodyTxt.textWrappingMode = TMPro.TextWrappingModes.Normal;
        bodyTxt.color            = Color.white;

        var continueGO = FindOrCreateChild(dialogPanel, "ContinuePrompt");
        SetAnchor(continueGO, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-200, 6), new Vector2(190, 20));
        var continueTxt = continueGO.GetComponent<TextMeshProUGUI>() ?? continueGO.AddComponent<TextMeshProUGUI>();
        continueTxt.text      = "[ E / Click to continue ]";
        continueTxt.fontSize  = 11;
        continueTxt.alignment = TextAlignmentOptions.Right;
        continueTxt.color     = new Color(1f, 1f, 1f, 0.5f);

        // ── 8. DialogUI on the first villager ────────────────────────────────
        var v1 = GameObject.Find("Villager1");
        if (v1 != null)
        {
            var dialogUi = v1.GetComponent<DialogUI>();
            if (dialogUi == null) dialogUi = v1.AddComponent<DialogUI>();
            SetField(dialogUi, "dialogRoot",     dialogPanel);
            SetField(dialogUi, "speakerNameText", nameTxt);
            SetField(dialogUi, "dialogBodyText",  bodyTxt);
            // Link hotbarUI
            var hotbarUi = hotbarGO.GetComponent<HotbarUI>();
            if (hotbarUi != null) SetField(dialogUi, "hotbarUI", hotbarUi);
        }

        var v2 = GameObject.Find("Villager2");
        if (v2 != null)
        {
            var dialogUi = v2.GetComponent<DialogUI>();
            if (dialogUi == null) dialogUi = v2.AddComponent<DialogUI>();
            SetField(dialogUi, "dialogRoot",      dialogPanel);
            SetField(dialogUi, "speakerNameText", nameTxt);
            SetField(dialogUi, "dialogBodyText",  bodyTxt);
            var hotbarUi = hotbarGO.GetComponent<HotbarUI>();
            if (hotbarUi != null) SetField(dialogUi, "hotbarUI", hotbarUi);
        }

        // ── 9. Screen Tint ───────────────────────────────────────────────────
        var tintGO = FindOrCreateChild(gameCanvas, "ScreenTint");
        SetAnchorStretch(tintGO, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var tintImg = tintGO.GetComponent<Image>() ?? tintGO.AddComponent<Image>();
        tintImg.color = new Color(1f, 0f, 0f, 0f);
        tintImg.raycastTarget = false;

        // ── 10. Game Over Panel ──────────────────────────────────────────────
        var goPanel = FindOrCreateChild(gameCanvas, "GameOverPanel");
        goPanel.SetActive(false);
        SetAnchorStretch(goPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var goBg = goPanel.GetComponent<Image>() ?? goPanel.AddComponent<Image>();
        goBg.color = new Color(0f, 0f, 0f, 0.75f);

        var goTextGO = FindOrCreateChild(goPanel, "GameOverText");
        SetAnchor(goTextGO, new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.6f), Vector2.zero, new Vector2(500, 80));
        var goTxt = goTextGO.GetComponent<TextMeshProUGUI>() ?? goTextGO.AddComponent<TextMeshProUGUI>();
        goTxt.text      = "GAME OVER";
        goTxt.fontSize  = 52;
        goTxt.fontStyle = FontStyles.Bold;
        goTxt.alignment = TextAlignmentOptions.Center;
        goTxt.color     = new Color(0.9f, 0.15f, 0.15f, 1f);

        var restartBtnGO = FindOrCreateChild(goPanel, "RestartButton");
        SetAnchor(restartBtnGO, new Vector2(0.5f, 0.4f), new Vector2(0.5f, 0.4f), Vector2.zero, new Vector2(200, 50));
        var restartBtn = restartBtnGO.GetComponent<Button>() ?? restartBtnGO.AddComponent<Button>();
        var restartBtnImg = restartBtnGO.GetComponent<Image>() ?? restartBtnGO.AddComponent<Image>();
        restartBtnImg.color = new Color(0.15f, 0.5f, 0.15f, 1f);

        var restartTxtGO = FindOrCreateChild(restartBtnGO, "Text");
        SetAnchorStretch(restartTxtGO, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var restartTxt = restartTxtGO.GetComponent<TextMeshProUGUI>() ?? restartTxtGO.AddComponent<TextMeshProUGUI>();
        restartTxt.text      = "Try Again";
        restartTxt.fontSize  = 20;
        restartTxt.fontStyle = FontStyles.Bold;
        restartTxt.alignment = TextAlignmentOptions.Center;
        restartTxt.color     = Color.white;

        // ── 11. Wire up GameManager ──────────────────────────────────────────
        var gmGO = GameObject.Find("GameManager");
        if (gmGO != null)
        {
            var gm = gmGO.GetComponent<GameManager>();
            if (gm != null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    SetField(gm, "playerHealth",    player.GetComponent<Health>());
                    SetField(gm, "playerTransform", player.transform);
                }

                SetField(gm, "gameOverUI",   goPanel);
                SetField(gm, "gameOverText", goTxt);
                SetField(gm, "restartButton", restartBtn);
                SetField(gm, "screenTint",   tintImg);

                // Villager2
                if (v2 != null)
                    SetField(gm, "villager2", v2.GetComponent<Villager2NPC>());
            }
        }

        // ── 12. Wire up HealthBar on player ──────────────────────────────────
        var hbComp = healthBarGO.GetComponent<HealthBar>();
        if (hbComp == null) hbComp = healthBarGO.AddComponent<HealthBar>();
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
            SetField(hbComp, "targetHealth", playerGO.GetComponent<Health>());
        SetField(hbComp, "fillImage",  fillImg);
        SetField(hbComp, "healthText", hpTxt);

        // ── 13. Wire MusicManager audio clips ────────────────────────────────
        var mmGO = GameObject.Find("MusicManager");
        if (mmGO != null)
        {
            var mm = mmGO.GetComponent<MusicManager>();
            if (mm != null)
            {
                var ambient = LoadAudioClip("Assets/Audio/Music/Village ambiance.mp3 (stäng av när fight börjar).mp3");
                var combat  = LoadAudioClip("Assets/Audio/Music/Village fight musik.mp3");
                if (ambient != null) SetField(mm, "musicClip",       ambient);
                if (combat  != null) SetField(mm, "combatMusicClip", combat);
            }
        }

        // ── 14. Mark scene dirty and save ────────────────────────────────────
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[VillageSceneSetup] ✓ Scene wired up successfully! Save the scene (Ctrl+S) to persist.");
        EditorUtility.DisplayDialog("Village Scene Setup", "Scene setup complete! Check the console for details.\nSave the scene with Ctrl+S.", "OK");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    static GameObject FindOrCreate(string name)
    {
        var go = GameObject.Find(name);
        if (go == null) go = new GameObject(name);
        return go;
    }

    static GameObject FindOrCreateChild(GameObject parent, string name)
    {
        var t = parent.transform.Find(name);
        if (t != null) return t.gameObject;

        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);

        // Ensure RectTransform for UI parents
        if (parent.GetComponent<Canvas>() != null || parent.GetComponent<RectTransform>() != null)
        {
            if (go.GetComponent<RectTransform>() == null)
                go.AddComponent<RectTransform>();
        }
        return go;
    }

    static void SetAnchor(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin       = anchorMin;
        rt.anchorMax       = anchorMax;
        rt.pivot           = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta       = sizeDelta;
    }

    static void SetAnchorStretch(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    static void SetField(object target, string fieldName, object value)
    {
        var type  = target.GetType();
        FieldInfo field = null;
        while (type != null && field == null)
        {
            field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            type  = type.BaseType;
        }
        if (field != null)
            field.SetValue(target, value);
        else
            Debug.LogWarning($"[VillageSceneSetup] Field '{fieldName}' not found on {target.GetType().Name}");
    }

    static AudioClip LoadAudioClip(string path)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null)
            Debug.LogWarning($"[VillageSceneSetup] AudioClip not found: {path}");
        return clip;
    }
}
