using UnityEngine;
using System;

public class EnvelopeSolver
{
    private const float G = 9.81f;
    private const float TIME_STEP = 0.05f;
    private const float MAX_FLIGHT_TIME = 180f;
    private const float HIT_THRESHOLD = 100.0f;
    private const float BISECTION_TOLERANCE = 10.0f;

    [Serializable]
    public struct MissileParams
    {
        public float velocity;      // Vm: 初始速度
        public float mass;
        public float thrust;        // T
        public float dragCoeff;     // 用于计算 D
        public float maxOverload;   // nzMax
        public float guidanceGain;  // N
        public float initialMissilePsi; // 导弹的发射离轴角（载机朝向与视线方向的夹角)，弧度
    }

    [Serializable]
    public struct TargetParams
    {
        public float velocityMag;   // Vt
        public float initialAspect; // 初始时刻目标航向与视线的夹角 (Psi_T0)
        public float maneuverG;     // gT
        public float maneuverPeriod;// T (用于正方形机动)
        public int maneuverType;    // 1:直线, 2:圆周, 3:正方形
    }

    private struct State
    {
        public float x, z, v, psi, time;
    }

    // 计算最大发射距离 Rmax
    public float CalculateMaxRange(MissileParams missile, TargetParams target)
    {
        float minRange = 500f;
        float maxRange = 1000000f;
        float finalRange = 0f;

        // 论文 2.2 二分法循环
        for (int i = 0; i < 30; i++)
        {
            float checkRange = (minRange + maxRange) / 2f;

            if (SimulateTrajectory(missile, target, checkRange))
            {
                finalRange = checkRange;
                minRange = checkRange; // 命中，尝试更远
            }
            else
            {
                maxRange = checkRange; // 未命中，缩短距离
            }

            if ((maxRange - minRange) < BISECTION_TOLERANCE) break;
        }
        return finalRange;
    }

    public float CalculateMinRange(MissileParams missile, TargetParams target, float rMaxResult)
    {
        float minRange = 100f;   // 理论物理最小限度
        float maxRange = rMaxResult; // 上限为已解算出的最大发射距离
        float finalRange = -1.0f;

        for (int i = 0; i < 30; i++)
        {
            float checkRange = (minRange + maxRange) / 2f;

            // 调用相同的龙格-库塔函数计算轨迹
            if (SimulateTrajectory(missile, target, checkRange))
            {
                // 如果命中，说明距离可能还可以更近
                finalRange = checkRange;
                maxRange = checkRange; // 命中，尝试更小范围
            }
            else
            {
                // 未命中，说明太近了（导弹转弯半径不够或无法完成导引）
                minRange = checkRange; // 未命中，尝试拉远
            }

            if ((maxRange - minRange) < BISECTION_TOLERANCE) break;
        }
        return finalRange;
    }

