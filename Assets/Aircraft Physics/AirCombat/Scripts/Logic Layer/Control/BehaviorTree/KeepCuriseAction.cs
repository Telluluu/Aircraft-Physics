using UnityEngine;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

[TaskCategory("BVR")]
public class KeepCuriseAction : Action
{
    private JetNpcController npcController;

    public float duration = 30f;
    public float timer = 0f;

    public override void OnAwake()
    {
        npcController = gameObject.GetComponent<JetNpcController>();
    }

    public override void OnStart()
    {
        timer = duration;
    }

    public override TaskStatus OnUpdate()
    {
        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            return TaskStatus.Success;
        }
        npcController.ControlThrottle(npcController.cruiseSpeed);
        npcController.CruiseMode();
        return TaskStatus.Running;
    }
}
