using UnityEngine;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

[TaskCategory("BVR")]
public class TimerLLR : Action
{
    public float duration = 15f;
    public float remainingTime = 0f;
    public DopplerRadar gamerRadar;

    public override void OnAwake()
    {
        gamerRadar = GameObject.Find("F/A-N26_Player")?.GetComponentInChildren<DopplerRadar>();
    }

    public override void OnStart()
    {
        remainingTime = duration;
    }

    public override TaskStatus OnUpdate()
    {
        remainingTime -= Time.deltaTime;

        if (remainingTime <= 0f)
        {
            // 未被锁定则转热追击，否则保持冷向
            if (gamerRadar.CheckTarget(gameObject.transform.Find("Collision")) == false)
                return TaskStatus.Success;
            else
                return TaskStatus.Failure;
        }
        else
        {
            return TaskStatus.Running;
        }
    }
}
