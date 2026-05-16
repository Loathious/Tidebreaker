using System.Collections;
using UnityEngine;

/// <summary>
/// Level 3 — Jungle Temple manager.
/// Objective: fight through monkeys and vine snakes, defeat the Jungle Guardian
/// mini-boss, then read the temple inscription. Self-bootstrapping.
/// </summary>
public class JungleManager : LevelManagerBase
{
    [Header("Jungle")]
    [SerializeField] private int enemiesToReachTemple = 10;

    [Header("Audio")]
    public AudioClip templeOpenClip;

    private int           _enemiesDefeated;
    private bool          _guardianDefeated;
    private JungleGuardian _guardian;
    private StoryPortal   _templePortal;

    protected override void OnLevelStart()
    {
        _guardian     = FindFirstObjectByType<JungleGuardian>(FindObjectsInactive.Include);
        _templePortal = FindFirstObjectByType<StoryPortal>(FindObjectsInactive.Include);

        // The temple stays sealed until the Guardian falls
        if (_templePortal != null) _templePortal.unlocked = false;

        ObjectiveManager.Instance?.ShowObjective(
            $"Fight through the jungle ({_enemiesDefeated}/{enemiesToReachTemple})");
    }

    public override void OnEnemyDefeated()
    {
        if (_guardianDefeated) return;
        _enemiesDefeated++;

        if (_enemiesDefeated < enemiesToReachTemple)
        {
            ObjectiveManager.Instance?.UpdateObjective(
                $"Fight through the jungle ({_enemiesDefeated}/{enemiesToReachTemple})");
        }
        else if (_enemiesDefeated == enemiesToReachTemple)
        {
            ObjectiveManager.Instance?.UpdateObjective("Defeat the Jungle Guardian!");
        }
    }

    /// <summary>Called by JungleGuardian when it dies.</summary>
    public void OnGuardianDefeated()
    {
        if (_guardianDefeated) return;
        _guardianDefeated = true;
        NotifyCombatEnded();

        ObjectiveManager.Instance?.UpdateObjective("Enter the temple");

        if (templeOpenClip != null && Camera.main != null)
            AudioSource.PlayClipAtPoint(templeOpenClip, Camera.main.transform.position, 0.9f);

        if (_templePortal != null)
            _templePortal.UnlockPortal();

        StartCoroutine(GuardianDownBanner());
    }

    private IEnumerator GuardianDownBanner()
    {
        Canvas canvas = FindOverlayCanvas();
        if (canvas == null) yield break;

        GameObject go = new GameObject("GuardianBanner");
        go.transform.SetParent(canvas.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.1f, 0.55f);
        rt.anchorMax = new Vector2(0.9f, 0.7f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var tmp = go.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text      = "THE GUARDIAN FALLS";
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.fontSize  = 22f;
        tmp.color     = new Color(0.7f, 1f, 0.6f, 0f);
        tmp.outlineWidth = 0.28f;
        tmp.outlineColor = new Color32(0, 0, 0, 255);
        FontEnforcer.ApplyTo(tmp);

        float t = 0f;
        while (t < 0.6f)
        { t += Time.unscaledDeltaTime; tmp.color = new Color(0.7f,1f,0.6f, t/0.6f); yield return null; }
        yield return new WaitForSecondsRealtime(1.6f);
        t = 0f;
        while (t < 0.6f)
        { t += Time.unscaledDeltaTime; tmp.color = new Color(0.7f,1f,0.6f, 1f-t/0.6f); yield return null; }
        Destroy(go);
    }
}
