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
        //Debug.Log("AirSpeed = " + base.rb.linearVelocity.magnitude);
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

        float requiredStableDuration = 0.5f;
        float stableTime = 0f;

        // 记录机动开始时的目标高度，转弯全程尽力维持此高度
        float targetAltitude = transform.position.y;

        // 提取水平面上的目标方向
        Vector3 planarTarget = Vector3.ProjectOnPlane(finalHeading, Vector3.up).normalized;

        // 阶段 1：建立坡度
        while (stableTime < requiredStableDuration)
        {
            float rollError = Mathf.Abs(Mathf.DeltaAngle(GetCurrentRoll(), 60f));

            if (rollError < 5f)
            {
                stableTime += Time.fixedDeltaTime;
            }
            else
            {
                stableTime = 0f;
            }

            ApplyRollTask(60f);
            MaintainTurnAltitude(targetAltitude);
            base.Yaw = 0f;
            yield return new WaitForFixedUpdate();
        }

        Debug.Log("Step 2");
        // 阶段 2：维持高度并等待机头在水平面上对准目标
        Vector3 planarForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

        while (Vector3.Dot(planarForward, planarTarget) < 0.98f)
        {
            planarForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

            // 固定维持第一阶段建立的坡度，不再动态调整
            ApplyRollTask(60f);

            // 仅通过高度偏差来控制俯仰
            MaintainTurnAltitude(targetAltitude);

            // 加入偏航控制辅助水平转向
            //ApplyYawTask(finalHeading);

            yield return new WaitForFixedUpdate();
        }

        Debug.Log("Step 3");
        // 阶段 3：恢复平飞并等待彻底稳定
        stableTime = 0f;
        while (stableTime < requiredStableDuration)
        {
            float rollError = Mathf.Abs(GetCurrentRoll());

            if (rollError < 5f)
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

    // 专属辅助方法：在盘旋时维持高度
    private void MaintainTurnAltitude(float targetAlt)
    {
        float altError = targetAlt - transform.position.y;
        float verticalVel = base.rb.linearVelocity.y;

        // 基础高度PD控制：偏低则拉，下坠则拉
        float pitchDemand = (altError * 0.05f) - (verticalVel * 0.1f);

        // 升力补偿：坡度越大，需要的基础拉力越多
        float currentRollRad = GetCurrentRoll() * Mathf.Deg2Rad;
        float bankComp = (1f / Mathf.Max(Mathf.Cos(currentRollRad), 0.2f)) - 1f;
        pitchDemand += bankComp * 0.15f;

        // 空速衰减保护
        float currentSpeed = base.rb.linearVelocity.magnitude;
        float maxPitch = Mathf.Clamp01((currentSpeed - 60f) / 60f);

        // 允许微弱推杆防止爬升过高，限制最大拉力防止失速
        float targetPitch = Mathf.Clamp(pitchDemand, -0.2f, maxPitch);

        base.Pitch = Mathf.MoveTowards(base.Pitch, targetPitch, Time.fixedDeltaTime * 3.0f);
    }

    public void ApplyRollTask(float targetBank)
    {
        float current = GetCurrentRoll();
        float error = Mathf.DeltaAngle(current, targetBank);
        float rollVel = transform.InverseTransformDirection(rb.angularVelocity).z * Mathf.Rad2Deg;

        // 1. 基础误差映射
        float errorThreshold = 45f;
        float normalizedError = Mathf.Clamp(error / errorThreshold, -1f, 1f);
        float powerInput = Mathf.Sign(normalizedError) * Mathf.Pow(Mathf.Abs(normalizedError), 1.5f);

        // 2. 阻尼计算
        float maxExpectedVelocity = 120f;
        float normalizedVel = Mathf.Clamp(rollVel / maxExpectedVelocity, -1f, 1f);
        float dampingCoefficient = 0.35f;
        float dampingForce = normalizedVel * dampingCoefficient;

        // 3. 原始合成输入与起步量
        float rawInput = powerInput - dampingForce;
        if (Mathf.Abs(error) > 2.0f && Mathf.Abs(rawInput) < 0.05f)
        {
            rawInput = Mathf.Sign(error) * 0.05f;
        }

        // --- 核心改动：基于动压的控制律衰减 (Gain Scheduling) ---
        float currentAirspeed = rb.linearVelocity.magnitude;
        float baseSpeed = 200f; // 以你测试手感最好的 200 空速为基准

        // 计算速度比。使用 Max 保证在低速（<200）时不会反向放大杆量导致失速抖动
        float speedRatio = Mathf.Max(currentAirspeed / baseSpeed, 1f);

        // 气动力与速度的平方成正比，因此理论上杆量需要除以速度比的平方。 为了保留一点高机动性，这里使用 1.5 到 2.0 次方进行衰减。
        float dynamicAttenuation = 1f / Mathf.Pow(speedRatio, 1.8f);

        // 将衰减系数应用到输入上（在 400 空速时，输入量会自动缩小到原本的约 28%）
        float finalInput = rawInput * dynamicAttenuation;

        // --- 核心改动：动态打杆速率限制 --- 在 200 空速可以瞬间推满杆 (5.0f) 在 400 空速必须柔和推杆 (可能只需 1.5f)，防止气动力瞬间冲击导致物理引擎震荡
        float stickMoveRate = Mathf.Lerp(5.0f, 1.5f, (currentAirspeed - baseSpeed) / 200f);
        stickMoveRate = Mathf.Clamp(stickMoveRate, 1.0f, 5.0f);

        // 5. 应用最终输入
        base.Roll = Mathf.MoveTowards(base.Roll, Mathf.Clamp(finalInput, -1f, 1f), Time.fixedDeltaTime * stickMoveRate);

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

    // 获取当前的俯仰正弦值（直接反映机头高低）
    public float GetSecurePitch()
    {
        return transform.forward.y * 90f; // 简单映射到 -90 到 90
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
