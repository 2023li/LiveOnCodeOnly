using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Sirenix.OdinInspector;
using Moyo.Unity;
using System.Linq;

public class BuildingBuilder : MonoSingleton<BuildingBuilder>, IBackHandler, IMoyoEventListener<BuildingBuilder.BuildingEvent>
{
    public struct BuildingEvent
    {
        static BuildingEvent e;
        public BuildingArchetype def;

        public static void Trigger(BuildingArchetype buildingDef)
        {
            e.def = buildingDef;
            MoyoEventManager.TriggerEvent(e);
        }
    }

    public void OnMoyoEvent(BuildingEvent eventType)
    {
        this.EnterBuildMode(eventType.def);
    }

    private enum ConstructionProcess
    {
        None,
        Placing,
        AwaitingConfirmation,
    }

    [ShowInInspector, ReadOnly]
    private ConstructionProcess process;

    [ShowInInspector, ReadOnly]
    private BuildingArchetype currentBuildDef;

    [SerializeField] private TileBase green;
    [SerializeField] private TileBase red;
    [SerializeField] private TileBase crimson; // 深红：占用

    [LabelText("确认面板地址")][SerializeField] private string confirmPanelAddress = "BuildingConfirmPanel";
    [SerializeField] private UIManager.UILayer confirmPanelLayer = UIManager.UILayer.Popup;

    private BuildingConfirmPanel confirmPanelInstance;
    private bool lastPlacementValid;

    // 使用 CubeCoor 列表替代 Vector3Int
    private readonly List<CubeCoor> tempBuildingCells = new List<CubeCoor>(64);
    private CubeCoor _currentCenterCube; // 记录当前的中心点
    private Vector3 _confirmAnchorWorld;

    public short Priority { get; set; } = LOConstant.InputPriority.Priority_BuildingBuilder;

    #region 生命周期

    private void OnEnable()
    {
        this.MoyoEventStartListening();
        if (InputManager.Instance == null) return;

        InputManager.Instance.Register(this);
        InputManager.Instance.Building_OnChangeCoordinates += Handle_放置;
        InputManager.Instance.Building_OnConfirmPlacement += Handle_确认放置;
        InputManager.Instance.Building_OnConfirmConstruction += Handle_完成建造;
    }

    private void OnDisable()
    {
        this.MoyoEventStopListening();
        if (!InputManager.HasInstance) return;

        InputManager.Instance.Building_OnChangeCoordinates -= Handle_放置;
        InputManager.Instance.Building_OnConfirmPlacement -= Handle_确认放置;
        InputManager.Instance.Building_OnConfirmConstruction -= Handle_完成建造;
    }

    #endregion

    #region 外部 API

    [Button]
    public void EnterBuildMode(BuildingArchetype buildingDef)
    {
        if (buildingDef == null) return;

        currentBuildDef = buildingDef;
        lastPlacementValid = false;
        tempBuildingCells.Clear();

        // 清除所有高亮层
        GridSystem.Instance.ClearHighlight();
        HideConfirmBar();

        process = ConstructionProcess.Placing;

        // 打开建造输入
        InputManager.Instance?.EnableBuildingMap();
    }

    [Button]
    public void ExitBuildMode()
    {
        process = ConstructionProcess.None;
        currentBuildDef = null;
        tempBuildingCells.Clear();
        GridSystem.Instance.ClearHighlight();
        HideConfirmBar();
        InputManager.Instance?.DisableBuildingMap();
    }

    #endregion

    #region 事件处理

    // 鼠标移动时（来自 InputManager 的转发）
    private void Handle_放置(Vector2 screenMousePos)
    {
        if (process != ConstructionProcess.Placing || currentBuildDef == null) return;

        // 1. 获取鼠标指向的六边形坐标 (Cube)
        _currentCenterCube = GridSystem.Instance.ScreenToCube(screenMousePos);

        // 2. 计算占地 (基于半径)
        // 假设 Size 1 = 半径 0 (1格), Size 2 = 半径 1 (7格)
        int radius = Mathf.Max(0, currentBuildDef.Size - 1);
        var cells = CoordinateCalculator.CellsInRadius(_currentCenterCube, radius);

        tempBuildingCells.Clear();
        tempBuildingCells.AddRange(cells);

        // 3. 分类：占用/空闲
        var occupied = new List<CubeCoor>();
        var valid = new List<CubeCoor>();

        foreach (var cell in tempBuildingCells)
        {
            if (GridSystem.Instance.IsAllowPlacementBuilding(cell))
                valid.Add(cell);
            else
                occupied.Add(cell);
        }

        lastPlacementValid = occupied.Count == 0;

        // 4. 设置分层高亮
        // Priority 0: 有效区域 (绿色)
        // Priority 1: 冲突区域 (深红)，会覆盖在绿色之上

        if (valid.Count > 0)
        {
            GridSystem.Instance.SetHighlight(valid, green ?? GridSystem.Instance.visualizationTile, 0);
        }
        else
        {
            // 如果全是无效的，清理一下0层避免残留
            GridSystem.Instance.ClearHighlight(0);
        }

        if (occupied.Count > 0)
        {
            GridSystem.Instance.SetHighlight(occupied, crimson ?? GridSystem.Instance.visualizationTile, 1);
        }
        else
        {
            GridSystem.Instance.ClearHighlight(1);
        }
    }

    private void Handle_确认放置()
    {
        if (process != ConstructionProcess.Placing || currentBuildDef == null) return;

        if (!lastPlacementValid) return;

        // 二次校验
        foreach (var cell in tempBuildingCells)
            if (!GridSystem.Instance.IsAllowPlacementBuilding(cell)) return;

        process = ConstructionProcess.AwaitingConfirmation;

        // 计算确认条锚点 (六边形中心的世界坐标)
        _confirmAnchorWorld = GridSystem.Instance.CubeToWorld(_currentCenterCube);
        ShowConfirmBarAt(_confirmAnchorWorld);
    }

