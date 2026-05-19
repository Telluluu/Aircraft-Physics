using UnityEngine;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

[TaskCategory("BVR")]
public class GuideAfterLaunchAction : Action
{
    private JetNpcController npcController;
    private DopplerRadar radar;

    public float minTurnDistance = 15000f;

    [Header("解算输出（预计脱离时间，秒）")]
    public float timeToTurn = 999f;

    public override void OnAwake()
    {
        npcController = gameObject.GetComponent<JetNpcController>();
        radar = gameObject.GetComponentInChildren<DopplerRadar>();
    }

    public override void OnStart()
    {
        timeToTurn = 999f;
    }

    public override TaskStatus OnUpdate()
    {
        if (npcController == null || radar == null)
        {
            timeToTurn = 999f;
            return TaskStatus.Failure;
        }

        Transform target = radar.lockedTargets[0];
        if (target == null) return TaskStatus.Success;

        // 1. 动态解算预计脱离时间（TTT）
        CalculateTimeToTurn(target);

        // 2. 发射后持续咬住目标，维持中继引导的物理姿态
        Vector3 dirToTarget = (target.position - transform.position).normalized;
        npcController.ControlThrottle(npcController.combatSpeed);
        npcController.ApplyYawTask(dirToTarget);

        float pitchError = target.position.y - transform.position.y;
        float targetPitch = Mathf.Clamp(pitchError * 0.1f, -20f, 20f);
        npcController.ApplyRollTask(0f);
        npcController.ApplyPitchTask(targetPitch);

        // 3. 断点判定：时间归零，意味着逼近安全死线，必须切断机头交由下个节点脱离
        if (timeToTurn <= 0f)
        {
            npcController.ResetInputs(); // 释放残留物理控制量
            return TaskStatus.Success;   // 节点成功，移交给后续大 G 转向防御节点
        }

        return TaskStatus.Running;
    }

    private void CalculateTimeToTurn(Transform target)
    {
        float currentDistance = Vector3.Distance(transform.position, target.position);

        if (currentDistance <= minTurnDistance)
        {
            timeToTurn = 0f;
            return;
        }

        Vector3 dirToTarget = (target.position - transform.position).normalized;
        Vector3 myVelocity = npcController.GetComponent<Rigidbody>().linearVelocity;
        Vector3 targetVelocity = target.GetComponent<Rigidbody>() ? target.GetComponent<Rigidbody>().linearVelocity : Vector3.zero;

        float closureRate = Vector3.Dot(myVelocity, dirToTarget) - Vector3.Dot(targetVelocity, dirToTarget);

        if (closureRate <= 0.1f)
        {
            timeToTurn = 999f;
            return;
        }

        timeToTurn = (currentDistance - minTurnDistance) / closureRate;
    }
}
