using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class AircraftPhysics : MonoBehaviour
{
    private const float PREDICTION_TIMESTEP_FRACTION = 0.5f;

    [SerializeField]
    private float thrust = 0;

    [SerializeField]
    private List<AeroSurface> aerodynamicSurfaces = null;

    private Rigidbody rb;
    private float thrustPercent;
    private BiVector3 currentForceAndTorque;

    public void SetThrustPercent(float percent)
    {
        thrustPercent = percent;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        // 计算当前帧风吹在空气动力学表面上产生的力
        BiVector3 forceAndTorqueThisFrame =
            CalculateAerodynamicForces(rb.linearVelocity, rb.angularVelocity, Vector3.zero, 1.2f, rb.worldCenterOfMass);

        // 预测加上推力后，下一次计算的力
        Vector3 velocityPrediction = PredictVelocity(forceAndTorqueThisFrame.p
            + transform.forward * thrust * thrustPercent + Physics.gravity * rb.mass);
        Vector3 angularVelocityPrediction = PredictAngularVelocity(forceAndTorqueThisFrame.q);

        // 预测条件下，风吹在空气动力学表面上产生的力
        BiVector3 forceAndTorquePrediction =
            CalculateAerodynamicForces(velocityPrediction, angularVelocityPrediction, Vector3.zero, 1.2f, rb.worldCenterOfMass);

        // 取预测前后结果平均值
        currentForceAndTorque = (forceAndTorqueThisFrame + forceAndTorquePrediction) * 0.5f;

        // 在当前刚体上加上空气的力和力矩，以及发动机的力
        rb.AddForce(currentForceAndTorque.p);
        rb.AddTorque(currentForceAndTorque.q);

        rb.AddForce(transform.forward * thrust * thrustPercent);
    }

    // 空气动力学计算
    private BiVector3 CalculateAerodynamicForces(Vector3 velocity, Vector3 angularVelocity, Vector3 wind, float airDensity, Vector3 centerOfMass)
    {
        BiVector3 forceAndTorque = new BiVector3();
        // 遍历空气动力学表面
        foreach (var surface in aerodynamicSurfaces)
        {
            // 空气动力学表面离整体质心的相对位置
            Vector3 relativePosition = surface.transform.position - centerOfMass;
            // 风相对空气动力学表面的力与力矩（考虑飞机速度、风速、飞机旋转线速度）
            forceAndTorque += surface.CalculateForces(-velocity + wind
                - Vector3.Cross(angularVelocity,
                relativePosition),
                airDensity, relativePosition);
        }
        return forceAndTorque;
    }

    private Vector3 PredictVelocity(Vector3 force)
    {
        return rb.linearVelocity + Time.fixedDeltaTime * PREDICTION_TIMESTEP_FRACTION * force / rb.mass;
    }

    // 给机体一个扭矩，预测它的旋转角速度
    private Vector3 PredictAngularVelocity(Vector3 torque)
    {
        // 当前的旋转姿态
        Quaternion inertiaTensorWorldRotation = rb.rotation * rb.inertiaTensorRotation;
        // 从世界坐标系转换到机体的惯性主轴空间
        Vector3 torqueInDiagonalSpace = Quaternion.Inverse(inertiaTensorWorldRotation) * torque;
        // 应用惯性张量，角加速度=力矩/惯性矩:α=τ/I 惯性张量是一个对角矩阵，所以直接对应分量相除
        Vector3 angularVelocityChangeInDiagonalSpace;
        angularVelocityChangeInDiagonalSpace.x = torqueInDiagonalSpace.x / rb.inertiaTensor.x;
        angularVelocityChangeInDiagonalSpace.y = torqueInDiagonalSpace.y / rb.inertiaTensor.y;
        angularVelocityChangeInDiagonalSpace.z = torqueInDiagonalSpace.z / rb.inertiaTensor.z;
        // 把算出来的角速度增量转换回世界坐标系，加到当前rb的角速度上
        return rb.angularVelocity + Time.fixedDeltaTime * PREDICTION_TIMESTEP_FRACTION
            * (inertiaTensorWorldRotation * angularVelocityChangeInDiagonalSpace);
    }

#if UNITY_EDITOR

    // For gizmos drawing. 计算升力中心
    public void CalculateCenterOfLift(out Vector3 center, out Vector3 force, Vector3 displayAirVelocity, float displayAirDensity)
    {
        Vector3 com;
        BiVector3 forceAndTorque;
        if (aerodynamicSurfaces == null)
        {
            center = Vector3.zero;
            force = Vector3.zero;
            return;
        }

        if (rb == null)
        {
            com = GetComponent<Rigidbody>().worldCenterOfMass;
            forceAndTorque = CalculateAerodynamicForces(-displayAirVelocity, Vector3.zero, Vector3.zero, displayAirDensity, com);
        }
        else
        {
            com = rb.worldCenterOfMass;
            forceAndTorque = currentForceAndTorque;
        }

        force = forceAndTorque.p;
        center = com + Vector3.Cross(forceAndTorque.p, forceAndTorque.q) / forceAndTorque.p.sqrMagnitude;
    }

#endif
}
