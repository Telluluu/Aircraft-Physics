using System.Collections;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;

public class MissileFireControl : MonoBehaviour
{
    [Header("References")]
    public Transform targetTransform; // 锁定的目标

    // public EnvelopeSolver solver; // 之前编写的解算器实例

    [Header("Missile Static Specs")]
    public EnvelopeSolver.MissileParams myMissile;

    public EnvelopeDrawer m_Drawer;
    private EnvelopeSolver m_Solver;

    public LaunchRange[] m_LaunchRanges;
    public bool m_IsLaunchRangesUpdated = false;

    public struct LaunchRange
    {
        public float initialMissilePsi;
        public float rMax;
        public float rMin;
        public float nezRMax;
        public float nezRMin;
    }

#if Debugging

    private Stopwatch swAsync = new Stopwatch();
#endif

    private void Start()
    {
        // 初始化导弹参数（根据你的导弹性能填写）
        myMissile.velocity = 600f;      // 初始速度 600m/s (约2马赫)
        myMissile.mass = 150f;
        myMissile.thrust = 500f;
        myMissile.dragCoeff = 0.02f;
        myMissile.maxOverload = 30f;    // 12G
        myMissile.guidanceGain = 3.0f;  // N=4
        m_Solver = new EnvelopeSolver();
        m_LaunchRanges = new LaunchRange[24];
#if Debugging
        swAsync = new Stopwatch();
#endif
    }

#if Debugging
    public int counter = 0;
#endif

    private float m_UpdateTimer = 0.0f;

    private void Update()
    {
        #region

#if Debugging
        if (counter == 0)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            string report = "导弹动态包线数据 (步进 15°):\n";
            var launchRanges = CalculateLaunchEnvelope(myMissile, targetTransform, m_Solver);

            for (int i = 0; i < launchRanges.Length; i++)
            {
                int angle = i * 15;
                var range = launchRanges[i];

                report += $"[角度 {angle,3}°] RMax: {range.rMax,7:F1}m | RMin: {range.rMin,7:F1}m | NEZ: {range.nezRMax,7:F1}m\n";
            }

            sw.Stop();
            UnityEngine.Debug.Log("总耗时:" + sw.ElapsedMilliseconds);
            UnityEngine.Debug.Log(report);
            counter++;
        }
        else if (counter == 1)
        {
            if (targetTransform != null)
            {
                swAsync.Start();
                TriggerEnvelopeCalculation(myMissile, targetTransform, m_Solver);
                string report = "导弹动态包线数据 (步进 15°):\n";
                for (int i = 0; i < m_LaunchRanges.Length; i++)
                {
                    int angle = i * 15;
                    var range = m_LaunchRanges[i];
                    report += $"[角度 {angle,3}°] RMax: {range.rMax,7:F1}m | RMin: {range.rMin,7:F1}m | NEZ: {range.nezRMax,7:F1}m\n";
                }
                // UpdateLaunchEnvelopeRenderer();
            }
            counter++;
        }
#endif
        #endregion

