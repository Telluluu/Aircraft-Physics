using UnityEngine;

public class MissileController : MonoBehaviour
{
    [Header("Components")]
    public Rigidbody rb;

    public DopplerRadar missileRadar;
    public DopplerRadar aircraftRadar;
    public Transform target;

    [Header("Simple Logic")]
    public float speed = 1020f;       // 强制 3马赫 (1020m/s)

    public float turnSpeed = 200f;    // 转向灵敏度（够高才能瞬间指向）

    private bool _isLaunched = false;
    private int _step = 0;

    public void Launch(Vector3 velocity, DopplerRadar radar, Transform targetTransform, IFF.IFFTeamType affiliation, IFF.IFFTeamType targetAffiliation)
    {
        // 1. 彻底断开父物体，防止载机位移干扰坐标更新
        transform.SetParent(null);

        this.target = targetTransform;
        this.aircraftRadar = radar;

        // 2. 初始化物理状态
        rb.isKinematic = true;
        rb.useGravity = false;

        _isLaunched = true;
        _step = 0;
    }

    private void FixedUpdate()
    {
        if (!_isLaunched) return;

        // --- 第一步：判断引导状态 ---
        bool hasTarget = false;
        if (target != null)
        {
            // 距离判定：是否进入导弹主动雷达范围
            if (_step == 0 && Vector3.Distance(transform.position, target.position) < missileRadar.maxRange)
            {
                _step = 1;
                if (missileRadar != null) missileRadar.isWorking = true;
            }

            // 锁定判定：由外部雷达脚本决定
            if (_step == 0)
                hasTarget = aircraftRadar != null && aircraftRadar.CheckTarget(target);
            else
                hasTarget = missileRadar != null && missileRadar.CheckTarget(target);
        }

        // --- 第二步：决定朝向 ---
        if (hasTarget && target != null)
        {
            // 计算简单的提前量预测
            float dist = Vector3.Distance(transform.position, target.position);
            float tti = dist / speed;
            Rigidbody targetRb = target.GetComponentInParent<Rigidbody>();
            Vector3 targetVel = targetRb ? targetRb.linearVelocity : Vector3.zero;

            Vector3 interceptPoint = target.position + targetVel * tti;
            Vector3 targetDir = (interceptPoint - transform.position).normalized;

            // 强制转向预测点
            if (targetDir != Vector3.zero)
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    Quaternion.LookRotation(targetDir),
                    turnSpeed * Time.fixedDeltaTime
                );
            }
        }
        // else: 没锁定时，导弹保持当前 transform.forward 不动

        // --- 第三步：核心位移（绝对执行，绝不分叉） --- 直接修改 rb.position 避开一切物理引擎的干扰逻辑
        Vector3 movement = transform.forward * speed * Time.fixedDeltaTime;
        rb.position += movement;

        // 同步 linearVelocity 供雷达 CheckTarget 计算相对速度 rb.linearVelocity = transform.forward * speed;
    }
}
