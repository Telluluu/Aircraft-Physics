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
            float rollError = Mathf.Abs(Mathf.DeltaAngle(GetCurrentRoll(), 85f));

            if (rollError < 2f)
            {
                stableTime += Time.fixedDeltaTime;
            }
            else
            {
                stableTime = 0f;
            }

            ApplyRollTask(85f);
            MaintainTurnAltitude(targetAltitude);
            base.Yaw = 0f;
            yield return new WaitForFixedUpdate();
        }

        Debug.Log("Step 2");
        // 阶段 2：维持高度并等待机头在水平面上对准目标
        Vector3 planarForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

        while (Vector3.Dot(planarForward, planarTarget) < 0.99f)
        {
            planarForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

            // 固定维持第一阶段建立的坡度，不再动态调整
            ApplyRollTask(85f);

            // 仅通过高度偏差来控制俯仰
            MaintainTurnAltitude(targetAltitude);

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
            ApplyPitchTask(0f);
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

        // 1. 显著提高 PD 增益：让垂直速率的需求更饥渴
        float desiredVSpeed = (altError * 3.0f) - (verticalVel * 0.4f);
        desiredVSpeed = Mathf.Clamp(desiredVSpeed, -40f, 50f); // 放开限速

        // 2. 映射为本地俯仰需求
        float currentRollRad = GetCurrentRoll() * Mathf.Deg2Rad;
        float cosRoll = Mathf.Max(Mathf.Cos(currentRollRad), 0.1f);
        float pitchAngleDemand = desiredVSpeed / cosRoll;

        // 3. 获取本地角速度
        float pitchVel = transform.InverseTransformDirection(rb.angularVelocity).x * Mathf.Rad2Deg;

        // 4. 降低 errorThreshold：让满杆阈值变小（15度就打满杆）
        float errorThreshold = 15f;
        float normalizedError = Mathf.Clamp(pitchAngleDemand / errorThreshold, -1f, 1f);

        // 降低幂次：从 1.5 降到 1.1，接近线性，小误差时也会给大杆量
        float powerInput = Mathf.Sign(normalizedError) * Mathf.Pow(Mathf.Abs(normalizedError), 1.1f);

        // 维持阻尼：防止由于增益过大导致的机头颤动
        float damping = (pitchVel / 90f) * 0.5f;

        // 注意：这里要符合“负值为拉杆”的映射逻辑 如果 pitchAngleDemand > 0 (需要抬头)，powerInput 为正，rawInput 为负（拉杆）
        float rawInput = damping - (powerInput * 1.5f); // 基础倍率直接给 1.5 倍

        // 5. 降低动压衰减的影响：让高速下依然保留更多控制力
        float currentAirspeed = rb.linearVelocity.magnitude;
        float speedRatio = Mathf.Max(currentAirspeed / 200f, 1f);
        float dynamicAttenuation = 1f / Mathf.Pow(speedRatio, 1.2f); // 1.8 降到 1.2

        float finalInput = rawInput * dynamicAttenuation;

        // 6. 放开杆量限制：允许瞬间推/拉到极限
        finalInput = Mathf.Clamp(finalInput, -1.0f, 1.0f);

        // 极速打杆：MoveTowards 系数从 5.0 提到 10.0
        base.Pitch = Mathf.MoveTowards(base.Pitch, finalInput, Time.fixedDeltaTime * 10.0f);
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

    public void ApplyPitchTask(float targetPitch)
    {
        float current = GetSecurePitch();
        float error = Mathf.DeltaAngle(current, targetPitch);
        float pitchVel = transform.InverseTransformDirection(rb.angularVelocity).x * Mathf.Rad2Deg;

        // 1. 减小满杆阈值：15度误差就给满杆
        float errorThreshold = 15f;
        float normalizedError = Mathf.Clamp(error / errorThreshold, -1f, 1f);

        // 2. 使用线性或低幂次：确保小角度时依然有极强修正力
        float powerInput = Mathf.Sign(normalizedError) * Mathf.Pow(Mathf.Abs(normalizedError), 1.0f);

        // 3. 增强阻尼，抵消大增益带来的超调
        float dampingForce = Mathf.Clamp(pitchVel / 90f, -1f, 1f) * 0.6f;

        // 4. 合成输入
        float rawInput = dampingForce - (powerInput * 1.2f);

        // 增加起步 Bias：只要误差大于 0.5 度，至少给 0.1 的杆量
        if (Mathf.Abs(error) > 0.5f && Mathf.Abs(rawInput) < 0.1f)
        {
            rawInput = -Mathf.Sign(error) * 0.1f;
        }

        // 5. 动压衰减同样调平缓
        float currentAirspeed = rb.linearVelocity.magnitude;
        float speedRatio = Mathf.Max(currentAirspeed / 200f, 1f);
        float dynamicAttenuation = 1f / Mathf.Pow(speedRatio, 1.2f);

        // 6. 应用最终输入
        float finalInput = rawInput * dynamicAttenuation;

        // 瞬时响应：MoveTowards 提升到 8.0
        base.Pitch = Mathf.MoveTowards(base.Pitch, Mathf.Clamp(finalInput, -1f, 1f), Time.fixedDeltaTime * 8.0f);

        // 极小的死区
        if (Mathf.Abs(error) < 0.2f && Mathf.Abs(pitchVel) < 0.5f)
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
