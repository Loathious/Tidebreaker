using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Adds a subtle scale-punch animation to a UI Button on hover and click.
/// Attach this alongside a Button component for a snappier menu feel.
///
///   Hover  → scales up to hoverScale over hoverDuration
///   Click  → punches down to clickScale then springs back
///   Exit   → returns to Vector3.one
/// </summary>
[RequireComponent(typeof(Button))]
public class ButtonJuice : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Hover")]
    [SerializeField] private float hoverScale    = 1.06f;
    [SerializeField] private float hoverDuration = 0.08f;

    [Header("Click")]
    [SerializeField] private float clickScale    = 0.92f;
    [SerializeField] private float clickDuration = 0.06f;

    private Vector3 _baseScale;
    private Coroutine _anim;

    private void Awake()
    {
        _baseScale = transform.localScale;
    }

    public void OnPointerEnter(PointerEventData _) => Animate(hoverScale, hoverDuration);
    public void OnPointerExit(PointerEventData _)  => Animate(1f, hoverDuration);
    public void OnPointerDown(PointerEventData _)  => Animate(clickScale, clickDuration);
    public void OnPointerUp(PointerEventData _)    => Animate(hoverScale, clickDuration);

    private void Animate(float targetUniform, float duration)
    {
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(ScaleTo(_baseScale * targetUniform, duration));
    }

    private IEnumerator ScaleTo(Vector3 target, float duration)
    {
        Vector3 start = transform.localScale;
        float   t     = 0f;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(duration, 0.001f);
            transform.localScale = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        transform.localScale = target;
        _anim = null;
    }

    private void OnDisable()
    {
        if (_anim != null) { StopCoroutine(_anim); _anim = null; }
        transform.localScale = _baseScale;
    }
}
