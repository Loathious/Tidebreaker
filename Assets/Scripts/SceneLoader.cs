using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Simple scene loading utility. Attach to any persistent manager object.
/// Uses scene names rather than indices for reliability.
/// </summary>
public static class SceneLoader
{
    /// <summary>Loads a scene by name.</summary>
    public static void Load(string sceneName)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>Loads a scene by build index.</summary>
    public static void Load(int buildIndex)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(buildIndex);
    }

    /// <summary>Reloads the currently active scene.</summary>
    public static void ReloadCurrent()
    {
        Load(SceneManager.GetActiveScene().buildIndex);
    }
}
