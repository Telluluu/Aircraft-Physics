using UnityEngine;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

[TaskCategory("BVR")]
public class CheckTartics : Conditional
{
    private int caseValue;
    public int tarticValue;
    private Rigidbody rb;
    private DopplerRadar radar;

    public override void OnAwake()
    {
        rb = gameObject.GetComponent<Rigidbody>();
        radar = gameObject.GetComponentInChildren<DopplerRadar>();
    }

    public override void OnStart()
    {
        if (rb == null || radar == null || radar.lockedTargets.Count == 0)
            return;

        Rigidbody targetRb = radar.lockedTargets[0].parent.GetComponent<Rigidbody>();
        float myVelocity = rb.linearVelocity.magnitude;
        float targetVelocity = targetRb.linearVelocity.magnitude;

        //Debug.Log(gameObject.name + ":" + myVelocity + "m/s");
        //Debug.Log(targetRb.gameObject.name + ":" + targetVelocity + "m/s");

        /*
         * 目标能量远小于自身，LLR 发射-脱离-追击；caseValue = 0
         * 目标能量远大于自身，LL 发射-脱离；caseValue = 1
         * 目标能量接近自身，LD 发射-决断；caseValue = 2
        */
        if (targetVelocity < myVelocity * 0.8f)
            caseValue = 0;
        else if (targetVelocity > myVelocity * 1.2f)
            caseValue = 1;
        else
            caseValue = 2;
    }

    public override TaskStatus OnUpdate()
    {
        if (tarticValue != caseValue)
            return TaskStatus.Failure;
        else
            return TaskStatus.Success;
    }
}
