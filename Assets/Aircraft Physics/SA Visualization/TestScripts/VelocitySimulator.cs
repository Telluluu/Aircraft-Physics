using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class VelocitySimulator : MonoBehaviour
{
    [Header("Base Settings")]
    public float baseSpeed = 200.0f; // 基础速度 (m/s)

    [Header("Maneuver Simulation")]
    public bool enableManeuver = true;

    public float turnAmplitude = 45f;  // 转向摆动幅度 (度)
    public float speedVariation = 50f; // 速度波动幅度 (m/s)
    public float maneuverFrequency = 0.5f; // 机动频率 (Hz)

    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        // 确保物理引擎不干扰我们的手动速度控制
        rb.useGravity = false;
    }

    private void FixedUpdate() // 物理速度建议在 FixedUpdate 更新
    {
        if (!enableManeuver)
        {
            // 静态直线飞行
            rb.linearVelocity = transform.forward * baseSpeed;
            return;
        }

        // 1. 计算随时间变化的正弦值 (-1 到 1)
        float wave = Mathf.Sin(Time.time * maneuverFrequency * 2 * Mathf.PI);

        // 2. 模拟速度大小的变化 (目标在加速/减速)
        float currentSpeed = baseSpeed + (wave * speedVariation);

        // 3. 模拟方向偏转 (目标在左右蛇行)
        float yawOffset = wave * turnAmplitude;
        Quaternion rotation = Quaternion.Euler(0, yawOffset, 0);
        Vector3 direction = rotation * Vector3.forward;

        // 4. 应用速度
        rb.linearVelocity = direction * currentSpeed;

        // 顺便更新朝向，方便你在 Scene 窗口观察
        if (rb.linearVelocity.magnitude > 0.1f)
        {
            transform.forward = rb.linearVelocity.normalized;
        }
    }
}
