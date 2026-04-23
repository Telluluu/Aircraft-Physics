using UnityEngine;

public class PlayerController : AirplaneController
{
    public MissileLauncher missileLauncher;
    public DopplerRadar radar;
    public Transform target;
    public int lockID = -1;
    public IFF myIFF;

    protected override void Update()
    {
        Pitch = Input.GetAxis("Vertical");
        Roll = Input.GetAxis("Horizontal");
        Yaw = Input.GetAxis("Yaw");

        Gamelogic.GameManager.Instance.radarLockonUI.SetTargets(this.radar.lockedTargets);
        // 切换目标
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (this.radar.lockedTargets.Count == 0)
            {
                this.target = null;
                return;
            }
            if (this.lockID == -1)
                this.lockID = 0;

            lockID = (lockID + 1) % radar.lockedTargets.Count;
            Gamelogic.GameManager.Instance.radarLockonUI.selectedIndex = lockID;
            target = radar.lockedTargets[lockID].transform;
        }

        // 发射导弹
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (target != null)
                this.missileLauncher.ShootMissile(this.radar, this.target,
                 this.myIFF.affilation, this.myIFF.enemyAffilation);
        }
        // 节流阀
        if (Input.mouseScrollDelta.y != 0)
        {
            thrustPercent += Input.mouseScrollDelta.y * 0.1f;
            thrustPercent = Mathf.Clamp(thrustPercent, 0, 1);
        }

        // 收放襟翼
        if (Input.GetKeyDown(KeyCode.F))
        {
            Flap = Flap > 0 ? 0 : 0.3f;
        }

        // 减速板
        if (Input.GetKeyDown(KeyCode.B))
        {
            brakesTorque = brakesTorque > 0 ? 0 : 100f;
        }

        displayText.text = "V: " + ((int)rb.linearVelocity.magnitude).ToString("D3") + " m/s\n";
        displayText.text += "A: " + ((int)transform.position.y).ToString("D4") + " m\n";
        displayText.text += "T: " + (int)(thrustPercent * 100) + "%\n";
        displayText.text += brakesTorque > 0 ? "B: ON" : "B: OFF";
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying)
            base.SetControlSurfecesAngles(Pitch, Roll, Yaw, Flap);
    }
}
