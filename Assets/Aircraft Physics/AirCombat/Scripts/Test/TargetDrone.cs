using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class TargetDrone : MonoBehaviour
{
    [Header("Movement Settings")]
    public float cruiseSpeed = 200f;      // 巡航速度

    public float turnSpeed = 2f;         // 转向灵敏度
    public float movementScale = 0.1f;   // 随机运动的剧烈程度
    public Vector3 flyRange = new Vector3(2000, 1000, 2000); // 飞行的活动范围限制

    private Rigidbody rb;
    private float seedX, seedY, seedZ;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;

        // 随机种子，保证多个目标的轨迹不同
        seedX = Random.value * 100f;
        seedY = Random.value * 100f;
        seedZ = Random.value * 100f;
    }

    private void FixedUpdate()
    {
        MoveRandomly();
        ApplyBoundaryForce();
    }

    private void MoveRandomly()
    {
        // 使用柏林噪声计算平滑的航向偏转
        float noiseX = Mathf.PerlinNoise(Time.time * movementScale, seedX) * 2f - 1f;
        float noiseY = Mathf.PerlinNoise(Time.time * movementScale, seedY) * 2f - 1f;
        float noiseZ = Mathf.PerlinNoise(Time.time * movementScale, seedZ) * 2f - 1f;

        Vector3 randomDirection = new Vector3(noiseX, noiseY, noiseZ);

        // 平滑旋转向随机方向
        Quaternion targetRotation = Quaternion.LookRotation(transform.forward + randomDirection);
        rb.MoveRotation(Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime));

        // 始终向前飞行，保持速度
        rb.linearVelocity = transform.forward * cruiseSpeed;
    }

    private void ApplyBoundaryForce()
    {
        // 如果超出预定范围，强制转向中心，防止跑太远
        if (transform.position.magnitude > flyRange.magnitude)
        {
            Vector3 dirToCenter = -transform.position.normalized;
            transform.forward = Vector3.Lerp(transform.forward, dirToCenter, Time.fixedDeltaTime * turnSpeed);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(Vector3.zero, flyRange * 2);
    }
}
