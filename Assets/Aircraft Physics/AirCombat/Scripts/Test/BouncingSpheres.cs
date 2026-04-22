using UnityEngine;
using System.Collections.Generic;

public class BouncingSpheres : MonoBehaviour
{
    [Header("设置")]
    public float radius = 10f;

    public float speed = 5f;

    private List<Rigidbody> sphereRbs = new List<Rigidbody>();

    private void Start()
    {
        for (int i = 0; i < 5; i++)
        {
            // 创建小球
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Rigidbody rb = sphere.AddComponent<Rigidbody>();

            // 基础配置
            rb.useGravity = false;
            sphere.transform.position = transform.position + Random.insideUnitSphere * (radius * 0.9f);

            // 移动规则：第一个在 XY 平面，其余随机
            Vector3 direction;
            if (i == 0)
            {
                direction = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), transform.position.z).normalized;
            }
            else
            {
                direction = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 12).normalized;
            }

            rb.linearVelocity = direction * speed;
            sphereRbs.Add(rb);
        }
    }

    private void FixedUpdate()
    {
        foreach (Rigidbody rb in sphereRbs)
        {
            // 距离检测
            if ((rb.position - transform.position).magnitude > radius)
            {
                // 计算反射向量：基于圆心方向
                Vector3 normal = -rb.position.normalized;
                rb.linearVelocity = Vector3.Reflect(rb.linearVelocity, normal).normalized * speed;

                // 强制修正位置，防止卡在边界外
                rb.position = transform.position + rb.position.normalized * radius;
            }

            // 确保第一个球始终锁定在 XY 平面（Z轴归零）
            if (rb == sphereRbs[0])
            {
                Vector3 pos = rb.position;
                pos.z = transform.position.z;
                rb.position = pos;

                Vector3 vel = rb.linearVelocity;
                vel.z = 0;
                rb.linearVelocity = vel.normalized * speed;
            }
        }
    }

    // 在编辑器中画出边界圆环方便观察
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
