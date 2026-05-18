using System;
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

    private Coroutine maneuverRoutine; // 用于记录当前运行的机动任务

    public IFF myIFF;
    public DopplerRadar radar;
    public MissileLauncher missileLauncher;

    protected override void Update()
    {
        // 基础动力维持
        if (isCombatMode)
            ControlThrottle(combatSpeed);
        float currentAlt = transform.position.y;
        if (Input.GetKeyDown(KeyCode.V))
        {
            // Split-S maneuverRoutine = StartCoroutine(Maneuver_Relative(180f, 180f, 5f));
            maneuverRoutine = StartCoroutine(Maneuver_Relative(135f, 180f, 5f));
            // High Yoyo

            // Low Yoyo
        }
        else if (Input.GetKeyDown(KeyCode.C))
        {
            // Turn cold
            maneuverRoutine = StartCoroutine(Maneuver_Relative(85f, 180f, 5f));
        }
        else if (Input.GetKeyDown(KeyCode.B))
        {
            maneuverRoutine = StartCoroutine(Maneuver_Relative(175f, 180f, 5f));
        }
        //if (base.rb.linearVelocity.magnitude < 300f)
        //    Debug.Log("Speed = " + base.rb.linearVelocity.magnitude);
    }

    /// <summary>
    /// 控速核心函数
    /// </summary>
    /// <param name="targetSpeed">期望维持的目标速度（传入 220 到 500 之间的值）</param>
    public void ControlThrottle(float targetSpeed)
    {
        // 安全限幅：确保传入的目标速度在你的 220~500 范围内
        targetSpeed = Mathf.Clamp(targetSpeed, 220f, 500f);

        // 获取当前飞机的真实空速
        float currentSpeed = rb.linearVelocity.magnitude;

        // 计算速度误差
        float speedError = targetSpeed - currentSpeed;

        // 固定的推力调整速率（每秒最多增减 0.5，防止推力剧烈抖动导致物理引擎打摆子）
        float throttleChangeRate = 0.5f;

        if (Mathf.Abs(speedError) > 5f) // 允许 5 码 的死区，避免油门过于敏感
        {
            // 如果当前速度低于目标，增加推力；反之减小推力
            float targetThrust = base.thrustPercent + Mathf.Sign(speedError) * throttleChangeRate * Time.deltaTime;

            // 限制油门在 0.0 (空闲) 到 1.0 (满油门) 之间
            base.thrustPercent = Mathf.Clamp(targetThrust, 0.0f, 1.0f);
        }

        // 防失速/防超速
        if (currentSpeed < 180f)
        {
            base.thrustPercent = 1.0f; // 极端低速下强制满油门门防止栽机
        }
        else if (currentSpeed > 550f)
        {
            base.thrustPercent = 0.0f; // 极端高速下强制关油门
        }
    }

    /// <summary>
    /// 无边界防飞丢的原地盘旋巡航
    /// </summary>
    public void CruiseMode()
    {
        // 1. 让飞机平稳地向右压坡度并转弯（利用你现有的横滚和偏航接口） 传入一个固定的右转矢量：当前右方和前方的中间值，让它一直转弯
        Vector3 circleDir = (transform.forward + transform.right).normalized;
        //ApplyYawTask(circleDir);

        // 2. 维持当前高度（防止巡航时栽下去）
        float targetCruiseAltitude = 2000f; // 设定一个初始高度
        float altitudeError = targetCruiseAltitude - transform.position.y;
        float targetPitch = Mathf.Clamp(altitudeError * 0.1f, -10f, 10f);
        ApplyRollTask(0f);
        ApplyPitchTask(targetPitch);

        // 3. 稍微给一点点横滚维持转弯姿态（可选，如果只用 Yaw 能转动，这里也可以直接改平） ApplyRollTask(15f); // 轻轻向右倾斜 15 度
    }

    public IEnumerator Maneuver_Relative(float targetRoll, float pitchDelta, float targetG)
    {
        // 阶段 1：对齐横滚
        while (Mathf.Abs(Mathf.DeltaAngle(GetCurrentRoll(), targetRoll)) > 2f)
        {
            ApplyRollTask(targetRoll);
            base.Pitch = 0f;
            base.Yaw = 0f;
            Debug.Log("CurrentRoll = " + GetCurrentRoll());
            yield return null;
        }

        // --- 阶段 2 准备：计算目标向量 --- 以当前局部坐标系的右轴（Right）为旋转轴，将 forward 向量旋转 pitchDelta 度 注意：拉杆是绕着飞机的右轴（transform.right）向上旋转
        Vector3 startForward = transform.forward;
        Vector3 rotationAxis = transform.right;

        // 计算目标机头指向（拉杆通常是负 Pitch 输入，但在角度旋转中需对应方向） 这里假设 pitchDelta 为正值，代表需要拉动的总角度
        Quaternion targetRot = Quaternion.AngleAxis(-pitchDelta, rotationAxis);
        Vector3 targetForward = targetRot * startForward;

        float dot = 0f;
        // 阶段 2：执行俯仰 当点乘值从负数或 0 增加到 接近 1 时，说明正在靠近目标 为了防止角度跨度过大导致的判定失败，可以结合“剩余角度”逻辑
        while (dot < 0.99f)
        {
            Vector3 currentForward = transform.forward;
            dot = Vector3.Dot(currentForward, targetForward);

            // 判定剩余角度（用于平滑减速）
            float remainingAngle = Vector3.Angle(currentForward, targetForward);

            // 检查是否已经越过了目标点 如果当前帧与目标向量的夹角开始变大，则说明已经错过最接近点 这里简单处理：只要进入 1度 范围内就认为完成
            if (remainingAngle < 1.0f) break;

            // 输入控制
            base.Pitch = -1.0f;
            base.Roll = 0f;
            base.Yaw = 0f;

            yield return null;
        }

        ResetInputs();
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
        float current = GetCurrentPitch();
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
    public float GetCurrentPitch()
    {
        return transform.forward.y * 90f; // 简单映射到 -90 到 90
    }

    public float GetCurrentRoll()
    {
        Vector3 localUp = transform.up;

        // 2. 将世界向上向量投影到飞机的横截面（由 localRight 和 localUp 定义的平面） 计算 transform.up 与 Vector3.up之间的带符号夹角，围绕机头方向（transform.forward）旋转
        float roll = Vector3.SignedAngle(localUp, Vector3.up, transform.forward);

        // 返回值范围：0 平飞，正值向右翻滚，负值向左翻滚
        return roll;
    }
}
