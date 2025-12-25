using System;
using UnityEngine;
using UnityEngine.UI;
using Lean.Pool; // LeanPool
using Moyo.Unity;
using Sirenix.OdinInspector;



public class UIItem_BuildingSelection : MonoBehaviour
{
    [AutoBind] public GameObject panel_选择建筑类型;
    [AutoBind] public GameObject panel_选择建筑;

    [Header("UI 引用")]
    [AutoBind,LabelText("建筑分类容器")] public RectTransform Content;                 // 分类按钮容器
    [AutoBind,LabelText("建筑选择容器")] public RectTransform BuildBuildingBtnContent; // 建筑按钮容器

    [AutoBind] public Button btn_Hide;


    [Header("预制体")]
    [LabelText("分类按钮预制体")]
    public IconTextButton classBtnPrefab;      // 用于“选择建筑分类”的按钮

    [LabelText("建筑按钮预制体")]
    public IconTextButton buildingBtnPrefab;   // 用于“选择建

    [Header("上下文引用")]
    [LabelText("游戏上下文")]
    [SerializeField] private GameContext _gameContext;

    private IGameContext _cachedContext;


    private void Reset()
    {
        this.AutoBindFields();
    }

    private void Awake()
    {
        if (btn_Hide != null)
        {
            btn_Hide.onClick.RemoveAllListeners();
            btn_Hide.onClick.AddListener(Hide);
        }

      
    }

