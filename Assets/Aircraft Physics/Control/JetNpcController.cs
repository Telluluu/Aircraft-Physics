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

    public static bool isC = false;

    protected override void Update()
    {
        base.thrustPercent = 1.0f;
        if (Input.GetKey(KeyCode.C))
        {
            if (isC == false)
            {
                isC = true;
                Vector3 debugTarTran = transform.position + transform.forward * 1000f;
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.position = debugTarTran;
                target = go.transform;
            }

            isCombatMode = true;
            AbortAndTurnCold(target);
        }
    }

    public void ApplyFlightDirector(Vector3 targetDir)
    {
        Debug.Log("ApplyFlightDirector:targetDir = " + targetDir.ToString());
        Vector3 localDir = transform.InverseTransformDirection(targetDir.normalized);

        // 1. 计算目标在本地 X-Y 平面上的角度（即我需要往哪个方向翻滚才能“抬头”对准它） Atan2(x, y) 算出来的是：目标相对于飞机正上方的偏移角
        float rollError = Mathf.Atan2(localDir.x, localDir.y) * Mathf.Rad2Deg;

        // 2. 滚转控制 (Roll) 如果目标不在前方窄圆锥内，我们优先翻转机身，让“机顶”对准目标
        float currentRoll = GetCurrentRoll();

        // 我们计算一个目标 Roll，目标是让 rollError 归零 如果目标在后方，localDir.z < 0，rollError 依然有效
        base.Roll = Mathf.Clamp(rollError * 0.05f, -1f, 1f);

        // 3. 俯仰控制 (Pitch) 只有当机顶大致对准了目标（rollError 较小），拉杆才有意义 计算目标与飞机前轴的夹角 (离轴角)
        float angleToTarget = Vector3.Angle(Vector3.forward, localDir);

        // 升力补偿
        float cosRoll = Mathf.Cos(currentRoll * Mathf.Deg2Rad);
        float liftComp = 1.0f / Mathf.Max(Mathf.Abs(cosRoll), 0.2f);

        // 如果目标在后方（angle > 90），全力拉杆
        if (localDir.z < 0)
        {
            base.Pitch = 1.0f * liftComp;
        }
        else
        {
            // 如果在前方，根据角度差比例拉杆
            float pitchError = Mathf.Atan2(localDir.y, localDir.z) * Mathf.Rad2Deg;
            base.Pitch = Mathf.Clamp(pitchError * 0.1f * liftComp, -1f, 1f);
        }

        // 4. 偏航控制 (Yaw)
        base.Yaw = Mathf.Clamp(localDir.x * 0.2f, -0.5f, 0.5f);
    }

    public void AbortAndTurnCold(Transform threat)
    {
        if (threat == null) return;

        // 1. 计算背对威胁的目标向量 注意：只在水平面上计算转弯，避免机头直接插向地面（除非你想结合俯冲）
        Vector3 dirAway = (transform.position - threat.position);
        dirAway.y = 0; // 暂时保持水平脱离
        Vector3 targetDir = dirAway.normalized;

        // 2. 调用第一层：指引器 它会自动处理：发现目标在后方 -> 满舵 Roll -> 大过载 Pitch 拉杆
        ApplyFlightDirector(targetDir);

        // 3. 动力管理：逃命必须满油门
        base.thrustPercent = 1.0f;

        // 4. 配置管理：收起襟翼，关闭减速板以减小阻力
        base.Flap = 0f;
        base.brakesTorque = 0f;
    }

    public float GetCurrentRoll()
    {
        // 1. 从刚体旋转中获取欧拉角 (0 到 360)
        Vector3 angles = base.rb.rotation.eulerAngles;

        // 2. 提取 Z 轴（横滚轴）
        float roll = angles.z;

        // 3. 将 0~360 映射到 -180~180 这样：0 是水平，90 是左侧飞，-90 是右侧飞，180/-180 是倒飞
        if (roll > 180)
        {
            roll -= 360;
        }

        return roll;
    }
}
