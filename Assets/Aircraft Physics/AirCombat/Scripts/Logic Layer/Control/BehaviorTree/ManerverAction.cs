using UnityEngine;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

[TaskCategory("BVR")]
public class ManerverAction : Action
{
    private JetNpcController npcController;

    public float targetRoll = 88f;
    public float deltaPitch = 180f;
    public float targetG = 5.0f;

    public override void OnAwake()
    {
        npcController = gameObject.GetComponent<JetNpcController>();
    }

    public override void OnStart()
    {
        npcController.maneuverRoutine = StartCoroutine(npcController.Maneuver_Relative(targetRoll, deltaPitch, targetG));
        npcController.isManeuvering = true;
    }

    public override TaskStatus OnUpdate()
    {
        if (npcController.isManeuvering == true)
            return TaskStatus.Running;
        else
            return TaskStatus.Success;
    }

    public override void OnEnd()
    {
        if (npcController != null && npcController.isManeuvering)
        {
            if (npcController.maneuverRoutine != null)
            {
                StopCoroutine(npcController.maneuverRoutine.ToString());
            }
            npcController.isManeuvering = false;
            npcController.ResetInputs();
        }
    }
}
