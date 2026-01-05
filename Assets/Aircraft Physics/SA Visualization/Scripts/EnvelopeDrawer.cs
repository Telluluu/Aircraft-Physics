using UnityEditor;
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
    public float lineWidth = 10f;

    private void Start()
    {
        // 初始化线条宽度和闭合设置
        SetupLine(rMaxLine, Color.cyan);
        SetupLine(rMinLine, Color.yellow);
        SetupLine(nezMaxLine, Color.red);
        SetupLine(nezMinLine, Color.red);
    }

    private void SetupLine(LineRenderer line, Color color)
    {
        if (line == null) return;
        line.useWorldSpace = true;
        line.loop = true; // 关键：让包线形成闭合回路
        line.startWidth = line.endWidth = lineWidth;
        line.material = new Material(Shader.Find("Sprites/Default")); // 或使用高亮材质
        line.startColor = line.endColor = color;
    }

    /// <summary>
    /// 更新四条包线曲线
    /// </summary>
    public void DrawEnvelope(LaunchRange[] ranges, Vector3 center, Vector3 forward, Vector3 right)
    {
        if (ranges == null || ranges.Length == 0) return;

        int count = ranges.Length;

        // 确保 LineRenderer 的顶点数一致
        UpdateLinePoints(rMaxLine, ranges, center, forward, right, r => r.rMax);
        UpdateLinePoints(rMinLine, ranges, center, forward, right, r => r.rMin);
        UpdateLinePoints(nezMaxLine, ranges, center, forward, right, r => r.nezRMax);
        UpdateLinePoints(nezMinLine, ranges, center, forward, right, r => r.nezRMin);
    }

    private void UpdateLinePoints(LineRenderer line, LaunchRange[] ranges, Vector3 center, Vector3 fwd, Vector3 rt, System.Func<LaunchRange, float> radiusSelector)
    {
        if (line == null) return;

        line.positionCount = ranges.Length;
        Vector3[] points = new Vector3[ranges.Length];

        for (int i = 0; i < ranges.Length; i++)
        {
            float angle = ranges[i].initialMissilePsi;
            float radius = radiusSelector(ranges[i]);

            // 极坐标转世界坐标
            points[i] = center + (fwd * Mathf.Cos(angle) + rt * Mathf.Sin(angle)) * radius;
        }

        line.SetPositions(points);
    }
}
