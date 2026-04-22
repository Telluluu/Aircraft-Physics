using HomingMissile;
using UnityEngine;

public class MissileLauncher : MonoBehaviour
{
    public GameObject missilePrefab;

    public void ShootMissile(DopplerRadar aircraftRadar, Transform targetTrans,
        IFF.IFFTeamType shooterIFF, IFF.IFFTeamType targetIFF)
    {
        GameObject go_missile = Instantiate(missilePrefab, transform.position, transform.rotation);
        Missile missile = go_missile.GetComponent<Missile>();
        missile.target = targetTrans.gameObject;
        missile.shooter = this.gameObject;

        MissilePointer missilePointer = missile.targetpointer.GetComponent<MissilePointer>();
        missilePointer.target = targetTrans.gameObject;
        missilePointer.aircraftRadar = aircraftRadar;
        missilePointer.missileRadar.iff.affilation = shooterIFF;
        missilePointer.missileRadar.iff.enemyAffilation = targetIFF;
        missilePointer.missileRadar.iff.objectType = IFF.IFFObjectType.Missile;

        missile.UseMissile();
    }
}
