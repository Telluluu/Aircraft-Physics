using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour
{
    [SerializeField]
    private List<AeroSurface> controlSurfaces = null;

    [SerializeField]
    private List<WheelCollider> wheels = null;

    [SerializeField]
    private float rollControlSensitivity = 0.2f;

    [SerializeField]
    private float pitchControlSensitivity = 0.2f;

    [SerializeField]
    private float yawControlSensitivity = 0.2f;

    [Range(-1, 1)]
    public float Pitch;

    [Range(-1, 1)]
    public float Yaw;

    [Range(-1, 1)]
    public float Roll;

    [Range(0, 1)]
    public float Flap;

    [SerializeField]
    private Text displayText = null;

    private float thrustPercent;
    private float brakesTorque;

    private AircraftPhysics aircraftPhysics;
    private Rigidbody rb;

    private void Start()
    {
        aircraftPhysics = GetComponent<AircraftPhysics>();
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        //Pitch = Input.GetAxis("Vertical");
        //Roll = Input.GetAxis("Horizontal");
        //Yaw = Input.GetAxis("Yaw");

        UpdateMouseSteering();

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

    private void FixedUpdate()
    {
        SetControlSurfecesAngles(Pitch, Roll, Yaw, Flap);
        aircraftPhysics.SetThrustPercent(thrustPercent);
        foreach (var wheel in wheels)
        {
            wheel.brakeTorque = brakesTorque;
            // small torque to wake up wheel collider
            wheel.motorTorque = 0.01f;
        }
    }

    public void SetControlSurfecesAngles(float pitch, float roll, float yaw, float flap)
    {
        foreach (var surface in controlSurfaces)
        {
            if (surface == null || !surface.IsControlSurface) continue;
            switch (surface.InputType)
            {
                case ControlInputType.Pitch:
                    surface.SetFlapAngle(pitch * pitchControlSensitivity * surface.InputMultiplyer);
                    break;

                case ControlInputType.Roll:
                    surface.SetFlapAngle(roll * rollControlSensitivity * surface.InputMultiplyer);
                    break;

                case ControlInputType.Yaw:
                    surface.SetFlapAngle(yaw * yawControlSensitivity * surface.InputMultiplyer);
                    break;

                case ControlInputType.Flap:
                    surface.SetFlapAngle(Flap * surface.InputMultiplyer);
                    break;
            }
        }
    }

    private void UpdateMouseSteering()
    {
        // 按住 Alt 时自由观察，不控制飞机转向
        if (Input.GetKey(KeyCode.LeftAlt)) return;

        // 1. 获取鼠标在屏幕空间对应的世界射线方向 这比直接取 Camera.forward 更精准，因为它包含了鼠标在屏幕上的偏移
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Vector3 mouseWorldDirection = ray.direction;

        // 2. 将鼠标指向的世界方向转换为飞机的本地坐标系
        Vector3 localTargetDir = transform.InverseTransformDirection(mouseWorldDirection);

        // 3. 映射到飞行控制量 (范围限制在 -1 到 1) 俯仰 (Pitch)：目标在上方(y > 0)则抬头。如果发现反向，请给 localTargetDir.y 加负号
        Pitch = Mathf.Clamp(localTargetDir.y * -2.0f, -45f, 45f);

        // 偏航 (Yaw)：目标在右侧(x > 0)则右转
        Yaw = Mathf.Clamp(localTargetDir.x * -2.0f, -45f, 45f);

        // 翻滚 (Roll)：为了手感，向左转时向左倾斜
        Roll = Mathf.Clamp(-localTargetDir.x * -2.0f, -45f, 45f);
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying)
            SetControlSurfecesAngles(Pitch, Roll, Yaw, Flap);
    }
}
