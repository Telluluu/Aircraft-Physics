using UnityEngine;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

public class WithinSight : Conditional
{
    public DopplerRadar radar;
    public JetNpcController npcController;

    public override void OnAwake()
    {
        radar = gameObject.GetComponentInChildren<DopplerRadar>();
        npcController = gameObject.GetComponent<JetNpcController>();
    }

    public override TaskStatus OnUpdate()
    {
        if (radar != null)
        {
            if (radar.lockedTargets.Count > 0)
            {
                npcController.isCombatMode = true;
                return TaskStatus.Success;
            }
        }
        npcController.isCombatMode = false;
        return TaskStatus.Failure;
    }
}
