using System.Collections.Generic;
using UnityEngine;

public class DopplerRadar : MonoBehaviour
{
    [Header("可视化")]
    public RadarLockonUI radarLockonUI;

    [Header("雷达设置")]
    public float maxRange = 2000f;       // 探测距离

    public float scanAngle = 60f;        // 扫描总夹角 (圆锥底角)

    public LayerMask targetMask;                         // 目标层级
    public float minDopplerVelocity = 5.0f;              // 最小多普勒过滤门限 (m/s)

    [Header("当前目标")]
    public List<Transform> lockedTargets = new List<Transform>();

    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponentInParent<Rigidbody>();
    }

    private void Update()
    {
        ScanTargets();
        if (radarLockonUI != null)
            radarLockonUI.SetTargets(lockedTargets);
    }

    private void ScanTargets()
    {
        lockedTargets.Clear();
        // 粗筛，获取球体内的所有潜在目标
        Collider[] potentialTargets = Physics.OverlapSphere(transform.position, maxRange, targetMask);

        Vector3 VM = rb != null ? rb.linearVelocity : Vector3.zero;

        foreach (var hit in potentialTargets)
        {
            Vector3 relativePos = hit.transform.position - transform.position;
            float distance = relativePos.magnitude;
            Vector3 directionToTarget = relativePos / distance;

            // 几何过滤：判定是否在雷达范围内
            float angleToTarget = Vector3.Angle(transform.forward, directionToTarget);
            if (angleToTarget > scanAngle / 2f) continue;

            Rigidbody targetRb = hit.GetComponent<Rigidbody>();
            Vector3 VT = (targetRb != null) ? targetRb.linearVelocity : Vector3.zero;

            // 多普勒过滤 径向速度差计算
            float vT_radial = Vector3.Dot(VT, directionToTarget);
            float vM_radial = Vector3.Dot(VM, directionToTarget);
            float relativeRadialVelocity = vT_radial - vM_radial;

            if (Mathf.Abs(relativeRadialVelocity) > minDopplerVelocity)
            {
                lockedTargets.Add(hit.transform);
                Debug.DrawLine(transform.position, hit.transform.position, Color.green);
            }
        }
    }

    // 可视化圆锥范围
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