        m_UpdateTimer += Time.deltaTime;
        if (m_UpdateTimer >= 1.0f)
        {
            if (targetTransform != null)
            {
                TriggerEnvelopeCalculation(myMissile, targetTransform, m_Solver);
                UpdateLaunchEnvelopeRenderer();
            }
            else
            {
                if (m_IsLaunchRangesUpdated != true)
                {
                    m_Drawer.Clear();
                }
            }
            m_UpdateTimer = 0.0f;
        }
    }

    private bool m_IsProcessing = false; // 状态旗标

    public void TriggerEnvelopeCalculation(EnvelopeSolver.MissileParams usedMissileParams, Transform targetTransform, EnvelopeSolver solver)
    {
        // 1. 检查锁：如果上次还没算完，直接退出，不开启新任务
        if (m_IsProcessing) return;
        // 2. 上锁：标记现在开始工作了
        m_IsProcessing = true;

        EnvelopeSolver.MissileParams missileParams = new EnvelopeSolver.MissileParams();
        missileParams.velocity = usedMissileParams.velocity;
        missileParams.mass = usedMissileParams.mass;
        missileParams.thrust = usedMissileParams.thrust;
        missileParams.dragCoeff = usedMissileParams.dragCoeff;
        missileParams.maxOverload = usedMissileParams.maxOverload;    // 12G
        missileParams.guidanceGain = usedMissileParams.guidanceGain;  // N=4

        LaunchRange[] launchRanges = new LaunchRange[360 / 15];

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

        EnvelopeSolver.TargetParams tParams = new EnvelopeSolver.TargetParams
        {
            velocityMag = targetVel.magnitude,
            initialAspect = initialAspectRad * Mathf.Rad2Deg, // 转换为角度
            maneuverG = 9.0f,      // 假设目标进行6G回避机动
            maneuverType = 3,      // 假设目标在做周期为T的正方形运动 (论文模型3)
            maneuverPeriod = 5.0f
        };
        // swAsync.Start();
        // 3. 开启异步任务链
        _ = Task.Run(async () =>
        {
            try
            {
                // 在后台并行计算 24x3 个任务
                var results = await CalculateEnvelopeAsync(missileParams, tParams, solver);

                // 4. 算完后，推送到绘制器
                if (results != null)
                {
#if Debugging
                    swAsync.Stop();
                    UnityEngine.Debug.Log($"总耗时: {swAsync.ElapsedMilliseconds} ms");

                    //string report = "导弹动态包线数据 (步进 15°):\n";
                    //for (int i = 0; i < m_LaunchRanges.Length; i++)
                    //{
                    //    int angle = i * 15;
                    //    var range = m_LaunchRanges[i];
                    //    report += $"[角度 {angle,3}°] RMax: {range.rMax,7:F1}m | RMin: {range.rMin,7:F1}m | NEZ: {range.nezRMax,7:F1}m\n";
                    //}
                    //UnityEngine.Debug.Log(report);
#endif
                    m_LaunchRanges = results;
                    m_IsLaunchRangesUpdated = true;
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"解算故障: {e.Message}");
            }
            finally
            {
                // 5. 解锁：无论成功失败，算完了就允许下一次触发
                m_IsProcessing = false;
            }
        });
    }

    // 更新LaunchEnvelopeRenderer的数据
    private void UpdateLaunchEnvelopeRenderer()
    {
        if (m_IsLaunchRangesUpdated)
        {
            m_IsLaunchRangesUpdated = false;

            if (targetTransform == null)
            {
                m_Drawer.Clear();
                return;
            }

            m_Drawer.PushNewData(m_LaunchRanges);
        }
    }

    // 单线程计算
    private LaunchRange[] CalculateLaunchEnvelope(EnvelopeSolver.MissileParams usedMissileParams, Transform targetTransform, EnvelopeSolver solver)
    {
        EnvelopeSolver.MissileParams missileParams = new EnvelopeSolver.MissileParams();
        missileParams.velocity = usedMissileParams.velocity;
        missileParams.mass = usedMissileParams.mass;
        missileParams.thrust = usedMissileParams.thrust;
        missileParams.dragCoeff = usedMissileParams.dragCoeff;
        missileParams.maxOverload = usedMissileParams.maxOverload;    // 12G
        missileParams.guidanceGain = usedMissileParams.guidanceGain;  // N=4

        LaunchRange[] launchRanges = new LaunchRange[360 / 15];

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

        EnvelopeSolver.TargetParams tParams = new EnvelopeSolver.TargetParams
        {
            velocityMag = 150.0f,
            initialAspect = initialAspectRad * Mathf.Rad2Deg, // 转换为角度
            maneuverG = 9.0f,      // 假设目标进行6G回避机动
            maneuverType = 3,      // 假设目标在做周期为T的正方形运动 (论文模型3)
            maneuverPeriod = 5.0f
        };

        //Stopwatch sw = new Stopwatch();
        //sw.Start();

        for (int q = 0; q < 360; q += 15)
        {
            missileParams.initialMissilePsi = q * Mathf.Deg2Rad;
            LaunchRange launchRange = new LaunchRange();
            launchRange.initialMissilePsi = q;
            // --- 第二步：准备解算器参数 ---

            // 假设目标进行6G回避机动 假设目标在做周期为T的正方形运动 (论文模型3)
            tParams.maneuverG = 9.0f;
            tParams.maneuverType = 3;

            // --- 第三步：调用解算器获取动态包线边界 ---

            launchRange.nezRMax = solver.CalculateMaxRange(missileParams, tParams);
            launchRange.nezRMin = solver.CalculateMinRange(missileParams, tParams, launchRange.nezRMax);

            // 假设目标在做匀速直线运动 (论文模型1)
            tParams.maneuverG = 1.0f;
            tParams.maneuverType = 1;

            float rMax0 = solver.CalculateMaxRange(missileParams, tParams);
            float rMin0 = solver.CalculateMinRange(missileParams, tParams, rMax0);

            // 假设目标进行2G回避机动 假设目标在做圆周运动 (论文模型2)
            tParams.maneuverG = 2.0f;
            tParams.maneuverType = 2;
            float rMax1 = solver.CalculateMaxRange(missileParams, tParams);
            float rMin1 = solver.CalculateMinRange(missileParams, tParams, rMax1);

            launchRange.rMax = Mathf.Max(rMax0, launchRange.nezRMax, rMax1);
            launchRange.rMin = Mathf.Min(rMin0, launchRange.nezRMin, rMin1);

            launchRanges[q / 15] = launchRange;
        }
        //sw.Stop();
        //UnityEngine.Debug.Log($"总耗时: {sw.ElapsedMilliseconds} ms");
        return launchRanges;
    }

    // 多线程计算导弹包线，外24层，内3层
    private async Task<LaunchRange[]> CalculateEnvelopeAsync(
    EnvelopeSolver.MissileParams mP,
    EnvelopeSolver.TargetParams tP,
    EnvelopeSolver solver)
    {
        // 准备 24 个角度的任务列表
        Task<LaunchRange>[] angleTasks = new Task<LaunchRange>[24];

        for (int i = 0; i < 24; i++)
        {
            int angleIdx = i; // 局部变量捕获
            float currentQ = angleIdx * 15.0f;

            // 为每个角度开启一个最高层级的异步任务
            angleTasks[i] = Task.Run(async () =>
            //angleTasks[i] = Task.Run(() =>
            {
                var mp = mP;
                mp.initialMissilePsi = currentQ * Mathf.Deg2Rad;

                // --- 核心优化：针对三种机动模型，开启三个并发子任务 --- 每一个子任务内部会顺序执行 Max 和 Min 的二分查找
                Task<(float max, float min)> nezTask = Task.Run(() => CalculateRangeSync(mp, tP, solver, 3, 9.0f));
                Task<(float max, float min)> linTask = Task.Run(() => CalculateRangeSync(mp, tP, solver, 1, 1.0f));
                Task<(float max, float min)> cirTask = Task.Run(() => CalculateRangeSync(mp, tP, solver, 2, 2.0f));

                // 同时等待这 3 个机动模型算完（这 3 个可能跑在不同的 CPU 核心上）
                await Task.WhenAll(nezTask, linTask, cirTask);

                // 汇总结果
                var nez = nezTask.Result;
                var lin = linTask.Result;
                var cir = cirTask.Result;

                //(float max, float min) nez = CalculateRangeSync(mp, tP, solver, 3, 9.0f);
                //(float max, float min) lin = CalculateRangeSync(mp, tP, solver, 1, 1.0f);
                //(float max, float min) cir = CalculateRangeSync(mp, tP, solver, 2, 2.0f);
                return new LaunchRange
                {
                    initialMissilePsi = currentQ,
                    nezRMax = nez.max,
                    nezRMin = nez.min,
                    // RMax 取三种模型中最乐观的（只要有一种情况能打着就算 RMax）
                    rMax = System.MathF.Max(nez.max, System.MathF.Max(lin.max, cir.max)),
                    // RMin 取三种模型中最保守的（只要有一种情况打不着就还没进 RMin）
                    rMin = System.MathF.Min(nez.min, System.MathF.Min(lin.min, cir.min))
                };
            });
        }

        // 最终汇总 24 个方向的结果
        return await Task.WhenAll(angleTasks);
    }

    // 这是一个纯数学包装函数，用来处理单个模型的串行依赖（Min 依赖 Max） 这是一个纯同步的数学逻辑块
    private (float max, float min) CalculateRangeSync(
        EnvelopeSolver.MissileParams mp,
        EnvelopeSolver.TargetParams tpBase,
        EnvelopeSolver solver,
        int type, float g)
    {
        // 这里没有任何 Task 或 await，只有纯粹的 CPU 运算
        var tp = tpBase;
        tp.maneuverType = type;
        tp.maneuverG = g;

        float max = solver.CalculateMaxRange(mp, tp);
        float min = solver.CalculateMinRange(mp, tp, max);
        return (max, min);
    }
}
