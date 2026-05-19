using UnityEngine;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

[TaskCategory("BVR")]
public class TimerLLR : Action
{
    public float duration = 15f;
    public float remainingTime = 0f;

    public override void OnStart()
    {
        remainingTime = duration;
    }

    public override TaskStatus OnUpdate()
    {
        remainingTime -= Time.deltaTime;
        if (remainingTime <= 0f)
        {
            return TaskStatus.Success;
        }
        else
        {
            return TaskStatus.Running;
        }
    }
}
