using UnityEngine;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

[TaskCategory("BVR")]
public class LaunchAction : Action
{
    private DopplerRadar radar;
    private IFF iff;
    private MissileLauncher missileLauncher;

    public override void OnAwake()
    {
        radar = gameObject.GetComponentInChildren<DopplerRadar>();
        iff = gameObject.GetComponentInChildren<IFF>();
        missileLauncher = gameObject.GetComponentInChildren<MissileLauncher>();
    }

    public override void OnStart()
    {
        if (radar == null || iff == null || radar.lockedTargets.Count == 0 || missileLauncher == null)
            return;

        missileLauncher.ShootMissile(radar, radar.lockedTargets[0], iff.affilation, iff.enemyAffilation);
    }
}
