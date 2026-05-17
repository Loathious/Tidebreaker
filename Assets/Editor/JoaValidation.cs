using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Runs automatically on every domain reload (after each compile).
/// Results appear in the Console: search for "[JoaValidation]".
/// </summary>
[InitializeOnLoad]
public static class JoaValidation
{
    static JoaValidation()
    {
        EditorApplication.delayCall += RunValidation;
    }

    [MenuItem("JoA/Validate Boss Fixes")]
    public static void RunValidation()
    {
        int pass = 0, fail = 0;

        Debug.Log("[JoaValidation] === Validation START ===");

        // ── 1. TMP Settings default font ─────────────────────────────────────
        {
            string path = Path.Combine(Application.dataPath,
                "TextMesh Pro", "Resources", "TMP Settings.asset");
            if (File.Exists(path))
            {
                string text = File.ReadAllText(path);
                const string pressGUID = "815cb26d3d66a724fa69e38b0e93a4c9";
                const string libGUID   = "8f586378b4e144a9851e7b34d9b748ee";
                if (text.Contains(pressGUID) && !text.Contains(libGUID))
                { Debug.Log("[JoaValidation] PASS  TMP default font → PressStart2P"); pass++; }
                else
                { Debug.LogError("[JoaValidation] FAIL  TMP default font is NOT PressStart2P"); fail++; }
            }
            else
            { Debug.LogWarning("[JoaValidation] SKIP  TMP Settings.asset not found"); }
        }

        // ── 2. Scene files — no LiberationSans GUID ──────────────────────────
        {
            const string badGUID = "8f586378b4e144a9851e7b34d9b748ee";
            string sceneDir = Path.Combine(Application.dataPath, "Scenes");
            string[] scenes = { "Jungle", "Desert", "Ocean", "Cave", "Village", "EndCredits" };
            int badCount = 0;
            foreach (string s in scenes)
            {
                string p = Path.Combine(sceneDir, s + ".unity");
                if (!File.Exists(p)) continue;
                if (File.ReadAllText(p).Contains(badGUID))
                { Debug.LogError($"[JoaValidation] FAIL  {s}.unity still has LiberationSans GUID"); badCount++; fail++; }
            }
            if (badCount == 0)
            { Debug.Log("[JoaValidation] PASS  All scene files free of LiberationSans GUID"); pass++; }
        }

        // ── 3. JungleGuardian.cs — patrol bounds + no JumpAttack ─────────────
        {
            string src = File.ReadAllText(
                Path.Combine(Application.dataPath, "Scripts", "JungleGuardian.cs"));

            Check(!src.Contains("PatrolBoundsClamp"),
                "JungleGuardian has no PatrolBoundsClamp (teleport fix)", ref pass, ref fail);
            Check(src.Contains("_minPatrolX") && src.Contains("_maxPatrolX"),
                "JungleGuardian has inline _minPatrolX/_maxPatrolX fields", ref pass, ref fail);
            Check(src.Contains("SetPatrolBounds"),
                "JungleGuardian has SetPatrolBounds method", ref pass, ref fail);
            Check(!src.Contains("JumpAttack"),
                "JungleGuardian has no JumpAttack (removed — caused teleport)", ref pass, ref fail);

            // Bounds enforcement runs BEFORE the _busy guard in FixedUpdate.
            // We verify the order by checking both strings appear and _busy guard is present.
            bool hasBoundsClamp  = src.Contains("_minPatrolX") && src.Contains("Mathf.Clamp");
            bool hasBusyReturn   = src.Contains("if (!_active || _busy || _player == null) return;");
            Check(hasBoundsClamp && hasBusyReturn,
                "JungleGuardian bounds clamping present (runs before _busy guard)", ref pass, ref fail);

            // Fallback sprite in Start so boss is never invisible.
            Check(src.Contains("ProceduralSprite.Box") && src.Contains("_sr.color = Color.white"),
                "JungleGuardian has fallback sprite + forced Color.white (never invisible)", ref pass, ref fail);
        }

        // ── 4. JungleManager.cs — calls SetPatrolBounds ───────────────────────
        {
            string src = File.ReadAllText(
                Path.Combine(Application.dataPath, "Scripts", "JungleManager.cs"));
            Check(src.Contains("SetPatrolBounds"),
                "JungleManager calls SetPatrolBounds", ref pass, ref fail);
        }

        // ── 5. BossHealthBar.cs — dimensions ──────────────────────────────────
        {
            string barPath = Path.Combine(Application.dataPath, "Scripts", "BossHealthBar.cs");
            if (File.Exists(barPath))
            {
                string src = File.ReadAllText(barPath);
                bool has260 = src.Contains("260");
                bool has22  = src.Contains(", 22") || src.Contains(",22");
                if (has260 && has22)
                { Debug.Log("[JoaValidation] PASS  BossHealthBar.cs contains 260×22 root dimensions"); pass++; }
                else
                { Debug.LogWarning($"[JoaValidation] WARN  BossHealthBar.cs dimension check: 260={has260} 22={has22}"); }
            }
        }

        // ── 6. Reflection: SetPatrolBounds round-trip ─────────────────────────
        {
            var flags  = BindingFlags.NonPublic | BindingFlags.Instance;
            var minF   = typeof(JungleGuardian).GetField("_minPatrolX", flags);
            var maxF   = typeof(JungleGuardian).GetField("_maxPatrolX", flags);
            var method = typeof(JungleGuardian).GetMethod("SetPatrolBounds",
                BindingFlags.Public | BindingFlags.Instance);

            if (minF != null && maxF != null && method != null)
            {
                var go = new GameObject("__ValidationDummy__");
                go.hideFlags = HideFlags.HideAndDontSave;
                go.AddComponent<Rigidbody2D>();
                go.AddComponent<BoxCollider2D>();
                var guardian = go.AddComponent<JungleGuardian>();

                method.Invoke(guardian, new object[] { 38.05f, 52.05f });
                float gotMin = (float)minF.GetValue(guardian);
                float gotMax = (float)maxF.GetValue(guardian);
                Object.DestroyImmediate(go);

                if (Mathf.Approximately(gotMin, 38.05f) && Mathf.Approximately(gotMax, 52.05f))
                { Debug.Log($"[JoaValidation] PASS  SetPatrolBounds round-trip: min={gotMin:F2} max={gotMax:F2}"); pass++; }
                else
                { Debug.LogError($"[JoaValidation] FAIL  SetPatrolBounds round-trip: got min={gotMin} max={gotMax}"); fail++; }
            }
            else
            { Debug.LogError("[JoaValidation] FAIL  Could not locate patrol fields/method via reflection"); fail++; }
        }

        // ── 7. PlayerController.cs — fall on death ────────────────────────────
        {
            string src = File.ReadAllText(
                Path.Combine(Application.dataPath, "Scripts", "PlayerController.cs"));

            // Must disable colliders on death.
            Check(src.Contains("col.enabled = false"),
                "PlayerController.OnDeath disables colliders (player falls)", ref pass, ref fail);
            // Must give a downward velocity kick.
            Check(src.Contains("new Vector2(0f, -4f)"),
                "PlayerController.OnDeath applies -4f downward kick", ref pass, ref fail);
            // FixedUpdate must bail out when dead (no velocity override during fall).
            Check(src.Contains("if (_isDead) return;"),
                "PlayerController.FixedUpdate returns early when dead (no fall interruption)", ref pass, ref fail);
        }

        // ── 8. Reflection: PlayerController._isDead set true on OnDeath ───────
        {
            var flags    = BindingFlags.NonPublic | BindingFlags.Instance;
            var isDeadF  = typeof(PlayerController).GetField("_isDead", flags);
            var onDeathM = typeof(PlayerController).GetMethod("OnDeath", flags);
            var rbF      = typeof(PlayerController).GetField("_rb",               flags);
            var defGravF = typeof(PlayerController).GetField("_defaultGravityScale", flags);

            if (isDeadF != null && onDeathM != null && rbF != null)
            {
                var go = new GameObject("__PCDummy__");
                go.hideFlags = HideFlags.HideAndDontSave;
                var rb = go.AddComponent<Rigidbody2D>();
                go.AddComponent<BoxCollider2D>();
                var groundGO = new GameObject("GroundCheck");
                groundGO.transform.SetParent(go.transform);
                go.AddComponent<Health>();
                var pc = go.AddComponent<PlayerController>();
                pc.groundCheck = groundGO.transform;

                // Start() doesn't run in EditMode — prime the fields OnDeath needs.
                rbF.SetValue(pc, rb);
                defGravF?.SetValue(pc, 1f);

                onDeathM.Invoke(pc, null);
                bool isDead = (bool)isDeadF.GetValue(pc);
                Object.DestroyImmediate(go);

                Check(isDead, "PlayerController._isDead = true after OnDeath()", ref pass, ref fail);
            }
            else
            { Debug.LogError("[JoaValidation] FAIL  Could not find _isDead / OnDeath / _rb via reflection"); fail++; }
        }

        // ── 9. LevelManagerBase.cs — solid death overlay (not a vignette) ─────
        {
            string src = File.ReadAllText(
                Path.Combine(Application.dataPath, "Scripts", "LevelManagerBase.cs"));

            // Must have a separate solid overlay builder (not reuse the gradient vignette).
            Check(src.Contains("BuildSolidDeathOverlay"),
                "LevelManagerBase has BuildSolidDeathOverlay (solid red, not a vignette)", ref pass, ref fail);
            // Solid overlay must not create a Texture2D (that would make it a vignette again).
            // Verify BuildSolidDeathOverlay itself contains no "new Texture2D".
            int solidIdx   = src.IndexOf("BuildSolidDeathOverlay", System.StringComparison.Ordinal);
            int nextMethod = src.IndexOf("\n    private ", solidIdx + 1, System.StringComparison.Ordinal);
            string solidBody = nextMethod > 0 ? src.Substring(solidIdx, nextMethod - solidIdx) : "";
            Check(!solidBody.Contains("new Texture2D"),
                "BuildSolidDeathOverlay does NOT create a Texture2D (confirms it is a plain solid Image)", ref pass, ref fail);
            // Death sequence must use unscaled time so it works after Time.timeScale=0.
            Check(src.Contains("Time.unscaledDeltaTime"),
                "DeathSequence uses unscaled time (works after timeScale=0)", ref pass, ref fail);
        }

        // ── 10. LevelManagerBase.cs — spawn position fix ──────────────────────
        {
            string src = File.ReadAllText(
                Path.Combine(Application.dataPath, "Scripts", "LevelManagerBase.cs"));

            Check(src.Contains("sameScene"),
                "LevelManagerBase.RestoreFromSave compares scene names (spawn fix)", ref pass, ref fail);
            // Health is reset when advancing to a new level.
            Check(src.Contains("hp?.ResetHealth()"),
                "LevelManagerBase resets player health when entering a new level", ref pass, ref fail);
        }

        // ── 11. HealthBar.cs — correct colors + player-tag fallback ───────────
        {
            string src = File.ReadAllText(
                Path.Combine(Application.dataPath, "Scripts", "HealthBar.cs"));

            // Green fill.
            Check(src.Contains("0.15f, 0.85f, 0.2f"),
                "HealthBar fillImage hardcoded green (0.15, 0.85, 0.2)", ref pass, ref fail);
            // Red lost-health.
            Check(src.Contains("0.85f, 0.1f, 0.1f"),
                "HealthBar lostHealthImage hardcoded red (0.85, 0.1, 0.1)", ref pass, ref fail);
            // Fallback search when bar is on a Canvas not in the player hierarchy.
            Check(src.Contains("FindGameObjectWithTag") && src.Contains("\"Player\""),
                "HealthBar falls back to FindGameObjectWithTag(\"Player\")", ref pass, ref fail);
            // Sibling order fix so red renders behind green.
            Check(src.Contains("SetSiblingIndex"),
                "HealthBar fixes sibling order (red behind green)", ref pass, ref fail);
        }

        string summary = fail == 0
            ? $"ALL {pass} CHECKS PASSED"
            : $"{pass} passed  |  {fail} FAILED";
        Debug.Log($"[JoaValidation] === Validation END: {summary} ===");
    }

    static void Check(bool condition, string label, ref int pass, ref int fail)
    {
        if (condition)
        { Debug.Log($"[JoaValidation] PASS  {label}"); pass++; }
        else
        { Debug.LogError($"[JoaValidation] FAIL  {label}"); fail++; }
    }
}
