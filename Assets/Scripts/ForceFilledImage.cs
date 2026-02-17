using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class ForceFilledImage : MonoBehaviour
{
    void Start()
    {
        Image img = GetComponent<Image>();
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = (int)Image.OriginHorizontal.Left;
        img.fillAmount = 1f;
        
        Debug.Log($"ForceFilledImage on {gameObject.name}: type={img.type}, fillMethod={img.fillMethod}, fillAmount={img.fillAmount}");
    }
}
