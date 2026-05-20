using UnityEngine;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

[TaskCategory("BVR")]
public class CruiseAction : Action
{
    private JetNpcController npcController;
    private DopplerRadar radar;
    private Rigidbody rb;

    public float duration = 30f;
    public float timer = 0f;

    public override void OnAwake()
    {
        npcController = gameObject.GetComponent<JetNpcController>();
        radar = gameObject.GetComponentInChildren<DopplerRadar>();
        rb = gameObject.GetComponent<Rigidbody>();
    }

    public override void OnStart()
    {
        timer = duration;
    }

    public override TaskStatus OnUpdate()
    {
        if (npcController == null || radar == null || radar.lockedTargets.Count > 0)
            return TaskStatus.Failure;

        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            if (rb.linearVelocity.magnitude >= npcController.cruiseSpeed - 15f)
                return TaskStatus.Success;
            timer = duration / 2;
        }
        npcController.ControlThrottle(npcController.cruiseSpeed);
        npcController.CruiseMode();
        return TaskStatus.Running;
    }
}
