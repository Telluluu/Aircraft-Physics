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
            float rollError = Mathf.Abs(Mathf.DeltaAngle(GetCurrentRoll(), 50f));
            Debug.Log("rollError = " + rollError);
            Debug.Log("CurrentError = " + GetCurrentRoll());
            Debug.Log("stableTime = " + stableTime);
            // 误差小于5度开始计时，一旦偏离重置计时
            if (rollError < 2)
            {
                stableTime += Time.fixedDeltaTime;
            }
            else
            {
                stableTime = 0f;
            }

            ApplyRollTask(50f);
            base.Pitch = 0.1f;
            base.Yaw = 0f;
            yield return new WaitForFixedUpdate();
        }
        Debug.Log("Step2");
        // 阶段 2：能量优先的转向控制
        while (Vector3.Dot(transform.forward, finalHeading.normalized) < 0.98f)
        {
            float currentAirspeed = base.rb.linearVelocity.magnitude;

            // --- 核心改动 1：动态坡度限制 --- 如果速度不足以维持 50 度转弯，自动放平翅膀恢复升力
            float safeRoll = (currentAirspeed < 120f) ? 30f : 50f;
            ApplyRollTask(safeRoll);

            float pitchVel = transform.InverseTransformDirection(base.rb.angularVelocity).x * Mathf.Rad2Deg;
            Vector3 localDir = transform.InverseTransformDirection(finalHeading.normalized);
            float pitchError = Mathf.Atan2(localDir.y, localDir.z) * Mathf.Rad2Deg;

            // --- 核心改动 2：垂直速率感知 ---
            float verticalVel = base.rb.linearVelocity.y;
            float climbComp = 0f;
            if (verticalVel < -2f) // 只要每秒下降超过 2 米
            {
                climbComp = Mathf.Abs(verticalVel) * 0.2f;
            }

            // --- 核心改动 3：更稳健的 Pitch 合成 ---
            float basePull = 0.25f;
            float trackingPull = Mathf.Clamp(pitchError / 20f, 0f, 0.5f); // 限制追踪项权重
            float gravityComp = Mathf.Clamp01(Mathf.Abs(Mathf.Min(0, transform.forward.y)) * 1.2f);

            float rawPitch = basePull + trackingPull + gravityComp + climbComp;

            // --- 核心改动 4：平滑的失速保护 --- 确保速度掉下来时，拉杆量平滑衰减，不要让机头剧烈“点头”
            float speedWeight = Mathf.SmoothStep(0f, 1f, (currentAirspeed - 60f) / 60f);

            float targetPitch = Mathf.Clamp(rawPitch * speedWeight, 0f, 1.0f);

            // 降低 MoveTowards 速率，防止舵面震荡引发物理引擎发疯
            base.Pitch = Mathf.MoveTowards(base.Pitch, targetPitch, Time.fixedDeltaTime * 3.0f);

            base.Yaw = 0f;
            yield return new WaitForFixedUpdate();
        }
        Debug.Log("Step 3");
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

    //滚转轴：稳定坡度
    //public void ApplyRollTask(float targetBank)
    //{
    //    float current = GetCurrentRoll();
    //    float error = Mathf.DeltaAngle(current, targetBank);
    //    float rollVel = transform.InverseTransformDirection(rb.angularVelocity).z * Mathf.Rad2Deg;

    // // 1. 误差归一化（将阈值调小，让曲线在 90 度以内更有斜率） float errorThreshold = 90f; float normalizedError =
    // Mathf.Clamp(error / errorThreshold, -1f, 1f);

    // // 使用 3 次方，保证小角度极其平缓 float powerInput = Mathf.Pow(normalizedError, 3f);

    // // 2. 阻尼归一化 假设飞机最大翻滚速度为 180 deg/s，将其映射到 0-1 范围 float maxExpectedVelocity = 180f; float
    // normalizedVel = Mathf.Clamp(rollVel / maxExpectedVelocity, -1f, 1f);

    // // 阻尼系数不应超过 1.0，通常 0.2-0.5 足够防止震荡 float dampingCoefficient = 0.4f; float dampingForce =
    // normalizedVel * dampingCoefficient;

    // // 3. 合成输入 float finalInput = powerInput - dampingForce;

    // // 4. 限制导数（保持物理平滑） base.Roll = Mathf.MoveTowards(base.Roll, Mathf.Clamp(finalInput, -1f, 1f),
    // Time.fixedDeltaTime * 2.0f);

    //    // 5. 强制死区
    //    if (Mathf.Abs(error) < 1.0f && Mathf.Abs(rollVel) < 1.0f)
    //    {
    //        base.Roll = 0f;
    //    }
    //}

    public void ApplyRollTask(float targetBank)
    {
        float current = GetCurrentRoll();
        float error = Mathf.DeltaAngle(current, targetBank);
        float rollVel = transform.InverseTransformDirection(rb.angularVelocity).z * Mathf.Rad2Deg;

        // 1. 降低阈值：让控制器在更小的角度范围内就感到“急迫”
        float errorThreshold = 45f;
        float normalizedError = Mathf.Clamp(error / errorThreshold, -1f, 1f);

        // 2. 降低幂次：从 3 次方降到 1.5 次方，提升小角度响应 使用 Sign 保持方向
        float powerInput = Mathf.Sign(normalizedError) * Mathf.Pow(Mathf.Abs(normalizedError), 1.5f);

        // 3. 动态阻尼归一化
        float maxExpectedVelocity = 120f; // 调低预期速度上限，使阻尼感更清晰
        float normalizedVel = Mathf.Clamp(rollVel / maxExpectedVelocity, -1f, 1f);
        float dampingCoefficient = 0.35f;
        float dampingForce = normalizedVel * dampingCoefficient;

        // 4. 合成输入：加入最小起步量（Bias） 只要误差存在且角速度较小，就给一个保底的纠正力
        float finalInput = powerInput - dampingForce;

        if (Mathf.Abs(error) > 2.0f && Mathf.Abs(finalInput) < 0.05f)
        {
            finalInput = Mathf.Sign(error) * 0.05f;
        }

        // 5. 提高变化速率：让舵面反应更快，2.0 提升到 5.0
        base.Roll = Mathf.MoveTowards(base.Roll, Mathf.Clamp(finalInput, -1f, 1f), Time.fixedDeltaTime * 5.0f);

        // 6. 精细死区
        if (Mathf.Abs(error) < 0.8f && Mathf.Abs(rollVel) < 1.0f)
        {
            base.Roll = 0f;
        }
    }

    public void ApplyPitchTask(Vector3 worldTargetDir)
    {
        Vector3 localDir = transform.InverseTransformDirection(worldTargetDir.normalized);
        // 计算俯仰误差
        float pitchError = Mathf.Atan2(localDir.y, localDir.z) * Mathf.Rad2Deg;
        float pitchVel = transform.InverseTransformDirection(rb.angularVelocity).x * Mathf.Rad2Deg;

        // 指数映射：阈值通常设为 45-60 度
        float errorThreshold = 45f;
        float normalizedError = Mathf.Clamp(pitchError / errorThreshold, -1f, 1f);
        float powerInput = Mathf.Pow(normalizedError, 3f);

        // 阻尼归一化：俯仰最大角速度通常比滚转小
        float maxExpectedVel = 90f;
        float dampingCoefficient = 0.3f;
        float dampingForce = Mathf.Clamp(pitchVel / maxExpectedVel, -1f, 1f) * dampingCoefficient;

        float finalInput = powerInput - dampingForce;

        // 限制导数：俯仰响应通常比滚转慢，MoveTowards 系数可以略小
        base.Pitch = Mathf.MoveTowards(base.Pitch, Mathf.Clamp(finalInput, -1f, 1f), Time.fixedDeltaTime * 2.5f);

        if (Mathf.Abs(pitchError) < 1.0f && Mathf.Abs(pitchVel) < 1.0f)
        {
            base.Pitch = 0f;
        }
    }

    // 偏航轴：对准 XZ 平面投影
    public void ApplyYawTask(Vector3 worldTargetDir)
    {
        Vector3 localDir = transform.InverseTransformDirection(worldTargetDir.normalized);
        float yawError = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
        float yawVel = transform.InverseTransformDirection(rb.angularVelocity).y * Mathf.Rad2Deg;

        // 偏航阈值设小，让其在小角度更敏感
        float errorThreshold = 30f;
        float normalizedError = Mathf.Clamp(yawError / errorThreshold, -1f, 1f);

        // 使用 5 次方：极其平缓的中心区域
        float powerInput = Mathf.Pow(normalizedError, 5f);

        float maxExpectedVel = 60f;
        float dampingForce = Mathf.Clamp(yawVel / maxExpectedVel, -1f, 1f) * 0.2f;

        float finalInput = powerInput - dampingForce;

        base.Yaw = Mathf.MoveTowards(base.Yaw, Mathf.Clamp(finalInput, -1f, 1f), Time.fixedDeltaTime * 2.0f);

        if (Mathf.Abs(yawError) < 1.0f && Mathf.Abs(yawVel) < 1.0f)
        {
            base.Yaw = 0f;
        }
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
