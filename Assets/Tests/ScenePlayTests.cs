using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

/// <summary>
/// PlayMode smoke tests for the new levels. Each test loads a scene, lets it
/// simulate for a few seconds and verifies its core / unique objects exist.
/// Results and any runtime errors are written to joa_test_results.txt in the
/// project root so they can be inspected even if the run can't return live.
/// </summary>
public class ScenePlayTests
{
    private static string ResultFile =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "..", "joa_test_results.txt"));

    private static void Append(string line)
    {
        try { File.AppendAllText(ResultFile, line + "\n"); } catch { /* ignore */ }
    }

    private static void OnLog(string msg, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            Append($"    [{type}] {msg}");
    }

    [OneTimeSetUp]
    public void Setup()
    {
        try { File.WriteAllText(ResultFile, "JoA PlayMode test run\n"); } catch { }
        Application.logMessageReceived += OnLog;
    }

    [OneTimeTearDown]
    public void Teardown()
    {
        Application.logMessageReceived -= OnLog;
        Append("RUN COMPLETE");
    }

    private IEnumerator LoadAndSimulate(string sceneName, float seconds)
    {
        SceneManager.LoadScene(sceneName);
        yield return null;
        yield return null;

        float t = 0f;
        int frames = 0;
        while (t < seconds && frames < 6000)
        {
            t += Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            frames++;
            yield return null;
        }
    }

    private void Check(string scene, Action checks)
    {
        try { checks(); Append($"{scene}: PASSED"); }
        catch (Exception e) { Append($"{scene}: FAILED - {e.Message}"); throw; }
    }

    private static void Require(string name)
    {
        Assert.IsNotNull(GameObject.Find(name), $"GameObject '{name}' missing");
    }

    [UnityTest]
    public IEnumerator Level3_Jungle()
    {
        Append("--- Jungle ---");
        yield return LoadAndSimulate("Jungle", 3f);
        Check("Jungle", () =>
        {
            Assert.IsNotNull(GameObject.FindGameObjectWithTag("Player"), "Player missing");
            Assert.IsNotNull(Camera.main, "Main camera missing");
            Require("GameManager");
            Require("JungleGuardian");
            Require("TempleInscription");
            Require("JunglePlatform_0");
        });
    }

    [UnityTest]
    public IEnumerator Level4_Desert()
    {
        Append("--- Desert ---");
        yield return LoadAndSimulate("Desert", 3f);
        Check("Desert", () =>
        {
            Assert.IsNotNull(GameObject.FindGameObjectWithTag("Player"), "Player missing");
            Require("GameManager");
            Require("Obelisk_1");
            Require("Obelisk_2");
            Require("Obelisk_3");
            Require("GreatSandworm");
            Require("PyramidReward");
        });
    }

    [UnityTest]
    public IEnumerator Level5_Ocean()
    {
        Append("--- Ocean ---");
        yield return LoadAndSimulate("Ocean", 4f);
        Check("Ocean", () =>
        {
            Assert.IsNotNull(GameObject.FindGameObjectWithTag("Player"), "Player missing");
            Require("GameManager");
            Require("Kraken");
            Require("StartPlatform");
            Require("OceanWater");
        });
    }

    [UnityTest]
    public IEnumerator EndCredits()
    {
        Append("--- EndCredits ---");
        yield return LoadAndSimulate("EndCredits", 3f);
        Check("EndCredits", () =>
        {
            Require("EndCreditsManager");
            Require("VillageBackground");
        });
    }
}
