using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Displays objectives, tutorial hints, and pop-up messages in screen corners.
/// </summary>
public class ObjectiveUI : MonoBehaviour
{
    [Header("Objective Panel (top-left)")]
    [SerializeField] private TextMeshProUGUI objectiveText;

    [Header("Tutorial Hint (centre-bottom)")]
    [SerializeField] private TextMeshProUGUI tutorialHintText;

    [Header("Message (centre screen)")]
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private float messageFadeDuration = 1f;

    private Coroutine messageFadeRoutine;

    void Awake()
    {
        if (objectiveText    != null) objectiveText.text    = "";
        if (tutorialHintText != null) tutorialHintText.text = "";
        if (messageText      != null) messageText.text      = "";
    }

    public void SetObjective(string text)
    {
        if (objectiveText == null) return;
        objectiveText.text = text;
    }

    public void ShowTutorialHint(string hint)
    {
        if (tutorialHintText == null) return;
        tutorialHintText.text = hint;
    }

    public void ClearTutorialHint()
    {
        if (tutorialHintText == null) return;
        tutorialHintText.text = "";
    }

    public void ShowMessage(string msg, float duration = 3f)
    {
        if (messageText == null) return;
        if (messageFadeRoutine != null) StopCoroutine(messageFadeRoutine);
        messageFadeRoutine = StartCoroutine(ShowMessageRoutine(msg, duration));
    }

    public void ClearMessage()
    {
        if (messageFadeRoutine != null) StopCoroutine(messageFadeRoutine);
        if (messageText != null)
        {
            Color c = messageText.color; c.a = 0f;
            messageText.color = c;
            messageText.text  = "";
        }
    }

    IEnumerator ShowMessageRoutine(string msg, float duration)
    {
        messageText.text = msg;
        Color c = messageText.color; c.a = 1f; messageText.color = c;

        yield return new WaitForSeconds(duration);

        float elapsed = 0f;
        while (elapsed < messageFadeDuration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(1f, 0f, elapsed / messageFadeDuration);
            messageText.color = c;
            yield return null;
        }

        messageText.text = "";
        c.a = 1f; messageText.color = c;
    }
}
