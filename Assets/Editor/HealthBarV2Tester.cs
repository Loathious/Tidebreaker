// Editor-only helper — gives an Inspector button for quickly testing
// HealthBarV2 without entering the JoA menu.
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HealthBarV2))]
public class HealthBarV2Tester : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to use the test buttons.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Runtime Tests", EditorStyles.boldLabel);

        var target = (HealthBarV2)this.target;

        if (GUILayout.Button("Hide")) target.Hide();
        if (GUILayout.Button("Show")) target.Show();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Player Health", EditorStyles.boldLabel);

        var player = GameObject.FindGameObjectWithTag("Player");
        var health = player != null
            ? (player.GetComponent<Health>() ?? player.GetComponentInChildren<Health>())
            : null;

        if (health == null)
        {
            EditorGUILayout.HelpBox("No Health found on Player.", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField($"Current HP: {health.CurrentHealth:F0} / {health.MaxHealth:F0}");

        if (GUILayout.Button("Deal 50 damage"))  health.TakeDamage(50f);
        if (GUILayout.Button("Heal 25 HP"))      health.Heal(25f);
        if (GUILayout.Button("Set HP to 15"))    health.SetCurrentHealth(15f);
        if (GUILayout.Button("Reset to full HP")) health.ResetHealth();
    }
}
#endif