    /// <summary>
    /// 展示分类按钮：为每个 BuildingClassify 枚举项生成一个按钮
    /// </summary>
    public void Show()
    {
        if (!ValidateRefs()) return;

        // 【修改点 1】：同时显示两个面板（因为现在是上下布局，不是切换显示）
        if (panel_选择建筑类型) panel_选择建筑类型.SetActive(true);
        if (panel_选择建筑) panel_选择建筑.SetActive(true); // 改为 true

        // 清空旧数据
        ClearBuildingClassButtons();
        ClearBuildingButtons(); // 初始化时先清空下方

        // 生成分类按钮
        bool isFirst = true;
        foreach (BuildingClassify classify in Enum.GetValues(typeof(BuildingClassify)))
        {
            CreateBuildingClassButton(classify);

            // 【修改点 2】：打开界面时，默认加载第一个分类的数据
            if (isFirst)
            {
                ShowBuilingClasss(classify);
                isFirst = false;
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(Content);
        gameObject.SetActive(true);
    }

    private void CreateBuildingClassButton(BuildingClassify classify)
    {
        if (classBtnPrefab == null)
        {
            Debug.LogError("[UIItem_BuildingSelection] classBtnPrefab 未赋值，无法创建分类按钮。");
            return;
        }

        IconTextButton item = LeanPool.Spawn(classBtnPrefab, Content);

        var rt = (RectTransform)item.transform;
        rt.localScale = Vector3.one;
        rt.anchoredPosition3D = Vector3.zero;
        item.gameObject.name = $"Btn_Class_{classify}";

        item.SetContent(classify.ToString(), null);

        var captured = classify;
        item.SetOnClick(() => ShowBuilingClasss(captured));
    }

    private void CreateBuildingButton(BuildingArchetype buildingDef, bool canBuild)
    {
        if (buildingBtnPrefab == null)
        {
            Debug.LogError("[UIItem_BuildingSelection] buildingBtnPrefab 未赋值，无法创建建筑按钮。");
            return;
        }

        IconTextButton item = LeanPool.Spawn(buildingBtnPrefab, BuildBuildingBtnContent);

        var rt = (RectTransform)item.transform;
        rt.localScale = Vector3.one;
        rt.anchoredPosition3D = Vector3.zero;
        item.gameObject.name = $"Btn_Build_{buildingDef.Id}";

        item.SetContent(buildingDef.DisplayName, buildingDef.BuildingIcon);

        BuildingArchetype captured = buildingDef;

        if (canBuild)
        {
            item.SetOnClick(() => BuildBuilding(captured));
        }
        else
        {
            item.SetOnClick(null);
        }

        item.SetInteractable(canBuild);
    }

    private void ClearBuildingClassButtons()
    {
        if (Content == null) return;
        for (int i = Content.childCount - 1; i >= 0; i--)
        {
            var child = Content.GetChild(i);
            if (child != null) LeanPool.Despawn(child);
        }
    }

    private void ClearBuildingButtons()
    {
        if (BuildBuildingBtnContent == null) return;
        for (int i = BuildBuildingBtnContent.childCount - 1; i >= 0; i--)
        {
            var child = BuildBuildingBtnContent.GetChild(i);
            if (child != null) LeanPool.Despawn(child);
        }
    }

    /// <summary>
    /// 点击分类按钮 → 在建筑容器中显示该分类的所有建筑，并切换到“选择建筑”面板
    /// </summary>
    public void ShowBuilingClasss(BuildingClassify classify)
    {
        if (ResourceRouting.Instance == null)
        {
            Debug.LogError("[UIItem_BuildingSelection] ResourceRouting.Instance 为空。");
            return;
        }

        IGameContext context = GetContext();
        if (context == null)
        {
            Debug.LogError("[UIItem_BuildingSelection] 未找到有效的 GameContext，无法评估建造条件。");
            return;
        }

        var allBuilding = ResourceRouting.Instance.GetClassAllBuildingDef(classify);
        if (allBuilding == null)
        {
            Debug.LogWarning($"[UIItem_BuildingSelection] 未找到分类 {classify} 的建筑定义。");
            return;
        }

        ClearBuildingButtons();

        foreach (var def in allBuilding)
        {
            if (def == null) continue;

            if (!ConditionUtility.TryEvaluateConditions(def.ShowInBuildPanel, null, context, out var reason))
            {
                Debug.LogWarning($"[UIItem_BuildingSelection] 跳过建筑 {def.DisplayName}({def.Id})：{reason}");
                continue;
            }
            bool canBuild = ConditionUtility.TryEvaluateConditions(def.AllowConstruction, null, context, out var buildReason);
            if (!canBuild)
            {
                var message = string.IsNullOrWhiteSpace(buildReason)
                    ? "条件未通过"
                    : buildReason;
                Debug.LogWarning($"[UIItem_BuildingSelection] 建筑 {def.DisplayName}({def.Id}) 当前不可建造：{message}");
            }
            CreateBuildingButton(def, canBuild);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(BuildBuildingBtnContent);

     

        Debug.Log($"[UIItem_BuildingSelection] 分类 {classify} 的建筑列表已生成。");
    }

    public void BuildBuilding(BuildingArchetype def)
    {       
        // TODO: 你的建造逻辑

        BuildingBuilder.BuildingEvent.Trigger(def);
    
    }


    public void Hide()
    {
        ClearBuildingClassButtons();
        ClearBuildingButtons();

        if (panel_选择建筑类型) panel_选择建筑类型.SetActive(false);
        if (panel_选择建筑) panel_选择建筑.SetActive(false);

        gameObject.SetActive(false);
    }

    private bool ValidateRefs()
    {
        bool ok = true;
        if (panel_选择建筑类型 == null) { Debug.LogWarning("[UIItem_BuildingSelection] panel_选择建筑类型 未绑定。"); ok = false; }
        if (panel_选择建筑 == null) { Debug.LogWarning("[UIItem_BuildingSelection] panel_选择建筑 未绑定。"); ok = false; }
        if (Content == null) { Debug.LogWarning("[UIItem_BuildingSelection] Content（分类容器）未绑定。"); ok = false; }
        if (BuildBuildingBtnContent == null) { Debug.LogWarning("[UIItem_BuildingSelection] BuildBuildingBtnContent（建筑容器）未绑定。"); ok = false; }

        if (classBtnPrefab == null) { Debug.LogWarning("[UIItem_BuildingSelection] classBtnPrefab（分类按钮预制体）未赋值。"); ok = false; }
        if (buildingBtnPrefab == null) { Debug.LogWarning("[UIItem_BuildingSelection] buildingBtnPrefab（建筑按钮预制体）未赋值。"); ok = false; }

        return ok;
    }

    private IGameContext GetContext()
    {
        if (_cachedContext != null)
        {
            return _cachedContext;
        }

        if (_gameContext != null)
        {
            _cachedContext = _gameContext;
            return _cachedContext;
        }

        _cachedContext = GameContext.Instance;
        return _cachedContext;
    }

}
