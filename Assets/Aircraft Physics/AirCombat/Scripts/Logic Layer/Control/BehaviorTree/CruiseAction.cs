using UnityEngine;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

public class CruiseAction : Action
{
    private JetNpcController npcController;
    private DopplerRadar radar;

    public override void OnAwake()
    {
        npcController = gameObject.GetComponent<JetNpcController>();
        radar = gameObject.GetComponentInChildren<DopplerRadar>();
    }

    public override TaskStatus OnUpdate()
    {
        if (npcController == null || radar == null)
            return TaskStatus.Failure;
        if (radar.lockedTargets.Count > 0)
            return TaskStatus.Success;
        npcController.ControlThrottle(npcController.cruiseSpeed);
        npcController.CruiseMode();
        return TaskStatus.Running;
    }
}
