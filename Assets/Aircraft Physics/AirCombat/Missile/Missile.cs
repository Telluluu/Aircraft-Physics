using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Missile : MonoBehaviour
{
    public float explosionRadius = 15f;
    public float speed = 800f;
    public int downspeed = 30;
    public bool fully_active = false;
    public float timeStartActivition = 1;
    public float timeStopBursting = 40;
    public float timeBeforeDestruction = 100;
    public float timeAlive;
    public GameObject target;
    public GameObject shooter;
    public Rigidbody projectilerb;
    public bool isactive = false;
    public Vector3 sleepposition;
    public GameObject targetpointer;
    public float turnSpeed = 0.035f;
    public AudioSource launch_sound;
    public AudioSource thrust_sound;
    public GameObject smoke_obj;
    public ParticleSystem smoke;
    public GameObject smoke_position;
    public GameObject destroy_effect;

    private bool isActivate = false;
    private bool isBurstStarted = false;

    private void Start()
    {
        projectilerb = this.GetComponent<Rigidbody>();
    }

    public void CallDestroyEffects()
    {
        Instantiate(destroy_effect, transform.position, transform.rotation);
    }

    public void SetMissile()
    {
        timeAlive = 0;
        transform.rotation = shooter.transform.rotation;
        transform.Rotate(0, 90, 0);
        transform.position = shooter.transform.position;
    }

    public void DestroyMe()
    {
        isactive = false;
        fully_active = false;
        timeAlive = 0;
        smoke.transform.SetParent(null);
        smoke.Pause();
        smoke.transform.position = sleepposition;
        smoke.Play();
        projectilerb.linearVelocity = Vector3.zero;
        thrust_sound.Pause();
        CallDestroyEffects();
        transform.position = sleepposition;
        Destroy(smoke.gameObject, 3);
        Destroy(this.gameObject);
    }

    public void UseMissile()
    {
        launch_sound.Play();
        isactive = true;
        SetMissile();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isactive)
        {
            if (other.gameObject.CompareTag("Player"))
            {
                if (other.gameObject == shooter)
                {
                    if (fully_active)
                    {
                        //damege the shooter;
                        DestroyMe();
                    }
                }
                else
                {
                    //damage the enemy;
                    DestroyMe();
                }
            }
        }
    }

    // 假设你的 IFF 逻辑已经挂在目标或导弹上
    private void CheckProximityFuze()
    {
        // 只有在完全激活（fully_active）且有目标时才检测
        if (!fully_active || target == null) return;

        float dist = Vector3.Distance(transform.position, target.transform.position);

        // 1. 基础距离判定
        if (dist <= explosionRadius)
        {
            Destroy(target);
            DestroyMe();
        }
    }

    private void FixedUpdate()
    {
        if (isactive)
        {
            if (!target.activeInHierarchy)
            {
                DestroyMe();
            }
            if (timeAlive <= timeStartActivition)
            {
                if (fully_active == false)
                {
                    fully_active = true;
                    thrust_sound.Play();
                }
                projectilerb.linearVelocity = transform.up * -1 * downspeed;
            }
            timeAlive += Time.fixedDeltaTime;
            if (timeAlive > timeStartActivition && timeAlive <= timeStopBursting && !isBurstStarted)
            {
                isBurstStarted = true;
                smoke = (Instantiate(smoke_obj, smoke_position.transform.position, smoke_position.transform.rotation)).GetComponent<ParticleSystem>();
                smoke.Play();
                smoke.transform.SetParent(this.transform);
            }
            if (timeAlive >= timeBeforeDestruction)
            {
                DestroyMe();
            }
            if (timeAlive >= timeStartActivition && timeAlive < timeBeforeDestruction)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, targetpointer.transform.rotation, turnSpeed);
                projectilerb.linearVelocity = transform.forward * speed;
            }
            CheckProximityFuze();
        }
    }
}
