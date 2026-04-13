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

        float requiredStableDuration = 0.5f; // 姿态需要持续稳定的时间（秒），可根据手感微调
        float stableTime = 0f;

        // 阶段 1：建立坡度并等待彻底稳定。
        while (stableTime < requiredStableDuration)
        {
            float rollError = Mathf.Abs(Mathf.DeltaAngle(GetCurrentRoll(), 90f));
            Debug.Log("rollError = " + rollError);
            Debug.Log("CurrentError = " + GetCurrentRoll());
            Debug.Log("stableTime = " + stableTime);
            // 误差小于5度开始计时，一旦偏离重置计时
            if (rollError < 15)
            {
                stableTime += Time.fixedDeltaTime;
            }
            else
            {
                stableTime = 0f;
            }

            ApplyRollTask(90f);
            base.Pitch = 0.1f;
            base.Yaw = 0f;
            yield return new WaitForFixedUpdate();
        }

        // 阶段 2：满偏拉杆转向。
        while (Vector3.Dot(transform.forward, finalHeading.normalized) < 0.9f)
        {
            ApplyRollTask(90f);
            base.Pitch = 1.0f;
            base.Yaw = 0f;
            yield return new WaitForFixedUpdate();
        }

        // 阶段 3：恢复平飞姿态并等待彻底稳定。
        stableTime = 0f; // 重置计时器
        while (stableTime < requiredStableDuration)
        {
            float rollError = Mathf.Abs(GetCurrentRoll());

            // 误差小于5度开始计时
            if (rollError < 15f)
            {
                stableTime += Time.fixedDeltaTime;
            }
            else
            {
                stableTime = 0f;
            }

            ApplyRollTask(0f);
            ApplyPitchTask(finalHeading);
            base.Yaw = 0f;
            yield return new WaitForFixedUpdate();
        }

        ResetInputs();
        Debug.Log("Maneuver: 转冷完成");
    }

    // 滚转轴：稳定坡度
    public void ApplyRollTask(float targetBank)
    {
        float current = GetCurrentRoll();
        float error = Mathf.DeltaAngle(current, targetBank);
        float rollVel = transform.InverseTransformDirection(rb.angularVelocity).z * Mathf.Rad2Deg;

        // 1. 误差归一化（将阈值调小，让曲线在 90 度以内更有斜率）
        float errorThreshold = 90f;
        float normalizedError = Mathf.Clamp(error / errorThreshold, -1f, 1f);

        // 使用 3 次方，保证小角度极其平缓
        float powerInput = Mathf.Pow(normalizedError, 3f);

        // 2. 阻尼归一化 假设飞机最大翻滚速度为 180 deg/s，将其映射到 0-1 范围
        float maxExpectedVelocity = 180f;
        float normalizedVel = Mathf.Clamp(rollVel / maxExpectedVelocity, -1f, 1f);

        // 阻尼系数不应超过 1.0，通常 0.2-0.5 足够防止震荡
        float dampingCoefficient = 0.4f;
        float dampingForce = normalizedVel * dampingCoefficient;

        // 3. 合成输入
        float finalInput = powerInput - dampingForce;

        // 4. 限制导数（保持物理平滑）
        base.Roll = Mathf.MoveTowards(base.Roll, Mathf.Clamp(finalInput, -1f, 1f), Time.fixedDeltaTime * 2.0f);

        // 5. 强制死区
        if (Mathf.Abs(error) < 1.0f && Mathf.Abs(rollVel) < 1.0f)
        {
            base.Roll = 0f;
        }
    }

    // 俯仰轴：追踪目标点高度
    public void ApplyPitchTask(Vector3 worldTargetDir)
    {
        Vector3 localDir = transform.InverseTransformDirection(worldTargetDir.normalized);
        float pitchError = Mathf.Atan2(localDir.y, localDir.z) * Mathf.Rad2Deg;

        if (Mathf.Abs(pitchError) < 1.5f)
        {
            base.Pitch = 0f;
            return;
        }

        float pitchVel = transform.InverseTransformDirection(rb.angularVelocity).x * Mathf.Rad2Deg;

        float damping = 0.4f;      // 调高阻尼
        float sensitivity = 30f;

        float input = (pitchError - pitchVel * damping) / sensitivity;
        base.Pitch = Mathf.Clamp(input, -1f, 1f);
    }

    // 偏航轴：对准 XZ 平面投影
    public void ApplyYawTask(Vector3 worldTargetDir)
    {
        Vector3 localDir = transform.InverseTransformDirection(worldTargetDir.normalized);
        float yawError = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;

        if (Mathf.Abs(yawError) < 1.5f)
        {
            base.Yaw = 0f;
            return;
        }

        float yawVel = transform.InverseTransformDirection(rb.angularVelocity).y * Mathf.Rad2Deg;

        float damping = 0.3f;
        float sensitivity = 25f;

        float input = (yawError - yawVel * damping) / sensitivity;
        base.Yaw = Mathf.Clamp(input, -1f, 1f);
    }

    public void ResetInputs()
    {
        base.Pitch = 0f;
        base.Roll = 0f;
        base.Yaw = 0f;
    }

    public float GetCurrentRoll()
    {
        // 1. 获取机身的本地右向量和上向量
        Vector3 localRight = transform.right;
        Vector3 localUp = transform.up;

        // 2. 将世界向上向量投影到飞机的横截面（由 localRight 和 localUp 定义的平面） 计算 transform.up 与 Vector3.up 之间的带符号夹角，围绕机头方向（transform.forward）旋转
        float roll = Vector3.SignedAngle(localUp, Vector3.up, transform.forward);

        // 返回值范围：0 平飞，正值向右翻滚，负值向左翻滚
        return roll;
    }
}