    private bool SimulateTrajectory(MissileParams mParams, TargetParams tParams, float R0)
    {
        // 1. 初始化状态 (论文 1.2 & 2.3: 初始 q=0, X轴指向目标)
        State mState = new State
        {
            x = 0,
            z = 0,
            v = mParams.velocity,
            psi = mParams.initialMissilePsi, // 导弹发射离轴角
            time = 0
        };

        float tx = R0;
        float tz = 0;
        float tPsi = tParams.initialAspect * Mathf.Deg2Rad; // 目标初始航向
        float minRT = float.MaxValue;
        float currentQ = 0;
        float lastQ = 0;
        while (mState.time < MAX_FLIGHT_TIME)
        {
            // --- A. 更新目标运动 (论文 1.4 公式 3-6) ---
            float dPsiT = 0;
            switch (tParams.maneuverType)
            {
                case 1: dPsiT = 0; break; // 直线
                case 2: dPsiT = (G * tParams.maneuverG) / tParams.velocityMag; break; // 圆周
                case 3: // 正方形
                    float sinTerm = Mathf.Sin(mState.time - tParams.maneuverPeriod);
                    dPsiT = (Mathf.Abs(sinTerm) > 0.001f) ?
                            -(G * tParams.maneuverG) / (tParams.velocityMag * sinTerm) : 0;
                    break;
            }
            tPsi += dPsiT * TIME_STEP;
            tx += tParams.velocityMag * Mathf.Cos(tPsi) * TIME_STEP; // 公式(6)
            tz -= tParams.velocityMag * Mathf.Sin(tPsi) * TIME_STEP; // 公式(6) 注意负号

            // --- B. 几何关系与命中判定 (论文 1.2 公式 1) ---
            float dx = tx - mState.x;
            float dz = tz - mState.z;
            float RT = Mathf.Sqrt(dx * dx + dz * dz);

            if (RT < minRT)
                minRT = RT;

            if (RT < HIT_THRESHOLD)
            {
                // ("R0 = " + R0 + "时命中! minRT = " + minRT);
                return true; // 命中
            }

            // 采样法计算视线角变化率
            // 1. 计算当前视线角 (弧度)
            currentQ = Mathf.Atan2(-dz, dx);

            // 2. 计算 q_dot
            float q_dot = 0;
            if (mState.time > 0) // 第一次特殊化处理
            {
                // DeltaAngle 返回的是角度差 (-180 到 180)
                float diffDeg = Mathf.DeltaAngle(lastQ * Mathf.Rad2Deg, currentQ * Mathf.Rad2Deg);
                q_dot = (diffDeg * Mathf.Deg2Rad) / TIME_STEP;
            }
            lastQ = currentQ; // 更新历史记录

            // 以g为单位的法向过载nz
            float nz = (mParams.guidanceGain * mState.v * q_dot) / G;
            // 不能超过导弹舵面提供的最大过载
            nz = Mathf.Clamp(nz, -mParams.maxOverload, mParams.maxOverload);
            // --- D. RK4 积分更新导弹 (论文 1.3 & 2.1 公式 2) ---
            mState = RungeKuttaStep(mState, mParams, nz);

            // 终止条件
        }
        return false;
    }

    private State RungeKuttaStep(State y, MissileParams mp, float nz)
    {
        float h = TIME_STEP;
        State d1 = GetDerivatives(y, mp, nz);
        State d2 = GetDerivatives(AddState(y, d1, h * 0.5f), mp, nz);
        State d3 = GetDerivatives(AddState(y, d2, h * 0.5f), mp, nz);
        State d4 = GetDerivatives(AddState(y, d3, h), mp, nz);

        return new State
        {
            x = y.x + (h / 6f) * (d1.x + 2 * d2.x + 2 * d3.x + d4.x),
            z = y.z + (h / 6f) * (d1.z + 2 * d2.z + 2 * d3.z + d4.z),
            v = y.v + (h / 6f) * (d1.v + 2 * d2.v + 2 * d3.v + d4.v),
            psi = y.psi + (h / 6f) * (d1.psi + 2 * d2.psi + 2 * d3.psi + d4.psi),
            time = y.time + h
        };
    }

    private State GetDerivatives(State s, MissileParams mp, float nz)
    {
        State d = new State();
        // 1. 基础阻力 (零升阻力)
        float dragZero = mp.dragCoeff * s.v * s.v;

        // 2. 诱导阻力 (Induced Drag) 核心逻辑：nz 越大，阻力越大；速度越低，维持相同 nz 需要的攻角越大，阻力也越大 这里的 10.0f 是一个缩放因子，用于匹配你的质量单位
        float inducedDrag = 0;
        //if (s.v > 10f)
        //{
        //    // 推荐系数取值见下文
        //    float k2 = 0.5f;
        //    inducedDrag = k2 * (nz * nz * G * G * mp.mass) / (s.v * s.v);
        //}

        float totalDrag = dragZero + inducedDrag;
        d.v = (mp.thrust - totalDrag) / mp.mass; // Vm_dot = (T-D)/m
        d.psi = (s.v > 0.1f) ? (G * nz) / s.v : 0; // PsiM_dot = g*nz/Vm
        d.x = s.v * Mathf.Cos(s.psi); // XM_dot
        d.z = -s.v * Mathf.Sin(s.psi); // ZM_dot
        return d;
    }

    private State AddState(State original, State delta, float scale)
    {
        return new State
        {
            x = original.x + delta.x * scale,
            z = original.z + delta.z * scale,
            v = original.v + delta.v * scale,
            psi = original.psi + delta.psi * scale
        };
    }
}
