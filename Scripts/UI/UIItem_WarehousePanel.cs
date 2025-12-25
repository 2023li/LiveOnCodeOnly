using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using Lean.Pool;

public class UIItem_WarehousePanel : MonoBehaviour
{
    [SerializeField, LabelText("库存Item预制体")]
    private IconTextButton itemPrefab;

    [SerializeField, LabelText("库存物资容器父对象")]
    private RectTransform itemContainer;

    [SerializeField, LabelText("关闭仓库按钮")]
    private Button btn_CloseWarehousePanel;

    private ResourceNetwork resourceNetwork;

    /// <summary>
    /// 当前激活的条目，用来统一回收到对象池
    /// </summary>
    private readonly List<IconTextButton> activeItems = new List<IconTextButton>();

    private void Awake()
    {
        resourceNetwork = GameContext.Instance.ResourceNetwork;

        if (btn_CloseWarehousePanel != null)
        {
            btn_CloseWarehousePanel.onClick.RemoveAllListeners();
            btn_CloseWarehousePanel.onClick.AddListener(Hide);
        }

        if (resourceNetwork != null)
        {
            // 资源网络有变化时自动刷新界面
            resourceNetwork.OnResourceNetworkStateChange += RefreshView;
        }

        // 默认先隐藏
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (resourceNetwork != null)
        {
            resourceNetwork.OnResourceNetworkStateChange -= RefreshView;
        }

        ClearItems();
    }

    /// <summary>
    /// 把当前所有 UI item 丢回对象池
    /// </summary>
    private void ClearItems()
    {
        for (int i = 0; i < activeItems.Count; i++)
        {
            var item = activeItems[i];
            if (item != null)
            {
                // 使用 LeanPool 回收，而不是 Destroy
                LeanPool.Despawn(item.gameObject);
            }
        }

        activeItems.Clear();
    }

    /// <summary>
    /// 刷新仓库物资显示
    /// </summary>
    private void RefreshView()
    {
        if (itemPrefab == null || itemContainer == null || resourceNetwork == null)
            return;

        // 先回收旧的
        ClearItems();

        // 遍历资源网络中的所有资源
        foreach (var sa in resourceNetwork.GetAllResourcesSnapshot())
        {
            var def = sa.Resource;
            int amount = sa.Amount;

            if (def == null || amount <= 0)
                continue;

            // 不显示的物资直接跳过
            if (def.DisplaySetting == SupplyDef.DisplayOption.不显示)
                continue;

            // 用 LeanPool 生成一个 item
            var item = LeanPool.Spawn(itemPrefab, itemContainer);
            activeItems.Add(item);

            // 显示名称优先用 LevelDisplayName，没填就用资源名
            string resName =
                string.IsNullOrEmpty(def.DisplayName) ? def.name : def.DisplayName;

            // text 显示数量，icon 显示物资图标
            item.SetContent(amount.ToString(), def.Icon);

            // 点击按钮打印物资名称
            item.SetOnClick(() =>
            {
                Debug.Log($"点击仓库物资：{resName}");
            });
        }
    }

    /// <summary>
    /// 打开仓库面板
    /// </summary>
    public void Show()
    {
        gameObject.SetActive(true);
        RefreshView();
    }

    /// <summary>
    /// 隐藏仓库面板
    /// </summary>
    public void Hide()
    {
        gameObject.SetActive(false);
      
         ClearItems();
    }
}
