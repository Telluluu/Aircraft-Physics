using UnityEngine;
using static MissileFireControl;

public class EnvelopeDrawer : MonoBehaviour
{
    [Header("Line Renderers")]
    public LineRenderer rMaxLine;

    public LineRenderer rMinLine;
    public LineRenderer nezMaxLine;
    public LineRenderer nezMinLine;

    [Header("Visual Settings")]
    public float lineWidth = 2f;

    [Tooltip("缩放比例：0.001 代表 1000米显示为 1个Unity单位")]
    public float displayScale = 1f;

    [Range(0.01f, 1f)]
    public float lerpSpeed = 0.15f; // 数值变动平滑度

    // 内部平滑缓存，防止解算跳变
    private float[] lerpRMax, lerpRMin, lerpNezMax, lerpNezMin;

    private LaunchRange[] latestData;
    private bool initialized = false;

    private void Start()
    {
        // 自动配置 LineRenderer
        SetupLine(rMaxLine, Color.cyan);
        SetupLine(rMinLine, Color.yellow);
        SetupLine(nezMaxLine, Color.red);
        SetupLine(nezMinLine, new Color(1f, 0.5f, 0f)); // 橙色区分
    }

    private void SetupLine(LineRenderer line, Color color)
    {
        if (line == null) return;
        line.useWorldSpace = false; // 关键：锁定在载机本地坐标系
        line.loop = true;
        line.startWidth = line.endWidth = lineWidth;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = line.endColor = color;
        line.positionCount = 0;
    }

    /// <summary>
    /// 当你的后台解算循环拿到新数据时，调用此方法传入 LaunchRange[]
    /// </summary>
    public void PushNewData(LaunchRange[] newData)
    {
        if (newData == null || newData.Length == 0) return;

        if (!initialized || lerpRMax.Length != newData.Length)
        {
            int len = newData.Length;
            lerpRMax = new float[len];
            lerpRMin = new float[len];
            lerpNezMax = new float[len];
            lerpNezMin = new float[len];
            initialized = true;
        }
        latestData = newData;
    }

    private void Update()
    {
        if (!initialized || latestData == null) return;

        // 每帧执行：插值计算 + 顶点刷新
        UpdateAllLines();
    }

    private void UpdateAllLines()
    {
        int count = latestData.Length;

        // 分别更新四个 LineRenderer
        DrawSingleLine(rMaxLine, count, i =>
        {
            lerpRMax[i] = Mathf.Lerp(lerpRMax[i], latestData[i].rMax, lerpSpeed);
            return lerpRMax[i];
        });

        DrawSingleLine(rMinLine, count, i =>
        {
            lerpRMin[i] = Mathf.Lerp(lerpRMin[i], latestData[i].rMin, lerpSpeed);
            return lerpRMin[i];
        });

        DrawSingleLine(nezMaxLine, count, i =>
        {
            lerpNezMax[i] = Mathf.Lerp(lerpNezMax[i], latestData[i].nezRMax, lerpSpeed);
            return lerpNezMax[i];
        });

        DrawSingleLine(nezMinLine, count, i =>
        {
            lerpNezMin[i] = Mathf.Lerp(lerpNezMin[i], latestData[i].nezRMin, lerpSpeed);
            return lerpNezMin[i];
        });
    }

    private void DrawSingleLine(LineRenderer line, int count, System.Func<int, float> radiusPicker)
    {
        if (line == null) return;

        line.positionCount = count;
        Vector3[] points = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            // 使用解算结果自带的 initialMissilePsi (角度)
            float angle = latestData[i].initialMissilePsi;
            float radius = radiusPicker(i) * displayScale;

            // 转换到本地坐标系：Z轴为前，X轴为右 如果你的解算器 0度是正前方，则 Cos对应Z，Sin对应X
            float x = Mathf.Sin(angle * Mathf.Deg2Rad) * radius;
            float z = Mathf.Cos(angle * Mathf.Deg2Rad) * radius;

            points[i] = new Vector3(x, 0, z);
        }

        line.SetPositions(points);
    }

    public void Clear()
    {
        // 1. 视觉清除：将所有 LineRenderer 的顶点数归零
        ClearLine(rMaxLine);
        ClearLine(rMinLine);
        ClearLine(nezMaxLine);
        ClearLine(nezMinLine);

        // 2. 数据清除：断开对最新数据的引用
        latestData = null;

        // 3. 缓存重置：将插值数组清零，防止下次显示时从旧位置“飞”过来
        if (initialized)
        {
            System.Array.Clear(lerpRMax, 0, lerpRMax.Length);
            System.Array.Clear(lerpRMin, 0, lerpRMin.Length);
            System.Array.Clear(lerpNezMax, 0, lerpNezMax.Length);
            System.Array.Clear(lerpNezMin, 0, lerpNezMin.Length);
        }

        // 注意：initialized 保持为 true 即可，无需重新分配内存
    }

    private void ClearLine(LineRenderer line)
    {
        if (line != null)
        {
            line.positionCount = 0;
        }
    }
}
