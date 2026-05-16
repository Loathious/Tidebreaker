using System.Collections.Generic;
using System.Collections;
using UnityEngine;

/// <summary>
/// Level 4 — Desert Pyramid manager.
/// Objective: activate three Obelisks, each behind its own trial:
///  • Obelisk 1 — defeat the great Sandworm.
///  • Obelisk 2 — cross the platform puzzle and reach it.
///  • Obelisk 3 — survive a wave of six sand creatures.
/// When all three are lit the pyramid opens (the reward portal unlocks).
/// </summary>
public class DesertManager : LevelManagerBase
{
    private readonly Obelisk[] _obelisks = new Obelisk[3];
    private StoryPortal        _pyramidPortal;

    private bool _ob1Done, _ob2Done, _ob3Done;
    private int  _activatedCount;

    private readonly List<GameObject> _waveEnemies = new List<GameObject>();
    private bool _waveStarted;

    protected override void OnLevelStart()
    {
        // Map obelisks by their index field
        foreach (Obelisk o in FindObjectsByType<Obelisk>(FindObjectsInactive.Include,
                                                         FindObjectsSortMode.None))
        {
            int i = Mathf.Clamp(o.index - 1, 0, 2);
            _obelisks[i] = o;
        }

        _pyramidPortal = FindFirstObjectByType<StoryPortal>(FindObjectsInactive.Include);
        if (_pyramidPortal != null) _pyramidPortal.unlocked = false;

        // Collect + hide the wave enemies until Obelisk 3 is reached
        foreach (SandwormAI sw in FindObjectsByType<SandwormAI>(FindObjectsInactive.Include,
                                                                FindObjectsSortMode.None))
        {
            if (sw.countsAsWaveEnemy)
            {
                _waveEnemies.Add(sw.gameObject);
                sw.gameObject.SetActive(false);
            }
        }

        ObjectiveManager.Instance?.ShowObjective("Activate the 3 Obelisks (0/3)");
    }

    protected override void Update()
    {
        base.Update();
        if (IsGameOver || Player == null) return;

        Vector3 p = Player.transform.position;

        // Obelisk 2 — reaching it completes the platform puzzle
        if (!_ob2Done && _obelisks[1] != null &&
            Vector2.Distance(p, _obelisks[1].transform.position) < 3.5f)
        {
            _ob2Done = true;
            _obelisks[1].SetChallengeComplete();
        }

        // Obelisk 3 — approaching it triggers the enemy wave
        if (!_waveStarted && _obelisks[2] != null &&
            Vector2.Distance(p, _obelisks[2].transform.position) < 9f)
        {
            StartWave();
        }

        // Wave cleared?
        if (_waveStarted && !_ob3Done && AllWaveEnemiesDead())
        {
            _ob3Done = true;
            if (_obelisks[2] != null) _obelisks[2].SetChallengeComplete();
            ObjectiveManager.Instance?.UpdateObjective(
                $"Activate the 3 Obelisks ({_activatedCount}/3) — trials complete");
        }
    }

    private void StartWave()
    {
        _waveStarted = true;
        NotifyCombatStarted();
        foreach (GameObject e in _waveEnemies)
            if (e != null) e.SetActive(true);
        ObjectiveManager.Instance?.UpdateObjective("Obelisk 3 — survive the sand wave!");
    }

    private bool AllWaveEnemiesDead()
    {
        foreach (GameObject e in _waveEnemies)
            if (e != null) return false;
        return true;
    }

    /// <summary>Called by SandwormAI when any sandworm dies.</summary>
    public void OnSandwormKilled(SandwormAI worm)
    {
        // The great Sandworm (not a wave creature) unlocks Obelisk 1
        if (worm != null && !worm.countsAsWaveEnemy && !_ob1Done)
        {
            _ob1Done = true;
            if (_obelisks[0] != null) _obelisks[0].SetChallengeComplete();
        }
    }

    /// <summary>Called by an Obelisk when the player activates it.</summary>
    public void OnObeliskActivated(Obelisk obelisk)
    {
        _activatedCount++;
        if (_activatedCount < 3)
        {
            ObjectiveManager.Instance?.UpdateObjective(
                $"Activate the 3 Obelisks ({_activatedCount}/3)");
        }
        else
        {
            NotifyCombatEnded();
            ObjectiveManager.Instance?.UpdateObjective("Enter the pyramid");
            if (_pyramidPortal != null) _pyramidPortal.UnlockPortal();
            StartCoroutine(PyramidOpensBanner());
        }
    }

    private IEnumerator PyramidOpensBanner()
    {
        Camera.main?.GetComponent<CameraShake>()?.Shake(0.3f, 1f);

        Canvas canvas = FindOverlayCanvas();
        if (canvas == null) yield break;

        GameObject go = new GameObject("PyramidBanner");
        go.transform.SetParent(canvas.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.1f, 0.55f);
        rt.anchorMax = new Vector2(0.9f, 0.7f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var tmp = go.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text      = "THE PYRAMID OPENS";
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.fontSize  = 22f;
        tmp.color     = new Color(1f, 0.9f, 0.5f, 0f);
        tmp.outlineWidth = 0.28f;
        tmp.outlineColor = new Color32(0, 0, 0, 255);
        FontEnforcer.ApplyTo(tmp);

        float t = 0f;
        while (t < 0.6f)
        { t += Time.unscaledDeltaTime; tmp.color = new Color(1f,0.9f,0.5f, t/0.6f); yield return null; }
        yield return new WaitForSecondsRealtime(1.8f);
        t = 0f;
        while (t < 0.6f)
        { t += Time.unscaledDeltaTime; tmp.color = new Color(1f,0.9f,0.5f, 1f-t/0.6f); yield return null; }
        Destroy(go);
    }
}
