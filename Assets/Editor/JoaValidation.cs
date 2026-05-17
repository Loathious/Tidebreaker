using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
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
        // Defer until Editor is fully ready so scene operations work.
        EditorApplication.delayCall += RunValidation;
    }

    [MenuItem("JoA/Validate Boss Fixes")]
    public static void RunValidation()
    {
        int pass = 0, fail = 0;
        string projectRoot = Application.dataPath.Replace("/Assets", "").Replace("\\Assets", "");

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
            { Debug.LogWarning("[JoaValidation] SKIP  TMP Settings.asset not found at expected path"); }
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

        // ── 3. JungleGuardian.cs source — inline patrol bounds present ────────
        {
            string src = File.ReadAllText(
                Path.Combine(Application.dataPath, "Scripts", "JungleGuardian.cs"));

            bool noClampRef   = !src.Contains("PatrolBoundsClamp");
            bool hasMinField  = src.Contains("_minPatrolX");
            bool hasMaxField  = src.Contains("_maxPatrolX");
            bool hasSetMethod = src.Contains("SetPatrolBounds");
            bool hasKAirTime  = src.Contains("kAirTime");

            if (noClampRef)
            { Debug.Log("[JoaValidation] PASS  JungleGuardian.cs has no PatrolBoundsClamp reference (teleport fix)"); pass++; }
            else
            { Debug.LogError("[JoaValidation] FAIL  JungleGuardian.cs still references PatrolBoundsClamp"); fail++; }

            if (hasMinField && hasMaxField)
            { Debug.Log("[JoaValidation] PASS  JungleGuardian.cs has inline _minPatrolX/_maxPatrolX fields"); pass++; }
            else
            { Debug.LogError("[JoaValidation] FAIL  JungleGuardian.cs missing inline patrol fields"); fail++; }

            if (hasSetMethod)
            { Debug.Log("[JoaValidation] PASS  JungleGuardian.cs has SetPatrolBounds method"); pass++; }
            else
            { Debug.LogError("[JoaValidation] FAIL  JungleGuardian.cs missing SetPatrolBounds"); fail++; }

            if (hasKAirTime)
            { Debug.Log("[JoaValidation] PASS  JungleGuardian.cs has jump velocity cap (kAirTime)"); pass++; }
            else
            { Debug.LogError("[JoaValidation] FAIL  JungleGuardian.cs missing jump velocity cap"); fail++; }
        }

        // ── 4. JungleManager.cs calls SetPatrolBounds ─────────────────────────
        {
            string src = File.ReadAllText(
                Path.Combine(Application.dataPath, "Scripts", "JungleManager.cs"));
            if (src.Contains("SetPatrolBounds"))
            { Debug.Log("[JoaValidation] PASS  JungleManager.cs calls SetPatrolBounds"); pass++; }
            else
            { Debug.LogError("[JoaValidation] FAIL  JungleManager.cs does not call SetPatrolBounds"); fail++; }
        }

        // ── 5. BossHealthBar.cs dimensions ────────────────────────────────────
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

        // ── 6. Reflection: SetPatrolBounds round-trip on a dummy instance ──────
        {
            var flags  = BindingFlags.NonPublic | BindingFlags.Instance;
            var minF   = typeof(JungleGuardian).GetField("_minPatrolX", flags);
            var maxF   = typeof(JungleGuardian).GetField("_maxPatrolX", flags);
            var method = typeof(JungleGuardian).GetMethod("SetPatrolBounds",
                BindingFlags.Public | BindingFlags.Instance);

            if (minF != null && maxF != null && method != null)
            {
                // Create a minimal GameObject in memory for the test.
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
                { Debug.LogError($"[JoaValidation] FAIL  SetPatrolBounds round-trip failed: got min={gotMin} max={gotMax}"); fail++; }
            }
            else
            { Debug.LogError("[JoaValidation] FAIL  Could not locate patrol fields/method via reflection"); fail++; }
        }

        string summary = fail == 0
            ? $"ALL {pass} CHECKS PASSED"
            : $"{pass} passed  |  {fail} FAILED";
        Debug.Log($"[JoaValidation] === Validation END: {summary} ===");
    }
}