    public bool TryHandleBack()
    {
        if (process == ConstructionProcess.None) return false;
        Handle_取消();
        return true;
    }

    private void Handle_取消()
    {
        switch (process)
        {
            case ConstructionProcess.Placing:
                ExitBuildMode();
                break;

            case ConstructionProcess.AwaitingConfirmation:
                process = ConstructionProcess.Placing;
                HideConfirmBar();
                // 恢复放置状态的高亮 (这里简单全清，下一帧 Handle_放置 会自动重绘，或者手动重置状态)
                GridSystem.Instance.ClearHighlight();
                break;

            default:
                break;
        }
    }

    private void Handle_完成建造()
    {
        if (process != ConstructionProcess.AwaitingConfirmation || currentBuildDef == null) return;

        // 1. 二次占用校验
        foreach (var cell in tempBuildingCells)
        {
            if (!GridSystem.Instance.IsAllowPlacementBuilding(cell))
            {
                Debug.LogWarning("目标区域已被占用，建造失败，返回放置状态。");
                process = ConstructionProcess.Placing;
                HideConfirmBar();
                return;
            }
        }

        // 2. 标记占用
        foreach (var cell in tempBuildingCells)
        {
            GridSystem.Instance.SetOccupy(cell);
        }

        // 3. 实例化 & 初始化
        if (currentBuildDef.BuildingPrefab != null)
        {
            BuildingInstance b = Instantiate(currentBuildDef.BuildingPrefab);

            // 设置物理位置
            b.transform.SetPositionAndRotation(_confirmAnchorWorld, Quaternion.identity);

            // 初始化建筑逻辑数据 (传入 CubeCoor)
            // 在六边形网格中，CenterIsCorner 通常为 false，除非你做的是顶点放置游戏
            // CellsInRadius 逻辑下，中心一定是某个格子
            b.Initialize(currentBuildDef, tempBuildingCells.ToArray(), _currentCenterCube);
        }
        else
        {
            Debug.LogWarning("建筑预制体未设置");
        }

        Debug.Log("完成建造");
        process = ConstructionProcess.None;
        HideConfirmBar();
        GridSystem.Instance.ClearHighlight();

        InputManager.Instance?.DisableBuildingMap();
    }

    private void Handle_取消建造()
    {
        if (process != ConstructionProcess.AwaitingConfirmation) return;

        process = ConstructionProcess.Placing;
        HideConfirmBar();
        GridSystem.Instance.ClearHighlight();
    }

    #endregion

    #region 程序化建造 API

    /// <summary>
    /// 尝试在指定世界位置建造 (六边形适配版)
    /// </summary>
    public bool TryCreateBuildingAtWorld(Vector3 worldPos, BuildingArchetype buildingDef, out BuildingInstance instance, bool ignoreOccupy = false)
    {
        instance = null;

        if (buildingDef == null)
        {
            Debug.LogWarning("TryCreateBuildingAtWorld: buildingDef 为空。");
            return false;
        }

        if (buildingDef.BuildingPrefab == null)
        {
            Debug.LogWarning("TryCreateBuildingAtWorld: 建筑预制体未设置。");
            return false;
        }

        // 1. 计算中心 CubeCoor
        CubeCoor centerCube = GridSystem.Instance.WorldToCube(worldPos);
        Vector3 anchorPos = GridSystem.Instance.CubeToWorld(centerCube);

        // 2. 计算占地 (Radius)
        int radius = Mathf.Max(0, buildingDef.Size - 1);
        List<CubeCoor> cells = CoordinateCalculator.CellsInRadius(centerCube, radius);

        // 3. 占用校验
        if (!ignoreOccupy)
        {
            foreach (var c in cells)
            {
                if (!GridSystem.Instance.IsAllowPlacementBuilding(c))
                {
                    Debug.LogWarning("TryCreateBuildingAtWorld: 目标区域存在占用，放置失败。");
                    return false;
                }
            }
        }

        // 4. 标记占用
        foreach (CubeCoor c in cells)
        {
            GridSystem.Instance.SetOccupy(c);
        }

        // 5. 实例化并定位
        var go = Instantiate(buildingDef.BuildingPrefab);
        instance = go;
        go.transform.SetPositionAndRotation(anchorPos, Quaternion.identity);

        // 6. 初始化逻辑
        go.Initialize(buildingDef, cells.ToArray(), centerCube);

        return true;
    }

    #endregion

    #region UI：确认条

    private async void ShowConfirmBarAt(Vector3 anchorWorldPos)
    {
        if (UIManager.Instance == null) return;

        var args = new BuildingConfirmPanel.Args
        {
            OnConfirm = Handle_完成建造,
            OnCancel = Handle_取消建造
        };

        var address = string.IsNullOrWhiteSpace(confirmPanelAddress) ? null : confirmPanelAddress;
        var panel = await UIManager.Instance.ShowPanel<BuildingConfirmPanel>(confirmPanelLayer, address, args);
        if (panel == null) return;

        confirmPanelInstance = panel;
        confirmPanelInstance.SetWorldAnchor(anchorWorldPos);
    }

    private void HideConfirmBar()
    {
        if (UIManager.Instance == null) return;

        if (UIManager.Instance.IsPanelLoaded<BuildingConfirmPanel>())
        {
            UIManager.Instance.HidePanel<BuildingConfirmPanel>();
        }

        confirmPanelInstance = null;
    }

    #endregion
}
