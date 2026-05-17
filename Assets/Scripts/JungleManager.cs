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

    private int            _enemiesDefeated;
    private bool           _guardianDefeated;
    private JungleGuardian _guardian;
    private StoryPortal    _templePortal;
    private GameObject     _templeGate;

    // World-space X of the temple gate — guardian always stays left of this.
    private const float GateX = 54.05f;
    private const float GateY = -1.09f;

    protected override void OnLevelStart()
    {
        _guardian     = FindFirstObjectByType<JungleGuardian>(FindObjectsInactive.Include);
        _templePortal = FindFirstObjectByType<StoryPortal>(FindObjectsInactive.Include);

        if (_templePortal != null) _templePortal.unlocked = false;
        SpawnTempleGate();

        if (_guardian != null)
        {
            // Use a BossArenaBounds object placed in the scene if available;
            // otherwise fall back to the hardcoded limits around the gate.
            BossArenaBounds arena = FindFirstObjectByType<BossArenaBounds>(FindObjectsInactive.Include);
            if (arena != null)
            {
                arena.GetBoundsX(out float arenaMin, out float arenaMax);
                EnsureGuardianInsideBounds(_guardian, arenaMin, arenaMax);
                _guardian.SetPatrolBounds(arenaMin, arenaMax);
            }
            else
            {
                EnsureGuardianOutsideTemple(_guardian);
                SetGuardianPatrolBounds(_guardian);
            }
        }

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

        if (_templeGate != null) Destroy(_templeGate);

        ObjectiveManager.Instance?.UpdateObjective("Enter the temple");

        if (templeOpenClip != null && Camera.main != null)
            AudioSource.PlayClipAtPoint(templeOpenClip, Camera.main.transform.position, 0.9f);

        if (_templePortal != null)
            _templePortal.UnlockPortal();

        StartCoroutine(GuardianDownBanner());
    }

    // ── Temple gate & guardian bounds ─────────────────────────────────────────

    private void SpawnTempleGate()
    {
        _templeGate = new GameObject("TempleGate");
        _templeGate.layer = LayerMask.NameToLayer("Ground") >= 0
            ? LayerMask.NameToLayer("Ground") : 3;

        // Absolute position as specified — invisible solid wall blocking the entrance.
        _templeGate.transform.position = new Vector3(GateX, GateY, 0f);

        BoxCollider2D col = _templeGate.AddComponent<BoxCollider2D>();
        col.size   = new Vector2(1f, 6f);
        col.offset = Vector2.zero;

        // Add SpriteRenderer but keep it disabled — gate is invisible.
        var sr = _templeGate.AddComponent<SpriteRenderer>();
        sr.sprite = ProceduralSprite.Box(8, 64, new Color(0.25f, 0.18f, 0.1f));
        sr.sortingOrder = 3;
        sr.enabled = false;
    }

    /// <summary>
    /// Moves the guardian into the arena bounds if it is currently outside them.
    /// Used when a BossArenaBounds object is placed in the scene.
    /// </summary>
    private void EnsureGuardianInsideBounds(JungleGuardian guardian, float minX, float maxX)
    {
        Collider2D col = guardian.GetComponent<Collider2D>();
        float visualX  = col != null ? col.bounds.center.x : guardian.transform.position.x;
        float targetX  = (minX + maxX) * 0.5f;   // center of the arena

        if (visualX >= minX && visualX <= maxX) return;   // already inside

        float delta    = targetX - visualX;
        Vector3 pos    = guardian.transform.position;
        pos.x         += delta;
        guardian.transform.position = pos;
        Rigidbody2D rb = guardian.GetComponent<Rigidbody2D>();
        if (rb != null) rb.position = new Vector2(pos.x, pos.y);
    }

    /// <summary>
    /// Moves the guardian so its visual center (collider bounds) is safely to the
    /// left of the temple gate, outside in the jungle arena.
    /// </summary>
    private void EnsureGuardianOutsideTemple(JungleGuardian guardian)
    {
        Collider2D col = guardian.GetComponent<Collider2D>();
        if (col == null) return;

        float visualX = col.bounds.center.x;
        const float targetVisualX = 46f;   // 8 units left of the gate

        // Only move if guardian is on the wrong side (inside/right of gate).
        if (visualX <= GateX - 2f) return;

        float delta = targetVisualX - visualX;
        Vector3 pos = guardian.transform.position;
        pos.x += delta;
        guardian.transform.position = pos;

        // Sync Rigidbody2D so physics doesn't snap it back on the first frame.
        Rigidbody2D rb = guardian.GetComponent<Rigidbody2D>();
        if (rb != null) rb.position = new Vector2(pos.x, pos.y);
    }

    private void SetGuardianPatrolBounds(JungleGuardian guardian)
    {
        // Derive arena from the guardian's actual scene-placed position so the bounds
        // are always centred on the boss regardless of where it was placed in the editor.
        Collider2D col = guardian.GetComponent<Collider2D>();
        float cx = col != null ? col.bounds.center.x : guardian.transform.position.x;
        guardian.SetPatrolBounds(cx - 4f, cx + 4f);
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
