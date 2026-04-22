using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class LockonFrame : MonoBehaviour
{
    public Image lockonFrameImage;
    private Material _instanceMaterial;
    public TMP_Text velocityText;
    public TMP_Text distanceText;

    private void Awake()
    {
        _instanceMaterial = Instantiate(lockonFrameImage.material);
        lockonFrameImage.material = _instanceMaterial;
        UnSelected();
    }

    public void Selected()
    {
        _instanceMaterial.SetColor("_Color", Color.red);
    }

    public void UnSelected()
    {
        _instanceMaterial.SetColor("_Color", Color.green);
    }

    public void SetText(float velocity, float distance)
    {
        velocityText.text = velocity.ToString("F2") + " m/s";
        distanceText.text = (distance / 1000.0f).ToString("F2") + " km";
    }
}
