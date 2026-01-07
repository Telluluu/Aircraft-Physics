using UnityEngine;
using System.Collections.Generic;

public class RadarLockonUI : MonoBehaviour
{
    [Header("配置")]
    public GameObject lockonFramePrefab;

    public int maxPoolSize = 10;

    public List<Transform> targetsTransform = new List<Transform>();
    private List<GameObject> framePool = new List<GameObject>();
    private Camera mainCam;

    private void Awake()
    {
        mainCam = Camera.main;

        // 预先创建10个锁定框并隐藏
        for (int i = 0; i < maxPoolSize; i++)
        {
            GameObject frame = Instantiate(lockonFramePrefab, this.transform);
            frame.SetActive(false);
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
                framePool[i].SetActive(true);

                // 将世界坐标转换为屏幕坐标
                Vector3 screenPos = mainCam.WorldToScreenPoint(targetsTransform[i].position);

                // 检查目标是否在相机前方（z > 0）
                if (screenPos.z > 0)
                {
                    framePool[i].transform.position = screenPos;
                }
                else
                {
                    framePool[i].SetActive(false);
                }
            }
            else
            {
                // 超过目标数量的框全部隐藏
                framePool[i].SetActive(false);
            }
        }
    }
}
