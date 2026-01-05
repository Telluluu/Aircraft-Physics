using System.Collections;
using UnityEngine;

public class MissileFireControl : MonoBehaviour
{
    [Header("References")]
    public Transform targetTransform; // 锁定的目标

    // public EnvelopeSolver solver; // 之前编写的解算器实例

    [Header("Missile Static Specs")]
    public EnvelopeSolver.MissileParams myMissile;

    private EnvelopeDrawer m_Drawer;

    // [Header("Real-time WEZ Data")]
    public struct LaunchRange
    {
        public float initialMissilePsi;
        public float rMax;
        public float rMin;
        public float nezRMax;
        public float nezRMin;
    }

    private void Start()
    {
        // 初始化导弹参数（根据你的导弹性能填写）
        myMissile.velocity = 600f;      // 初始速度 600m/s (约2马赫)
        myMissile.mass = 150f;
        myMissile.thrust = 500f;
        myMissile.dragCoeff = 0.02f;
        myMissile.maxOverload = 30f;    // 12G
        myMissile.guidanceGain = 3.0f;  // N=4
        m_Drawer = new EnvelopeDrawer();
        StartCoroutine(UpdateEnvelopeRoutine());
    }

    private IEnumerator UpdateEnvelopeRoutine()
    {
        LaunchRange[] launchRanges;
        while (true)
        {
            // 执行耗时的后台计算
            launchRanges = CalculateLaunchEnvelope();

            // 计算完成后，立即推送到 LineRenderer 渲染
            if (launchRanges != null)
            {
                m_Drawer.DrawEnvelope(launchRanges, transform.position, transform.forward, transform.right);
            }

            yield return new WaitForSeconds(0.5f);
        }
    }

    private LaunchRange[] CalculateLaunchEnvelope()
    {
        EnvelopeSolver solver = new EnvelopeSolver();

        EnvelopeSolver.MissileParams missileParams = new EnvelopeSolver.MissileParams();
        missileParams.velocity = myMissile.velocity;
        missileParams.mass = myMissile.mass;
        missileParams.thrust = myMissile.thrust;
        missileParams.dragCoeff = myMissile.dragCoeff;
        missileParams.maxOverload = myMissile.maxOverload;    // 12G
        missileParams.guidanceGain = myMissile.guidanceGain;  // N=4
        LaunchRange[] launchRanges = new LaunchRange[360 / 15];
        for (int q = 0; q < 360; q += 15)
        {
            missileParams.initialMissilePsi = q * Mathf.Deg2Rad;
            var result = CalculateLaunchRange(missileParams, targetTransform, solver);
            launchRanges[q / 15] = result;
        }
        return launchRanges;
    }

    private LaunchRange CalculateLaunchRange(EnvelopeSolver.MissileParams missileParams, Transform targetTransform, EnvelopeSolver solver)
    {
        LaunchRange launchRange = new LaunchRange();

        // --- 第一步：坐标系转换 (World -> Interceptor) ---

        // 1. 获取水平面上的相对位移矢量
        Vector3 worldRelPos = targetTransform.position - transform.position;
        worldRelPos.y = 0;
        float currentDist = worldRelPos.magnitude;

        // 2. 获取世界坐标系下的视线角 (Line of Sight) Atan2(z, x) 得到的是 Unity 世界空间的角度
        float worldLOSAngle = Mathf.Atan2(worldRelPos.z, worldRelPos.x);

        // 3. 获取目标世界速度矢量及其航向角 假设目标有 Rigidbody，如果没有，需自行计算速度矢量
        Vector3 targetVel = targetTransform.GetComponent<Rigidbody>()?.linearVelocity ?? Vector3.zero;
        targetVel.y = 0;
        float worldTargetHeading = Mathf.Atan2(targetVel.z, targetVel.x);

        // 4. 计算初始进入角 (Aspect Angle) 论文定义的 tPsi 是目标航向相对于视线的偏角 如果目标正对你飞来，targetHeading 和 losAngle 相差 180度
        float initialAspectRad = worldTargetHeading - worldLOSAngle;
        Debug.Log("initialAspect: " + initialAspectRad * Mathf.Rad2Deg);

        // 5. 获取载机（导弹发射瞬间）的世界坐标系朝向角 假设当前 Transform 就是导弹或载机，其 Forward 方向即为发射指向
        Vector3 missileForward = transform.forward;
        missileForward.y = 0; // 投影到水平面
        float worldMissileHeading = Mathf.Atan2(missileForward.z, missileForward.x);

        // 6. 计算导弹相对于视线的初始偏角 (Missile Lead Angle) 论文中 mPsi 是导弹速度矢量相对于 LOS 的夹角
        float initialMissilePsiRad = worldMissileHeading - worldLOSAngle;

        // 为了保证角度在 [-PI, PI] 范围内，建议进行弧度归一化
        while (initialMissilePsiRad > Mathf.PI) initialMissilePsiRad -= 2 * Mathf.PI;
        while (initialMissilePsiRad < -Mathf.PI) initialMissilePsiRad += 2 * Mathf.PI;

        Debug.Log($"初始离轴角 (MissilePsi): {initialMissilePsiRad * Mathf.Rad2Deg} 度");

        // --- 传递给仿真参数 ---
        myMissile.initialMissilePsi = initialMissilePsiRad;
        // --- 第二步：准备解算器参数 ---

        EnvelopeSolver.TargetParams tParams = new EnvelopeSolver.TargetParams
        {
            velocityMag = 150.0f,
            initialAspect = initialAspectRad * Mathf.Rad2Deg, // 转换为角度
            maneuverG = 6.0f,      // 假设目标进行6G回避机动
            maneuverType = 3,      // 假设目标在做圆周运动 (论文模型2)
            maneuverPeriod = 5.0f
        };

        // --- 第三步：调用解算器获取动态包线边界 ---
        float time = Time.time;
        launchRange.nezRMax = solver.CalculateMaxRange(myMissile, tParams);
        launchRange.nezRMin = solver.CalculateMinRange(myMissile, tParams, launchRange.nezRMax);

        tParams = new EnvelopeSolver.TargetParams
        {
            velocityMag = 150.0f,
            initialAspect = initialAspectRad * Mathf.Rad2Deg, // 转换为角度
            maneuverG = 1.0f,      // 假设目标进行2G回避机动
            maneuverType = 1,      // 假设目标在做圆周运动 (论文模型2)
            maneuverPeriod = 5.0f
        };

        float rMax0 = solver.CalculateMaxRange(myMissile, tParams);
        float rMin0 = solver.CalculateMinRange(myMissile, tParams, rMax0);

        tParams = new EnvelopeSolver.TargetParams
        {
            velocityMag = 150.0f,
            initialAspect = initialAspectRad * Mathf.Rad2Deg, // 转换为角度
            maneuverG = 2.0f,      // 假设目标进行2G回避机动
            maneuverType = 2,      // 假设目标在做圆周运动 (论文模型2)
            maneuverPeriod = 5.0f
        };

        float rMax1 = solver.CalculateMaxRange(myMissile, tParams);
        float rMin1 = solver.CalculateMinRange(myMissile, tParams, rMax1);

        launchRange.rMax = Mathf.Max(rMax0, launchRange.nezRMax, rMax1);
        launchRange.rMin = Mathf.Min(rMin0, launchRange.nezRMin, rMin1);

        time = Time.time - time;
        // --- 第四步：判定判定 --- canFire = (currentDist >= rMin && currentDist <= rMax);

        // 调试绘制 DrawWEZGizmos(currentDist);
        return launchRange;
    }
}
