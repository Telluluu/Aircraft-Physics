using System.Collections;
using UnityEngine;

public class MissileController : AirplaneController
{
    [Header("Missile Specs")]
    public float motorThrust = 27000f;   // 推力

    public float burnTime = 8f;          // 动力持续时间
    public float proximityRange = 10f;   // 近炸引信范围
    public float maxG = 30f;             // 最大过载限制

    [Header("Guidance Settings")]
    public Transform target;

    public float navigationConstant = 4.0f; // 比例制导常数 (N)，通常取 3-5
    public float activationDelay = 0.5f;    // 发射后安全延迟（防炸自己）
    public DopplerRadar missileRadar;
    public DopplerRadar aircraftRadar;

    private bool _isLaunched = false;
    private float _launchTime;
    private Vector3 _lastTargetPosition;
    private Vector3 _lastMissilePosition;

    // 两步制导

    // 第一步：载机雷达数据链引导

    // 第二步：导弹雷达引导
    public int m_step = 0;

    protected override void Start()
    {
        base.Start();
        // 初始状态下物理引擎和推力关闭
        base.thrustPercent = 1;
        missileRadar.isWoking = false;
        m_step = 0;
    }

    protected override void Update()
    {
        if (Input.GetKey(KeyCode.UpArrow))
        {
            base.Pitch = -0.1f;
        }
        else if (Input.GetKey(KeyCode.DownArrow))
        {
            base.Pitch = 0.1f;
        }
        else
        {
            base.Pitch = 0;
        }

        if (Input.GetKey(KeyCode.LeftArrow))
        {
            base.Yaw = -0.1f;
        }
        else if (Input.GetKey(KeyCode.RightArrow))
        {
            base.Yaw = 0.1f;
        }
        else
        {
            base.Yaw = 0;
        }

        if (m_step == 0)
        {
        }
        else if (m_step == 1)
        {
        }

        // 1. 执行制导律计算
        Vector3 guidanceCommand = CalculateProportionalNavigation();

        // 2. 将世界空间的过载指令转换为本地控制杆量
        ApplyGuidanceToSurfaces(guidanceCommand);

        //if (!_isLaunched) return;

        //// 动力衰减逻辑
        //if (Time.time - _launchTime > burnTime)
        //{
        //    base.thrustPercent = 0;
        //}

        //// 近炸引信检测
        //if (target != null && Vector3.Distance(transform.position, target.position) < proximityRange)
        //{
        //    Explode();
        //}
    }

    protected override void FixedUpdate()
    {
        if (!_isLaunched || target == null) return;

        base.FixedUpdate();
    }

    public void Launch(Transform targetTransform)
    {
        target = targetTransform;
        _isLaunched = true;
        _launchTime = Time.time;
        _lastTargetPosition = target.position;
        _lastMissilePosition = transform.position;

        // 开启物理
        rb.isKinematic = false;
        base.thrustPercent = 1.0f;
    }

    public void RaddarStart()
    {
        missileRadar.isWoking = true;
        m_step = 1;
        missileRadar.ScanTargets();
        bool isFindTarget = false;
        if (target != null)
            isFindTarget = missileRadar.CheckTarget(target);
        else target = missileRadar.lockedTargets[0].transform;
    }

    private Vector3 CalculateProportionalNavigation()
    {
        if (target == null) return Vector3.zero;

        // 相对位移与相对速度
        Vector3 relativePos = target.position - transform.position;
        Vector3 relativeVel = (target.position - _lastTargetPosition) / Time.fixedDeltaTime - rb.linearVelocity;

        // 计算视线角速度 (Line of Sight Rate) Omega = (r x v) / (r . r)
        Vector3 losRate = Vector3.Cross(relativePos, relativeVel) / relativePos.sqrMagnitude;

        // 计算向心加速度指令: a = N * V_close * losRate 这里简化处理：直接使用比例常数乘以角速度
        Vector3 accelerationCommand = navigationConstant * rb.linearVelocity.magnitude * losRate;

        _lastTargetPosition = target.position;
        return accelerationCommand;
    }

    private void ApplyGuidanceToSurfaces(Vector3 accelCmd)
    {
        // 将加速度指令转换到本地坐标系
        Vector3 localAccel = transform.InverseTransformDirection(accelCmd);

        // 限制最大过载 (G-Limiter)
        float maxAccel = maxG * 9.81f;
        localAccel = Vector3.ClampMagnitude(localAccel, maxAccel);

        // 映射到 Pitch 和 Yaw 注意：导弹通常不需要 Roll 控制，或者使用自动增稳保持 Roll 为 0 这里的敏感度需根据导弹的动压（速度）进行类似于
        // JetNpcController 的衰减
        float speedRatio = Mathf.Max(rb.linearVelocity.magnitude / 300f, 1.0f);
        float attenuation = 1.0f / (speedRatio * speedRatio);

        base.Pitch = Mathf.Clamp(localAccel.y / maxAccel, -1f, 1f) * attenuation;
        base.Yaw = Mathf.Clamp(localAccel.x / maxAccel, -1f, 1f) * attenuation;

        // 自动增稳横滚
        base.Roll = -transform.InverseTransformDirection(rb.angularVelocity).z * 0.1f;
    }

    private void Explode()
    {
        // 爆炸逻辑
        Debug.Log("Splash!");
        Destroy(gameObject);
    }
}
