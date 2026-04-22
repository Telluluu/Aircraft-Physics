using UnityEngine;

public class MissileLauncher : MonoBehaviour
{
    public GameObject missilePrefab;

    public MissileController LaunchMissile(DopplerRadar launcherRadar, Transform target)
    {
        var missileGO = Instantiate(missilePrefab, launcherRadar.transform.position, launcherRadar.transform.rotation);
        MissileController missileController = missileGO.GetComponent<MissileController>();
        missileController.SetTarget(launcherRadar, target);
        return missileController;
    }
}
