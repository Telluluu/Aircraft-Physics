using System.Collections.Generic;
using UnityEngine;

public class DopplerRadar : MonoBehaviour
{
    [Header("雷达设置")]
    public float maxRange = 2000f;       // 探测距离

    public float scanAngle = 60f;        // 扫描总夹角 (圆锥底角)

    public LayerMask targetMask;                         // 目标层级
    public float minDopplerVelocity = 5.0f;              // 最小多普勒过滤门限 (m/s)

    [Header("当前目标")]
    public List<Transform> lockedTargets = new List<Transform>();

    private Rigidbody rb;

    public bool isWorking = true;

    public IFF iff;

    private void Start()
    {
        rb = GetComponentInParent<Rigidbody>();
    }

    private void Update()
    {
        UpdateTargets();
    }

    public bool CheckTarget(Transform target)
    {
        return lockedTargets.Contains(target);
    }

    public void UpdateTargets()
    {
        if (!isWorking) return;

        List<Transform> currentScanned = ScanTargets();

        // 1. 移除丢失的目标 (List 内部会遍历处理) 这里的 lambda 表达式会检查旧目标是否还在新扫描结果中，或者是否已被销毁(null)
        lockedTargets.RemoveAll(t => t == null || !currentScanned.Contains(t));

        // 2. 添加新增的目标
        for (int i = 0; i < currentScanned.Count; i++)
        {
            Transform t = currentScanned[i];
            if (!lockedTargets.Contains(t))
            {
                lockedTargets.Add(t);
            }
        }
    }

    private List<Transform> ScanTargets()
    {
        List<Transform> newLockedTargets = new List<Transform>();
        Collider[] potentialTargets = Physics.OverlapSphere(transform.position, maxRange, targetMask);

        Vector3 VM = rb != null ? rb.linearVelocity : Vector3.zero;

        foreach (var hit in potentialTargets)
        {
            // 敌我识别
            var targetIFF = hit.transform.GetComponent<IFF>();
            if (targetIFF != null)
            {
                if (targetIFF.affilation != iff.enemyAffilation || targetIFF.objectType == IFF.IFFObjectType.Missile)
                    continue;
            }

            Vector3 relativePos = hit.transform.position - transform.position;
            float distance = relativePos.magnitude;
            Vector3 directionToTarget = relativePos / distance;

            // 1. 几何过滤
            float angleToTarget = Vector3.Angle(transform.forward, directionToTarget);
            if (angleToTarget > scanAngle / 2f) continue;

            Rigidbody targetRb = hit.GetComponentInParent<Rigidbody>();
            Vector3 VT = (targetRb != null) ? targetRb.linearVelocity : Vector3.zero;

            // 2. 径向速度计算
            float vT_radial = Vector3.Dot(VT, directionToTarget); // 目标在视线上的绝对径向速度
            float vM_radial = Vector3.Dot(VM, directionToTarget); // 载机在视线上的绝对径向速度
            float relativeRadialVelocity = vT_radial - vM_radial; // 两者相对径向速度（接近速度）

            // 3. 判定是否为“下视”状态 (视线向下倾斜)
            bool isLookDown = directionToTarget.y < -0.5f;

            // 4. 盲区判定逻辑
            // A: 相对速度盲区 (同速同向，Doppler Notch)
            bool isDopplerNotch = Mathf.Abs(relativeRadialVelocity) < minDopplerVelocity;

            // B: 地杂波盲区 (3-9机动，目标相对地面径向速度接近0，Clutter Notch) 只有在下视且目标相对地面的径向速度极小时发生
            bool isClutterNotch = (Mathf.Abs(vT_radial) < minDopplerVelocity);

            // 如果掉进任何一个盲区，则无法锁定
            if (isDopplerNotch || isClutterNotch)
            {
                // 可选：在此处 Debug 显示脱锁原因
                //Debug.Log($"Radar: {name}, Target: {hit.name},Doppler Notch: {isDopplerNotch}, Clutter Notch: {isClutterNotch}, VT:{VT}, Relative Radial Velocity: {relativeRadialVelocity}");
                Debug.DrawLine(transform.position, hit.transform.position, Color.red);
                continue;
            }

            // 5. 锁定成功
            newLockedTargets.Add(hit.transform);
            Debug.DrawLine(transform.position, hit.transform.position, Color.green);
            //Debug.Log($"Radar: {name}, Target: {hit.name},Doppler Notch: {isDopplerNotch}, Clutter Notch: {isClutterNotch}, Relative Radial Velocity: {relativeRadialVelocity}");
        }
        return newLockedTargets;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        // 简单的圆锥线框绘制
        Gizmos.DrawRay(transform.position, Quaternion.Euler(0, scanAngle / 2f, 0) * transform.forward * maxRange);
        Gizmos.DrawRay(transform.position, Quaternion.Euler(0, -scanAngle / 2f, 0) * transform.forward * maxRange);
        Gizmos.DrawRay(transform.position, Quaternion.Euler(scanAngle / 2f, 0, 0) * transform.forward * maxRange);
        Gizmos.DrawRay(transform.position, Quaternion.Euler(-scanAngle / 2f, 0, 0) * transform.forward * maxRange);
    }
}
