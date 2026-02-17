using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class FilledImageSetup : MonoBehaviour
{
    [SerializeField] private Image.FillMethod fillMethod = Image.FillMethod.Horizontal;
    [SerializeField] private int fillOrigin = 0;
    [SerializeField] private bool fillClockwise = true;
    
    void Awake()
    {
        Image img = GetComponent<Image>();
        if (img != null)
        {
            img.type = Image.Type.Filled;
            img.fillMethod = fillMethod;
            img.fillOrigin = fillOrigin;
            img.fillClockwise = fillClockwise;
            img.fillAmount = 1f;
        }
    }
}
