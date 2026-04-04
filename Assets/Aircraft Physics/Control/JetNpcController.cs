using System.Collections;
using UnityEngine;

public class JetNpcController : AirplaneController
{
    [Header("AI Settings")]
    public Transform target;

    public float cruiseSpeed = 250f;
    public float combatSpeed = 350f;
    public float crankAngle = 50f; // 39航道偏转角

    [Header("测试用变量")]
    public bool isCombatMode = false;

    private Coroutine activeManeuver; // 用于记录当前运行的机动任务

    protected override void Update()
    {
        // 基础动力维持
        base.thrustPercent = 1.0f;

        if (Input.GetKeyDown(KeyCode.C))
        {
            // 1. 计算目标航向：当前机头水平面上的正后方
            Vector3 targetHeading = -transform.forward;
            targetHeading.y = 0; // 锁定在水平面上
            targetHeading.Normalize();

            // 2. 状态清理：如果已有任务在运行，先停止，防止舵面控制权冲突
            if (activeManeuver != null)
            {
                StopCoroutine(activeManeuver);
                ResetInputs(); // 重置杆量防止残留
            }

            // 3. 启动转冷动作
            activeManeuver = StartCoroutine(Maneuver_TurnCold(targetHeading));

            Debug.Log($"[Test] 触发转冷机动。目标航向: {targetHeading}");
        }
    }

    public IEnumerator Maneuver_TurnCold(Vector3 finalHeading)
    {
        Debug.Log("Maneuver: 开始转冷 (Level 180 Turn)");

        // 阶段 1：建立坡度。目标是侧身 90 度（High-G Turn 的准备姿态） 判定条件：当前坡度与目标坡度误差小于 10 度即进入下一阶段
        while (Mathf.Abs(Mathf.DeltaAngle(GetCurrentRoll(), 90f)) > 10f)
        {
            ApplyRollTask(90f);
            base.Pitch = 0.1f; // 维持极小拉杆，抵消机头下沉
            yield return null;
        }

        // 阶段 2：满偏拉杆转向。 我们利用侧倾后的升力分量来实现最快的水平掉头。 判定条件：机头与目标航向的点积 > 0.99 (约 8 度以内)
        while (Vector3.Dot(transform.forward, finalHeading.normalized) < 0.99f)
        {
            // 持续调用原子函数，物理层会自动处理 90 度的微调和阻尼
            ApplyRollTask(90f);

            // 转向阶段不再使用 PD 追踪，而是直接满偏拉杆以获得最大转弯率
            base.Pitch = 1.0f;

            base.Yaw = 0;
            yield return null;
        }

        // 阶段 3：恢复平飞姿态。 判定条件：坡度回到 5 度以内
        while (Mathf.Abs(GetCurrentRoll()) > 5f)
        {
            ApplyRollTask(0f); // 目标回正到 0 度

            // 使用俯仰任务函数精调高度，指向最终航向
            ApplyPitchTask(finalHeading);
            yield return null;
        }

        ResetInputs(); // 清除杆量，动作结束
        Debug.Log("Maneuver: 转冷完成");
    }

    public void ResetInputs()
    {
        base.Pitch = 0f;
        base.Roll = 0f;
        base.Yaw = 0f;
    }

    // 滚转轴：稳定坡度
    public void ApplyRollTask(float targetBank)
    {
        float current = GetCurrentRoll();
        float error = Mathf.DeltaAngle(current, targetBank);

        // 获取本地 Z 轴角速度 (deg/s)
        float rollVel = transform.InverseTransformDirection(rb.angularVelocity).z * Mathf.Rad2Deg;

        float damping = 0.5f;      // 预测时间常数，越大刹车越早
        float sensitivity = 60f;   // 满偏阈值，越大动作越柔和

        float input = (error - rollVel * damping) / sensitivity;

        // 使用 MoveTowards 模拟舵机偏转速率，彻底过滤物理抖动
        base.Roll = Mathf.MoveTowards(base.Roll, Mathf.Clamp(input, -1f, 1f), Time.deltaTime * 10f);
    }

    // 俯仰轴：追踪目标点高度
    public void ApplyPitchTask(Vector3 worldTargetDir)
    {
        Vector3 localDir = transform.InverseTransformDirection(worldTargetDir.normalized);
        // 计算目标相对于机头的仰角误差
        float pitchError = Mathf.Atan2(localDir.y, localDir.z) * Mathf.Rad2Deg;

        // 获取本地 X 轴角速度 (deg/s)
        float pitchVel = transform.InverseTransformDirection(rb.angularVelocity).x * Mathf.Rad2Deg;

        float damping = 0.25f;     // 俯仰惯性大，阻尼略调高
        float sensitivity = 30f;

        float input = (pitchError - pitchVel * damping) / sensitivity;
        base.Pitch = Mathf.MoveTowards(base.Pitch, Mathf.Clamp(input, -1f, 1f), Time.deltaTime * 8f);
    }

    // 偏航轴：对准 XZ 平面投影
    public void ApplyYawTask(Vector3 worldTargetDir)
    {
        Vector3 localDir = transform.InverseTransformDirection(worldTargetDir.normalized);
        float yawError = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;

        // 获取本地 Y 轴角速度 (deg/s)
        float yawVel = transform.InverseTransformDirection(rb.angularVelocity).y * Mathf.Rad2Deg;

        float damping = 0.2f;
        float sensitivity = 25f;

        float input = (yawError - yawVel * damping) / sensitivity;
        base.Yaw = Mathf.MoveTowards(base.Yaw, Mathf.Clamp(input, -1f, 1f), Time.deltaTime * 10f);
    }

    public float GetCurrentRoll()
    {
        // 1. 获取机身的本地右向量和上向量
        Vector3 localRight = transform.right;
        Vector3 localUp = transform.up;

        // 2. 将世界向上向量投影到飞机的横截面（由 localRight 和 localUp 定义的平面） 计算 transform.up 与 Vector3.up 之间的带符号夹角，围绕机头方向（transform.forward）旋转
        float roll = Vector3.SignedAngle(Vector3.up, localUp, transform.forward);

        // 返回值范围：0 平飞，正值向右翻滚，负值向左翻滚（或根据你的习惯取反）
        return roll;
    }
}
