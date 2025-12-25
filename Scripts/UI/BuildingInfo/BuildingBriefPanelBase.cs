using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BuildingBriefPanelBase : MonoBehaviour
{

    [SerializeField] private string panelGuid;
    public string PanelGuid {  get { return panelGuid; } }    

    private void Reset()
    {
        panelGuid = Guid.NewGuid().ToString();
    }

    protected abstract void ShowInfo(BuildingInstance building);

    public virtual void Show(RectTransform rectTransform, BuildingInstance building)
    {
        if (rectTransform == null)
        {
            Debug.LogWarning("信息显示画布为空");
        }


        // 尝试把自己当作 RectTransform 使用
        RectTransform self = transform as RectTransform;

        // 设为子物体（不保持世界坐标，方便直接贴合父节点）
        if (self != null)
            self.SetParent(rectTransform,false);
        else
            transform.SetParent(rectTransform,false);

        if (self != null)
        {
            // 将锚点设置为四个角（拉伸充满父节点）
            self.anchorMin = Vector2.zero;
            // (0,0)
            self.anchorMax = Vector2.one;
            // (1,1)

            // 置中 Pivot（可按需调整）
            self.pivot = new Vector2(0.5f, 0.5f);

            // 清空位置与偏移，确保完全贴合父节点
            self.anchoredPosition = Vector2.zero;
            self.offsetMin = Vector2.zero;
            self.offsetMax = Vector2.zero;

            // 保证缩放为 1
            self.localScale = Vector3.one;

            // 可选：放到最上层
            self.SetAsLastSibling();
        }

        // 激活并渲染内容
        gameObject.SetActive(true);
        ShowInfo(building);
    }

}
