using UnityEngine;
using System.Collections.Generic;

public class RadarLockonUI : MonoBehaviour
{
    [Header("配置")]
    public GameObject lockonFramePrefab;

    public int maxPoolSize = 10;

    public int selectedIndex = 0;
    public List<Transform> targetsTransform = new List<Transform>();
    private List<LockonFrame> framePool = new List<LockonFrame>();
    private Camera mainCam;

    private void Awake()
    {
        mainCam = Camera.main;

        // 预先创建10个锁定框并隐藏
        for (int i = 0; i < maxPoolSize; i++)
        {
            GameObject frameGO = Instantiate(lockonFramePrefab, this.transform);
            frameGO.SetActive(false);
            var frame = frameGO.GetComponent<LockonFrame>();
            framePool.Add(frame);
        }
    }

    public void SetTargets(List<Transform> targets)
    {
        this.targetsTransform = targets;
    }

    private void Update()
    {
        UpdateLockonPositions();
    }

    private void UpdateLockonPositions()
    {
        if (targetsTransform == null) return;

        int targetCount = targetsTransform.Count;

        for (int i = 0; i < maxPoolSize; i++)
        {
            if (i < targetCount && targetsTransform[i] != null)
            {
                framePool[i].transform.gameObject.SetActive(true);

                // 将世界坐标转换为屏幕坐标
                Vector3 screenPos = mainCam.WorldToScreenPoint(targetsTransform[i].position);

                // 检查目标是否在相机前方（z > 0）
                if (screenPos.z > 0)
                {
                    screenPos.z = 0;
                    framePool[i].transform.position = screenPos;

                    var frame = framePool[i];
                    if (frame != null)
                    {
                        if (i == selectedIndex)
                            frame.Selected();
                        else
                            frame.UnSelected();
                    }
                }
                else
                {
                    framePool[i].transform.gameObject.SetActive(false);
                }
            }
            else
            {
                // 超过目标数量的框全部隐藏
                framePool[i].transform.gameObject.SetActive(false);
            }
        }
    }
}
