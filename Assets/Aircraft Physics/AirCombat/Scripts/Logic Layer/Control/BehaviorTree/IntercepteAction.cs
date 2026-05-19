using UnityEngine;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

[TaskCategory("BVR")]
public class IntercepteAction : Action
{
    private JetNpcController npcController;
    private DopplerRadar radar;

    public float fireDistance = 40000f;
    public float timeToFire = 999f;

    public override void OnAwake()
    {
        npcController = gameObject.GetComponent<JetNpcController>();
        radar = gameObject.GetComponentInChildren<DopplerRadar>();
    }

    public override void OnStart()
    {
        timeToFire = 999f;
    }

    public override TaskStatus OnUpdate()
    {
        if (npcController == null || radar == null || radar.lockedTargets.Count == 0)
        {
            timeToFire = 999f;
            return TaskStatus.Failure;
        }
        Transform target = radar.lockedTargets[0];
        // 1. 动态逻辑解算时序
        CalculateTimeToFire(target);
        // 2. 维持拦截
        Vector3 dirToTarget = (target.position - transform.position).normalized;
        npcController.ControlThrottle(npcController.combatSpeed);
        npcController.ApplyYawTask(dirToTarget);

        float pitchError = target.position.y - transform.position.y;
        float targetPitch = Mathf.Clamp(pitchError * 0.1f, -20f, 20f);
        npcController.ApplyRollTask(0f);
        npcController.ApplyPitchTask(targetPitch);

        // 3. 断点判定：一旦实际距离缩短到最优发射距离内，时间归零，进入发射节点
        if (timeToFire <= 0f)
        {
            return TaskStatus.Success;
        }

        return TaskStatus.Running;
    }

    private void CalculateTimeToFire(Transform target)
    {
        float currentDistance = Vector3.Distance(transform.position, target.position);

        if (currentDistance <= fireDistance)
        {
            timeToFire = 0f;
            return;
        }

        Vector3 dirToTarget = (target.position - transform.position).normalized;
        Vector3 myVelocity = npcController.GetComponent<Rigidbody>().linearVelocity;
        Vector3 targetVelocity = target.GetComponent<Rigidbody>() ? target.GetComponent<Rigidbody>().linearVelocity : Vector3.zero;

        float closureRate = Vector3.Dot(myVelocity, dirToTarget) - Vector3.Dot(targetVelocity, dirToTarget);

        if (closureRate <= 0.1f)
        {
            timeToFire = 999f;
            return;
        }

        timeToFire = (currentDistance - fireDistance) / closureRate;
    }
}
