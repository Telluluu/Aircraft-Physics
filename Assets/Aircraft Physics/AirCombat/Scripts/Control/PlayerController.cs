using HomingMissile;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements.InputSystem;

public class PlayerController : AirplaneController
{
    #region
    //[SerializeField]
    //private List<AeroSurface> controlSurfaces = null;

    //[SerializeField]
    //private List<WheelCollider> wheels = null;

    //[SerializeField]
    //private float rollControlSensitivity = 0.2f;

    //[SerializeField]
    //private float pitchControlSensitivity = 0.2f;

    //[SerializeField]
    //private float yawControlSensitivity = 0.2f;

    //[SerializeField]
    //private Text displayText = null;

    //private float thrustPercent;
    //private float brakesTorque;

    //private AircraftPhysics aircraftPhysics;
    //private Rigidbody rb;

    //protected override void Start()
    //{
    //}
    #endregion

    protected override void Update()
    {
        Pitch = Input.GetAxis("Vertical");
        Roll = Input.GetAxis("Horizontal");
        Yaw = Input.GetAxis("Yaw");

        // 节流阀
        if (Input.GetKeyDown(KeyCode.Space))
        {
            thrustPercent = thrustPercent > 0 ? 0 : 1f;
        }

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
