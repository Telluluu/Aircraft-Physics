using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HomingMissile
{
    public class shoot_missile_example : MonoBehaviour
    {
        public GameObject missile_prefab;
        public GameObject target;

        public void shoot_missile()
        {
            GameObject go_missile = Instantiate(missile_prefab, new Vector3(200, 200, 200), transform.rotation);
            homing_missile missile = go_missile.GetComponent<homing_missile>();
            missile.target = target;
            missile.targetpointer.GetComponent<homing_missile_pointer>().target = target;
            missile.shooter = this.gameObject;
            missile.usemissile();
        }
    }
}
