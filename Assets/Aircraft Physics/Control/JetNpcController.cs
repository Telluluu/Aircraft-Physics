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

    private Vector3 debugEscapeHeading; // 锁定的测试目标方向
    private bool isTestingTurn = false;

    protected override void Update()
    {
        // 1. 基础推力：测试时保持动力
        base.thrustPercent = 1.0f;

        // 2. 触发测试：按下 C 键时“锁定”逃逸方向
        if (Input.GetKeyDown(KeyCode.C))
        {
            isTestingTurn = true;

            // 计算背对 target 的水平方向
            Vector3 dirAway = (transform.position - transform.forward * 10000f);
            dirAway.y = 0;
            debugEscapeHeading = dirAway.normalized;

            Debug.Log("开始转冷测试，目标航向: " + debugEscapeHeading);
        }

        // 3. 执行逻辑
        if (isTestingTurn)
        {
            // 实时画出目标航向（绿色长线），方便观察飞机是否往这个方向靠拢
            Debug.DrawRay(transform.position, debugEscapeHeading * 100f, Color.green);

            // 调用你的通用指向器
            ApplyFlightDirector(debugEscapeHeading);

            // 4. 判定完成：当机头对准目标航向（点积接近 1）时停止测试
            float dot = Vector3.Dot(transform.forward, debugEscapeHeading);
            if (dot > 0.99f)
            {
                Debug.Log("测试完成：已对准目标航向");
                isTestingTurn = false;
                // 重置输入，防止过冲
                base.Roll = 0;
                base.Pitch = 0;
                base.Yaw = 0;
            }
        }
    }

    public void ApplyFlightDirector(Vector3 worldTargetDir)
    {
        Vector3 localDir = transform.InverseTransformDirection(worldTargetDir.normalized);

        // 1. 计算离轴角（目标与机头正前方的夹角） 0度表示正前方，180度表示正后方
        float angleToTarget = Vector3.Angle(Vector3.forward, localDir);

        float targetRoll = 0f;
        float targetPitch = 0f;

        // 2. 情况 A：目标在前方窄圆锥内（例如 5 度以内） 此时不需要大幅度翻滚，直接微调俯仰和偏航即可
        if (angleToTarget < 5f)
        {
            targetRoll = 0f;
            targetPitch = localDir.y * 0.5f; // 简单的比例引导
            base.Yaw = localDir.x * 0.5f;
        }
        // 3. 情况 B：目标在后方（转冷逻辑的核心）
        else if (localDir.z < 0)
        {
            // 计算目标在机身左右哪一侧
            float side = Mathf.Sign(localDir.x);
            if (localDir.x == 0) side = 1f; // 默认向右转

            // 强制目标翻滚角为 90 度（侧身转向） 这样可以避免 Atan2 在 180 度附近的跳变
            targetRoll = 90f * side;

            // 判定：只有当飞机坡度接近 90 度时，才全力拉杆 transform.right.y 接近 0 意味着翅膀垂直于地面（坡度90度） 或者简单判断 rollError
            float currentRollError = targetRoll - GetCurrentRoll(); // 需要你自己实现 GetCurrentRoll
            if (Mathf.Abs(currentRollError) < 20f)
            {
                targetPitch = 1.0f; // 大过载转弯
            }
        }
        // 4. 情况 C：目标在侧方（通用指向）
        else
        {
            targetRoll = Mathf.Atan2(localDir.x, localDir.y) * Mathf.Rad2Deg;
            float rollWeight = Mathf.Clamp01(1.0f - (Mathf.Abs(targetRoll) / 90f));
            targetPitch = Mathf.Atan2(localDir.y, localDir.z) * Mathf.Rad2Deg * 0.05f * rollWeight;
        }

        // 5. 应用控制量 使用简单的比例控制（P控制器），加上对 Roll 的特殊处理
        base.Roll = Mathf.Clamp(targetRoll / 180f, -1f, 1f);
        base.Pitch = Mathf.Clamp(targetPitch, -1f, 1f);
    }

    public void StartTurnCold(Transform threat)
    {
        if (threat == null) return;
        StopAllCoroutines(); // 防止多个指令冲突
        StartCoroutine(TurnColdRoutine(threat.position));
    }

    private IEnumerator TurnColdRoutine(Vector3 threatPos)
    {
        // 1. 初始化：锁定脱离航向
        Vector3 escapeHeading = (transform.position - threatPos);
        escapeHeading.y = 0;
        escapeHeading.Normalize();

        // 配置基础动力
        base.thrustPercent = 1.0f;
        base.Flap = 0f;

        // 2. 第一阶段：快速横滚 (直到坡度接近 90 度或目标投影) 此时不拉杆，只翻滚，减少能量损失并对准转向矢量
        while (true)
        {
            Vector3 localDir = transform.InverseTransformDirection(escapeHeading);
            float rollError = Mathf.Atan2(localDir.x, localDir.y) * Mathf.Rad2Deg;

            base.Roll = Mathf.Clamp(rollError * 0.1f, -1f, 1f);
            base.Pitch = 0.1f; // 维持微小仰角

            // 当翻滚误差小于 20 度时，进入下一阶段
            if (Mathf.Abs(rollError) < 20f) break;
            yield return null;
        }

        // 3. 第二阶段：大过载拉杆 (转弯) 此时保持横滚修正，同时全速拉杆
        while (Vector3.Dot(transform.forward, escapeHeading) < 0.98f)
        {
            Vector3 localDir = transform.InverseTransformDirection(escapeHeading);
            float rollError = Mathf.Atan2(localDir.x, localDir.y) * Mathf.Rad2Deg;

            base.Roll = Mathf.Clamp(rollError * 0.1f, -1f, 1f);

            // 目标在后方或离轴角大时，全力拉杆
            base.Pitch = 1.0f;

            yield return null;
        }

        // 4. 第三阶段：改平回正 机头已对准，撤销拉杆，收回横滚
        float stabilizeTime = 1.0f;
        while (stabilizeTime > 0)
        {
            ApplyFlightDirector(escapeHeading); // 使用你的通用指向器做最后微调
            stabilizeTime -= Time.deltaTime;
            yield return null;
        }

        // 动作完成，清理控制输入
        base.Roll = 0;
        base.Pitch = 0;
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
