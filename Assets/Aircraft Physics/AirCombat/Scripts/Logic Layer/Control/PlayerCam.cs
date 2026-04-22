using UnityEngine;

public class PlayerCam : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float distance = 12.0f;
    [SerializeField] private float sensitivity = 2.0f;

    [Header("跟随偏移")]
    [SerializeField] private Vector3 followOffset = new Vector3(0, 2.5f, 0); // 抬高视点

    [SerializeField] private float smoothSpeed = 10f; // 回位速度

    private float yaw;
    private float pitch;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Confined;
        // 初始同步一次角度
        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;
    }

    private void LateUpdate()
    {
        if (!target) return;

        if (Input.GetKey(KeyCode.LeftAlt))
        {
            Cursor.lockState = CursorLockMode.Locked;
            HandleFreeLook();
        }
        else
        {
            Cursor.lockState = CursorLockMode.Confined;
            HandleStandardFollow();
        }
    }

    private void HandleFreeLook()
    {
        // 自由旋转模式
        yaw += Input.GetAxis("Mouse X") * sensitivity;
        pitch -= Input.GetAxis("Mouse Y") * sensitivity;
        pitch = Mathf.Clamp(pitch, -60f, 60f);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
        // 自由视角建议也加上 followOffset，围绕飞机的中心上方旋转，视觉更好
        Vector3 position = (target.position + target.TransformDirection(followOffset)) - (rotation * Vector3.forward * distance);

        transform.rotation = rotation;
        transform.position = position;
    }

    private void HandleStandardFollow()
    {
        // 常规跟随模式：目标位置在飞机后方，并叠加偏移 使用 target.forward 确保相机永远在机尾
        Vector3 desiredPos = (target.position + target.TransformDirection(followOffset)) - (target.forward * distance);

        // 平滑移动位置
        transform.position = Vector3.Lerp(transform.position, desiredPos, Time.deltaTime * smoothSpeed);

        // 视角朝向：看向飞机前方的一个点（让飞机处于屏幕中下部）
        Quaternion desiredRot = Quaternion.LookRotation(target.position + target.forward * 20f + target.up * 2f - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, Time.deltaTime * smoothSpeed);

        // 关键：在跟随模式下实时更新变量，保证按下 Alt 时衔接顺滑
        Vector3 currentAngles = transform.eulerAngles;
        yaw = currentAngles.y;
        pitch = NormalizeAngle(currentAngles.x);
    }

    // 处理 eulerAngles 0-360 转换到 -180-180 的问题
    private float NormalizeAngle(float angle)
    {
        if (angle > 180f) angle -= 360f;
        return angle;
    }
}
