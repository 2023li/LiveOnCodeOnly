using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Moyo.Unity;
using UnityEngine.UI;

/// <summary>
/// 查询面板
/// </summary>
public class UIItem_QueryPanel : MonoBehaviour
{
    [SerializeField] private Toggle toggle_治安;
    [SerializeField] private Toggle toggle_医疗;
    [SerializeField] private Toggle toggle_环境;

    private Dictionary<Toggle, AuraCategory> toggleCategoryMap;
    private bool isInitialized = false;

    
    private void Awake()
    {

        Debug.LogWarning("需要完成");
       // InitializeByData();
    }
    /*
    private void InitializeByData()
    {
        if (isInitialized) return;


        // 建立Toggle与光环类型的映射
        toggleCategoryMap = new Dictionary<Toggle, AuraCategory>
        {
            { toggle_治安, AuraCategory.Security },
            { toggle_医疗, AuraCategory.Health },
            { toggle_环境, AuraCategory.Beauty }
        };

        // 添加监听事件
        if (toggle_治安 != null) toggle_治安.onValueChanged.AddListener(OnToggleChanged);
        if (toggle_医疗 != null) toggle_医疗.onValueChanged.AddListener(OnToggleChanged);
        if (toggle_环境 != null) toggle_环境.onValueChanged.AddListener(OnToggleChanged);

        isInitialized = true;
    }

    /// <summary>
    /// Toggle状态改变时的回调
    /// </summary>
    private void OnToggleChanged(bool isOn)
    {
        UpdateAuraHighlight();
    }

    /// <summary>
    /// 更新光环高亮显示
    /// </summary>
    private void UpdateAuraHighlight()
    {
        var context = GameContext.Instance;
        if (context == null || context.Environment == null)
        {
            GridSystem.Instance.ClearHighlight();
            return;
        }

        CityEnvironment environment = context.Environment;
        List<AuraCategory> selectedCategories = GetSelectedCategories();

        if (selectedCategories.Count == 0)
        {
            // 没有选中任何Toggle，清除高亮
            GridSystem.Instance.ClearHighlight();
            return;
        }
        else if (selectedCategories.Count == 1)
        {
            // 单选情况：直接显示该类型的光环
            AuraCategory category = selectedCategories[0];
            GridSystem.Instance.ShowAuraHighlight(context, category);
            Debug.Log("单选这里有bug");
        }
        else
        {
            // 多选情况：合并显示所有选中类型的光环
            ShowMultiAuraHighlight(environment, selectedCategories);
        }
    }

    /// <summary>
    /// 获取当前选中的光环类型列表
    /// </summary>
    private List<AuraCategory> GetSelectedCategories()
    {
        List<AuraCategory> selected = new List<AuraCategory>();

        foreach (KeyValuePair<Toggle, AuraCategory> pair in toggleCategoryMap)
        {
            if (pair.Key != null && pair.Key.isOn)
            {
                selected.Add(pair.Value);
            }
        }

        return selected;
    }

    /// <summary>
    /// 显示多选光环的高亮
    /// </summary>
    private void ShowMultiAuraHighlight(CityEnvironment environment, List<AuraCategory> categories)
    {
        // 获取所有活跃的单元格
        var allActiveCells = new HashSet<Vector3Int>();
        foreach (var category in categories)
        {
            var cells = environment.EnumerateActiveCells(category);
            foreach (var cell in cells)
            {
                allActiveCells.Add(cell);
            }
        }

        // 计算每个单元格的综合值
        var level1Cells = new List<Vector3Int>();
        var level2Cells = new List<Vector3Int>();
        var level3Cells = new List<Vector3Int>();

        foreach (var cell in allActiveCells)
        {
            int maxLevel = 0;
            bool allCategoriesHaveValue = true;

            // 检查该单元格在所有选中类型中的值
            foreach (var category in categories)
            {
                int value = environment.GetValue(cell, category);
                if (value == 0)
                {
                    allCategoriesHaveValue = false;
                    break;
                }

                // 记录当前类型的等级
                int level = value >= 3 ? 3 : value;
                if (level > maxLevel)
                {
                    maxLevel = level;
                }
            }

            // 如果单元格在所有选中类型中都有值，则根据最高等级分类
            if (allCategoriesHaveValue)
            {
                if (maxLevel >= 3)
                {
                    level3Cells.Add(cell);
                }
                else if (maxLevel == 2)
                {
                    level2Cells.Add(cell);
                }
                else if (maxLevel == 1)
                {
                    level1Cells.Add(cell);
                }
            }
        }

        // 获取对应的Tile
        var tile1 = TileLib.GetTile(GameTileEnum.Tile_浅绿色);  // 等级1
        var tile2 = TileLib.GetTile(GameTileEnum.Tile_深红色);  // 等级2
        var tile3 = TileLib.GetTile(GameTileEnum.Tile_深红色);  // 等级3



        // 创建高亮配置
        List<GridSystem.HighlightSpec> highlights = new List<GridSystem.HighlightSpec>();

        if (level1Cells.Count > 0)
            highlights.Add(new GridSystem.HighlightSpec(level1Cells, tile1));

        if (level2Cells.Count > 0)
            highlights.Add(new GridSystem.HighlightSpec(level2Cells, tile2));

        if (level3Cells.Count > 0)
            highlights.Add(new GridSystem.HighlightSpec(level3Cells, tile3));

        // 设置高亮
        if (highlights.Count > 0)
        {
            GridSystem.Instance.SetHighlight(highlights.ToArray());
        }
        else
        {
            GridSystem.Instance.ClearHighlight();
        }
    }

    public void Show()
    {
        gameObject.SetActive(true);
        // 显示时更新一次高亮
        UpdateAuraHighlight();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        // 隐藏时清除高亮
        GridSystem.Instance.ClearHighlight();
    }

    /// <summary>
    /// 手动触发Toggle状态更新（用于外部调用）
    /// </summary>
    public void RefreshToggleState()
    {
        UpdateAuraHighlight();
    }

    private void OnDestroy()
    {
        // 清理监听事件
        if (toggle_治安 != null) toggle_治安.onValueChanged.RemoveListener(OnToggleChanged);
        if (toggle_医疗 != null) toggle_医疗.onValueChanged.RemoveListener(OnToggleChanged);
        if (toggle_环境 != null) toggle_环境.onValueChanged.RemoveListener(OnToggleChanged);
    }

    */
}
