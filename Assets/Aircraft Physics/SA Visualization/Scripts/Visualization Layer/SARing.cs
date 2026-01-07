using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SARing : MonoBehaviour
{
    [Header("感知环参数")]
    [Range(0.0f, 0.5f)]
    public float arcInnerRadius = 0.25f;

    [Range(0.0f, 0.5f)]
    public float arcOuterRadius = 0.5f;

    [Header("SA Ring References")]
    public GameObject leftWingArc, tailArc, rightWingArc, bestTurnRateArc, psBall;

    public float Ps = 0;

    public struct SpData
    {
        public float leftWingSp, leftTailWingSp, rightTailWingSp, rightWingSp;
    }

    public struct ManeuverEfficiencyData
    {
        public float countinuousManeuverEfficiency;
        public bool isContinuousManeuverEfficiency;
        public bool isMaxInstantaneousManeuverEfficiency;
    }

    // SA环材质
    private Material leftWingArcMat, tailArcMat, rightWingArcMat, bestTurnRateArcMat, psBallMat;

    // SA环显示数据
    private float displayPs;

    private SpData displaySpData;
    private ManeuverEfficiencyData displayMeData;

    private void Start()
    {
        leftWingArcMat = leftWingArc.GetComponent<Image>().material;
        tailArcMat = tailArc.GetComponent<Image>().material;
        rightWingArcMat = rightWingArc.GetComponent<Image>().material;
        bestTurnRateArcMat = bestTurnRateArc.GetComponent<Image>().material;
        psBallMat = psBall.GetComponent<Image>().material;
    }

    private void Update()
    {
        RefreshUIRendering();
    }

    public void SetSAVisualizationData(float deltaEnergyHeight,
    SpData spData,
    ManeuverEfficiencyData meData)
    {
        // Debug.Log("SetSAVisualizationData");
        displayPs = deltaEnergyHeight;
        displaySpData = spData;
        displayMeData = meData;
    }

    private void RefreshUIRendering()
    {
        // 材质更新
        Color psColor = Color.Lerp(Color.red, Color.green, (displayPs + 1f) * 0.5f);
        psBallMat.SetColor("_Color", psColor);

        leftWingArcMat.SetColor("_Color", Color.Lerp(Color.green, Color.red, Mathf.Abs(displaySpData.leftWingSp)));
        tailArcMat.SetColor("_Color", Color.Lerp(Color.green, Color.red, Mathf.Abs(displaySpData.leftTailWingSp)));
        rightWingArcMat.SetColor("_Color", Color.Lerp(Color.green, Color.red, Mathf.Abs(displaySpData.rightWingSp)));
        if (displayMeData.isContinuousManeuverEfficiency == true)
        {
            Color color = bestTurnRateArcMat.GetColor("_Color");
            color.a = 1.0f;
            bestTurnRateArcMat.SetColor("_Color", color);
        }
        else
        {
            Color color = bestTurnRateArcMat.GetColor("_Color");
            color.a = 0.0f;
            bestTurnRateArcMat.SetColor("_Color", color);
        }
        // Debug.Log("Eff:" + displayMeData.countinuousManeuverEfficiency);
        float innerRadius = Mathf.Lerp(0.25f, 0.35f, (displayMeData.countinuousManeuverEfficiency));
        float outerRadius = Mathf.Lerp(0.35f, 0.5f, (displayMeData.countinuousManeuverEfficiency));
        leftWingArcMat.SetFloat("_InnerRadius", innerRadius);
        leftWingArcMat.SetFloat("_OuterRadius", outerRadius);
        tailArcMat.SetFloat("_InnerRadius", innerRadius);
        tailArcMat.SetFloat("_OuterRadius", outerRadius);
        rightWingArcMat.SetFloat("_InnerRadius", innerRadius);
        rightWingArcMat.SetFloat("_OuterRadius", outerRadius);
        bestTurnRateArcMat.SetFloat("_InnerRadius", innerRadius);
        bestTurnRateArcMat.SetFloat("_OuterRadius", outerRadius);
    }
}
