using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MissilePointer : MonoBehaviour
{
    public GameObject target;
    public DopplerRadar aircraftRadar;
    public DopplerRadar missileRadar;
    public bool _isLocked;

    private void Update()
    {
        if (target != null)
        {
            _isLocked = aircraftRadar.CheckTarget(target.transform) || missileRadar.CheckTarget(target.transform);
        }
    }

    private void FixedUpdate()
    {
        if (_isLocked && target != null)
            transform.LookAt(target.transform.position);
    }
}
