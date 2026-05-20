using UnityEngine;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using System.Collections.Generic;

[TaskCategory("BVR")]
public class LDConditional : Conditional
{
    public MissileLauncher missileLauncher;
    public DopplerRadar radar;
    public DopplerRadar gamerRadar;

    public override void OnAwake()
    {
        missileLauncher = gameObject.GetComponentInChildren<MissileLauncher>();
        radar = gameObject.GetComponentInChildren<DopplerRadar>();
        gamerRadar = GameObject.Find("F/A-N26_Player")?.GetComponentInChildren<DopplerRadar>();
    }

    public override TaskStatus OnUpdate()
    {
        if (gamerRadar.CheckTarget(gameObject.transform.Find("Collision")) == true)
        {
            Debug.Log("LD failed");
            return TaskStatus.Failure;
        }
        bool isMissileLockon = false;
        foreach (var missile in missileLauncher.missiles)
        {
            if (missile == null)
                continue;
            DopplerRadar missileRadar = missile.GetComponentInChildren<DopplerRadar>();
            if (missileRadar == null)
                continue;
            if (missileRadar.lockedTargets.Count > 0)
            {
                isMissileLockon = true;
                break;
            }
        }
        if (isMissileLockon == true)
            return TaskStatus.Success;
        else
            return TaskStatus.Failure;
    }
}
