using Unity.Android.Gradle.Manifest;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(AircraftPhysics))]
public class AerodynamicPerformance : MonoBehaviour
{
    private Rigidbody rb;

    [Header("感知环")]
    public SARing saRing;

    // 能量感知
    [Header("能量感知")]
    public float lastEnergyHeight = 0;

    public float currentEnergyHeight = 0;
    public float deltaEnergyHeight = 0;
    public float calculateFrequency = 0.5f;
    private float timer = 0;

    // 失速预警 左右翼尖，左右、垂直尾翼失速接近率
    [Header("失速预警")]
    public float leftWingTipStallProximity, rightWingTipStallProximity,
        leftTailStallProximity, verticalTailStallProximity, rightTailStallProximity = 0;

    // 左右翼尖，左右、垂直尾翼
    public AeroSurface leftWing, rightWing, leftTail, verticalTail, rightTail;

    // 机动效率 持续机动效率越接近0效率越高，大于0说明攻角过大，小于0说明攻角过小
    [Header("机动效率")]
    public float countinuousManeuverEfficiency = 0;

    public bool isContinuousManeuverEfficiency = false;

    // 是否处于瞬时机动效率最高时
    public bool isMaxInstantaneousManeuverEfficiency = false;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        currentEnergyHeight = rb.transform.position.y +
            rb.linearVelocity.magnitude * rb.linearVelocity.magnitude / Physics.gravity.magnitude;
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= calculateFrequency)
        {
            CalculateDeltaEnergyHeight();
            CalculateShallProximity();
            CheckManeuverEfficiency();
            SARing.SpData spData = new()
            {
                leftWingSp = leftWingTipStallProximity,
                leftTailWingSp = leftTailStallProximity,
                rightTailWingSp = rightTailStallProximity,
                rightWingSp = rightWingTipStallProximity
            };

            SARing.ManeuverEfficiencyData meData = new()
            {
                countinuousManeuverEfficiency = this.countinuousManeuverEfficiency,
                isContinuousManeuverEfficiency = this.isContinuousManeuverEfficiency,
                isMaxInstantaneousManeuverEfficiency = this.isContinuousManeuverEfficiency
            };
            saRing.SetSAVisualizationData(deltaEnergyHeight, spData, meData);
            timer = 0;
        }
    }

    private void CalculateDeltaEnergyHeight()
    {
        currentEnergyHeight = rb.transform.position.y +
            rb.linearVelocity.magnitude * rb.linearVelocity.magnitude / Physics.gravity.magnitude;

        deltaEnergyHeight = currentEnergyHeight - lastEnergyHeight;
        lastEnergyHeight = currentEnergyHeight;
    }

    private void CalculateShallProximity()
    {
        leftWingTipStallProximity = CalculateStallProximity(leftWing.CurrentAoA, leftWing.DynamicZeroLiftAoA,
            leftWing.DynamicStallHigh, leftWing.DynamicStallLow);
        rightWingTipStallProximity = CalculateStallProximity(rightWing.CurrentAoA, rightWing.DynamicZeroLiftAoA,
            rightWing.DynamicStallHigh, rightWing.DynamicStallLow);
        leftTailStallProximity = CalculateStallProximity(leftTail.CurrentAoA, leftTail.DynamicZeroLiftAoA,
            leftTail.DynamicStallHigh, leftTail.DynamicStallLow);
        verticalTailStallProximity = CalculateStallProximity(verticalTail.CurrentAoA, verticalTail.DynamicZeroLiftAoA,
            verticalTail.DynamicStallHigh, verticalTail.DynamicStallLow);
        rightWingTipStallProximity = CalculateStallProximity(rightTail.CurrentAoA, rightTail.DynamicZeroLiftAoA,
            rightTail.DynamicStallHigh, rightTail.DynamicStallLow);
    }

    // Sp处以[0,1]时，攻角为正，Sp处于[-1,0]时，攻角为负，俯冲时高度换速度可以改出失速，所以负攻角预警只考虑倒飞
    private float CalculateStallProximity(float AoA, float zeroLiftAoa, float stallAngleHigh, float stallAngleLow)
    {
        float sp = 0;
        // 区分正负攻角的失速情况
        if (AoA >= zeroLiftAoa)
        {
            sp = (AoA - zeroLiftAoa) / (stallAngleHigh - zeroLiftAoa);
        }
        else
        {
            sp = -((AoA - zeroLiftAoa) / (stallAngleLow - zeroLiftAoa));
        }
        return Mathf.Clamp(sp, -1f, 1f);
    }

    // 机动效率判断
    private void CheckManeuverEfficiency()
    {
        float sp = Mathf.Max(leftWingTipStallProximity, rightWingTipStallProximity);
        // 当单位剩余功率为正，且Sp处于0.5~0.7时，为最佳持续转弯率
        if (deltaEnergyHeight > 0 && sp > 0.5f && sp < 0.7f)
        {
            countinuousManeuverEfficiency = (sp - 0.6f) / 0.3f;
            isContinuousManeuverEfficiency = true;
        }
        else
        {
            isContinuousManeuverEfficiency = false;
        }

        if (sp < 1.0f && sp > 0.9f)
        {
            isMaxInstantaneousManeuverEfficiency = true;
        }
        else
        {
            isMaxInstantaneousManeuverEfficiency = false;
        }
    }
}
