using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Level 5 — The Ocean / Final Boss manager.
/// Plays the Kraken intro cutscene, starts the three-phase boss fight, and on
/// victory runs the "explosion of light" finale before loading the End Credits.
/// </summary>
public class OceanManager : LevelManagerBase
{
    [Header("Ocean")]
    public string endCreditsScene = "EndCredits";

    private KrakenBoss      _kraken;
    private PlayerController _playerCtrl;
    private bool            _ending;

    private static readonly string[] IntroLines =
    {
        "Who are you? You cannot defeat me...",
        "I am the Kraken — I am far too powerful for the likes of you!"
    };

    private static readonly string[] DeathLines =
    {
        "aaahahahahahahahahahahahah...",
        "nooooooooooooo!"
    };

    protected override void OnLevelStart()
    {
        _kraken     = FindFirstObjectByType<KrakenBoss>(FindObjectsInactive.Include);
        _playerCtrl = Player != null ? Player.GetComponent<PlayerController>() : null;

        // Grant bow + armor (carried through from the Desert pyramid, or entering scene directly)
        PlayerRanged.Grant();
        MagicalArmor.Grant();
        StoryPortal.EnsurePlayerGear();

        ObjectiveManager.Instance?.ShowObjective("Defeat the Kraken");
        StartCoroutine(IntroCutscene());
    }

    private IEnumerator IntroCutscene()
    {
        _playerCtrl?.LockInput();
        yield return new WaitForSeconds(0.6f);

        // The Kraken rises out of the ocean
        if (_kraken != null)
        {
            Vector3 risen  = _kraken.transform.position;
            Vector3 sunken = risen + Vector3.down * 7f;
            _kraken.transform.position = sunken;

            Camera.main?.GetComponent<CameraShake>()?.Shake(0.25f, 2f);
            float t = 0f;
            while (t < 2f)
            {
                t += Time.deltaTime;
                _kraken.transform.position =
                    Vector3.Lerp(sunken, risen, Mathf.SmoothStep(0f, 1f, t / 2f));
                yield return null;
            }
            _kraken.transform.position = risen;
        }

        yield return new WaitForSeconds(0.4f);

        // Intro dialogue (guard prevents infinite wait if dialog callback never fires)
        DialogUI dialog = FindFirstObjectByType<DialogUI>(FindObjectsInactive.Include);
        bool done = false;
        if (dialog != null)
            dialog.ShowDialog("Kraken", IntroLines, () => done = true, null,
                              _kraken != null ? _kraken.transform : null);
        else done = true;

        float dialogGuard = 0f;
        while (!done && dialogGuard < 20f) { dialogGuard += Time.deltaTime; yield return null; }

        _playerCtrl?.UnlockInput();
        ObjectiveManager.Instance?.UpdateObjective("Destroy the Kraken's tentacles");

        if (_kraken != null) _kraken.BeginFight();
    }

    /// <summary>Called by KrakenBoss when it dies.</summary>
    public void OnKrakenDefeated()
    {
        if (_ending) return;
        _ending = true;
        StartCoroutine(VictoryCutscene());
    }

    private IEnumerator VictoryCutscene()
    {
        _playerCtrl?.LockInput();
        ObjectiveManager.Instance?.HideObjective();
        MusicManager.Instance?.Stop();

        // Kraken death dialogue
        DialogUI dialog = FindFirstObjectByType<DialogUI>(FindObjectsInactive.Include);
        bool done = false;
        if (dialog != null)
            dialog.ShowDialog("Kraken", DeathLines, () => done = true);
        else done = true;
        float guard = 0f;
        while (!done && guard < 8f) { guard += Time.unscaledDeltaTime; yield return null; }

        // Explosion of light — full-screen white flash
        Canvas canvas = FindOverlayCanvas();
        Image flash = null;
        if (canvas != null)
        {
            GameObject go = new GameObject("VictoryFlash");
            go.transform.SetParent(canvas.transform, false);
            go.transform.SetAsLastSibling();
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            flash = go.AddComponent<Image>();
            flash.color = new Color(1f, 1f, 1f, 0f);
        }

        Camera.main?.GetComponent<CameraShake>()?.Shake(0.5f, 1.2f);

        float t = 0f;
        while (t < 1.4f)
        {
            t += Time.unscaledDeltaTime;
            if (flash != null) flash.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t / 1.4f));
            yield return null;
        }

        // Hold the white, then load the ending
        yield return new WaitForSecondsRealtime(1f);

        SaveManager.Instance?.DeleteSave();   // the adventure is complete

        if (Application.CanStreamedLevelBeLoaded(endCreditsScene))
            SceneManager.LoadScene(endCreditsScene);
        else
            SceneManager.LoadScene("MainMenu");
    }
}
