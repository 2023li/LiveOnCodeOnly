using System;
using System.Collections.Generic;
using System.Linq;
using Moyo.Unity; // 假设这是你的单例库
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GridSystem : MonoSingleton<GridSystem>
{
    public enum Layer
    {
        地图边界, 水, 障碍, 道路, 特效
    }

    protected override bool IsDontDestroyOnLoad => false;

    // -------------------------------------------------
    // 组件引用
    // -------------------------------------------------
    [Title("地图组件")]
    public Grid mapGrid;
    public Tilemap tilemap_地图边界;
    public Tilemap tilemap_水;
    public Tilemap tilemap_障碍;
    public Tilemap tilemap_道路;
    public Tilemap tilemap_特效;

    [Title("可视化资源")]
    [FoldoutGroup("可视化瓦片"), LabelText("默认高亮")]
    public Tile visualizationTile;
    [FoldoutGroup("可视化瓦片"), LabelText("蓝色范围")]
    public Tile tile_Blue;
    [FoldoutGroup("可视化瓦片"), LabelText("红色危险")]
    public Tile tile_Red;
    [FoldoutGroup("可视化瓦片"), LabelText("绿色安全")]
    public Tile tile_Green;
    [FoldoutGroup("可视化瓦片"), LabelText("黄色选择")]
    public Tile tile_Yellow;

    // -------------------------------------------------
    // 数据存储
    // -------------------------------------------------
    // 核心数据源仍需以 Offset (Vector3Int) 存储，因为这是 Tilemap 的底层索引方式
    private Dictionary<Vector3Int, CellData> _cellDataDict;
    private Dictionary<Layer, Tilemap> _layerMapDict;

    protected override void Awake()
    {
        base.Awake();
        Init();
    }

    public void Init()
    {
        _layerMapDict = new Dictionary<Layer, Tilemap>()
        {
            {Layer.地图边界, tilemap_地图边界},
            {Layer.水, tilemap_水},
            {Layer.障碍, tilemap_障碍},
            {Layer.道路, tilemap_道路},
            {Layer.特效, tilemap_特效},
        };

        _cellDataDict = new Dictionary<Vector3Int, CellData>();

        // 初始化数据 - 遍历 Unity Tilemap
        BoundsInt bounds = tilemap_地图边界.cellBounds;
        foreach (var pos in bounds.allPositionsWithin)
        {
            if (tilemap_地图边界.HasTile(pos))
            {
                // 创建数据时，自动计算对应的 CubeCoor
                var cell = new CellData(pos);
                _cellDataDict.Add(pos, cell);
            }
        }

        // 初始化道路
        BoundsInt roadBounds = tilemap_道路.cellBounds;
        foreach (var pos in roadBounds.allPositionsWithin)
        {
            if (tilemap_道路.HasTile(pos) && _cellDataDict.ContainsKey(pos))
            {
                _cellDataDict[pos].roadType = RoadType.道路;
            }
        }

        InitHighlightSystem();
    }

    // -----------------------------------------------------------------------
    // 1. 坐标查询接口 (统一返回 CubeCoor)
    // -----------------------------------------------------------------------


    /// <summary>
    /// 将屏幕坐标 (如 Input.mousePosition) 转换为 六边形立方体坐标
    /// </summary>
    /// <param name="screenPos">屏幕像素坐标</param>
    /// <returns>对应的 CubeCoor</returns>
    public CubeCoor ScreenToCube(Vector3 screenPos)
    {
        // 1. 获取摄像机 (沿用你原本的 InputManager 单例)
        Camera cam = InputManager.Instance.RealCamera;

        // 2. 屏幕坐标 -> 世界坐标
        // 注意：对于 2D 游戏，ScreenToWorldPoint 通常直接转换即可
        // 如果是透视相机且地图不在 Z=0，这里可能需要射线检测 (Raycast)，但沿用你之前的逻辑如下：
        Vector3 worldPos = cam.ScreenToWorldPoint(screenPos);

        // 3. 世界坐标 -> CubeCoor (复用现有逻辑)
        return WorldToCube(worldPos);
    }

    /// <summary>
    /// 获取鼠标下的六边形坐标
    /// </summary>
    public CubeCoor GetMouseCubeCoor()
    {
        Vector3 worldPos = InputManager.Instance.RealCamera.ScreenToWorldPoint(InputManager.Instance.MousePos);
        return WorldToCube(worldPos);
    }

    /// <summary>
    /// 世界坐标转 CubeCoor
    /// </summary>
    public CubeCoor WorldToCube(Vector3 worldPos)
    {
        // 1. 使用 Unity Grid 将世界坐标转为 Offset 坐标 (Vector3Int)
        Vector3Int offset = mapGrid.WorldToCell(new Vector3(worldPos.x, worldPos.y, 0));
        // 2. 转为 CubeCoor
        return CoordinateCalculator.OffsetToCube(offset);
    }

    /// <summary>
    /// CubeCoor 转世界坐标中心点
    /// </summary>
    public Vector3 CubeToWorld(CubeCoor cube)
    {
        Vector3Int offset = CoordinateCalculator.CubeToOffset(cube);
        return mapGrid.GetCellCenterWorld(offset);
    }

    // -----------------------------------------------------------------------
    // 2. 逻辑判定接口 (接受 CubeCoor)
    // -----------------------------------------------------------------------

    public bool IsAllowPlacementBuilding(CubeCoor cube)
    {
        Vector3Int offset = CoordinateCalculator.CubeToOffset(cube);

        // 必须在地图范围内
        if (!_cellDataDict.ContainsKey(offset)) return false;

        bool hasObstacle = _layerMapDict[Layer.障碍].HasTile(offset);
        bool hasRoad = _layerMapDict[Layer.道路].HasTile(offset);

        return !hasObstacle && !hasRoad;
    }

    /// <summary>
    /// 获取移动阻力 (供 CoordinateCalculator 调用)
    /// </summary>
    public float GetMobileResistance(CubeCoor cube)
    {
        Vector3Int offset = CoordinateCalculator.CubeToOffset(cube);
        if (_cellDataDict.TryGetValue(offset, out CellData data))
        {
            return data.GetMobileResistance();
        }
        return -1f; // 无效区域/不可通行
    }

    public void SetOccupy(CubeCoor cube)
    {
        Vector3Int offset = CoordinateCalculator.CubeToOffset(cube);
        _layerMapDict[Layer.障碍].SetTile(offset, visualizationTile);
    }
    // -----------------------------------------------------------------------
    // 3. 多层 Tilemap 高亮系统 (Int Priority Based)
    // -----------------------------------------------------------------------

    // 对象池：存储已经生成的 Tilemap 实例 (Key: Priority)
    private Dictionary<int, Tilemap> _layerTilemapInstances;

    // 基础 SortingOrder，所有高亮层都会在此基础上叠加
    private int _baseSortingOrder;

    private void InitHighlightSystem()
    {
        _layerTilemapInstances = new Dictionary<int, Tilemap>();

        // 1. 获取模板的 SortingOrder 作为基准
        var renderer = tilemap_特效.GetComponent<TilemapRenderer>();
        _baseSortingOrder = renderer != null ? renderer.sortingOrder : 0;

        // 2. 清空并禁用模板
        tilemap_特效.ClearAllTiles();
        tilemap_特效.gameObject.SetActive(false);
    }

    /// <summary>
    /// 获取或创建指定优先级的 Tilemap
    /// </summary>
    private Tilemap GetOrCreateTilemap(int priority)
    {
        // 1. 如果池子里有，直接返回 (复用)
        if (_layerTilemapInstances.TryGetValue(priority, out Tilemap existingTilemap))
        {
            if (!existingTilemap.gameObject.activeSelf) existingTilemap.gameObject.SetActive(true);
            return existingTilemap;
        }

        // 2. 如果没有，基于模板 Instantiate 一个新的
        GameObject newObj = Instantiate(tilemap_特效.gameObject, tilemap_特效.transform.parent);
        newObj.name = $"Tilemap_Highlight_Priority_{priority}";
        newObj.SetActive(true);

        Tilemap newTilemap = newObj.GetComponent<Tilemap>();
        TilemapRenderer newRenderer = newObj.GetComponent<TilemapRenderer>();

        // 3. 设置排序：BaseOrder + 1 (避免跟原有冲突) + Priority
        // Priority 越高，SortingOrder 越大，显示在越上层
        if (newRenderer != null)
        {
            newRenderer.sortingOrder = _baseSortingOrder + 1 + priority;
        }

        // 4. 存入池子
        _layerTilemapInstances[priority] = newTilemap;
        newTilemap.ClearAllTiles();

        return newTilemap;
    }

    /// <summary>
    /// 设置高亮
    /// </summary>
    /// <param name="coords">需要高亮的格子</param>
    /// <param name="tile">瓦片资源，不传使用默认</param>
    /// <param name="priority">优先级，默认为0，越高越优先</param>
    public void SetHighlight(IEnumerable<CubeCoor> coords, TileBase tile = null, int priority = 0)
    {
        // 获取对应的物理 Tilemap
        Tilemap targetMap = GetOrCreateTilemap(priority);

        // 先清空该层旧数据 (覆盖模式)
        targetMap.ClearAllTiles();

        if (coords == null) return;

        TileBase tileToUse = tile ?? visualizationTile;

        // 收集有效格子进行批量绘制
        var validCells = new List<Vector3Int>();
        var tiles = new List<TileBase>();

        foreach (var cube in coords)
        {
            Vector3Int offset = CoordinateCalculator.CubeToOffset(cube);
            // 依然只高亮有效区域
            if (_cellDataDict.ContainsKey(offset))
            {
                validCells.Add(offset);
                tiles.Add(tileToUse);
            }
        }

        if (validCells.Count > 0)
        {
            targetMap.SetTiles(validCells.ToArray(), tiles.ToArray());
        }
    }

    /// <summary>
    /// 清除高亮
    /// </summary>
    /// <param name="priorities">
    /// 传入需要清除的优先级数组。
    /// 如果不传参数 (空)，则清除所有层的高亮。
    /// </param>
    public void ClearHighlight(params int[] priorities)
    {
        // 模式1：清除所有
        if (priorities == null || priorities.Length == 0)
        {
            foreach (var kv in _layerTilemapInstances)
            {
                kv.Value.ClearAllTiles();
            }
        }
        // 模式2：清除指定层
        else
        {
            foreach (int p in priorities)
            {
                if (_layerTilemapInstances.TryGetValue(p, out Tilemap map))
                {
                    map.ClearAllTiles();
                }
            }
        }
    }
}

// -------------------------------------------------
// 辅助数据类
// -------------------------------------------------

public enum RoadType { 无道路, 道路 }

[Serializable]
public class CellData
{
    public Vector3Int OffsetCoor;
    public CubeCoor CubeCoor; // 缓存对应的 CubeCoor
    public RoadType roadType = RoadType.无道路;

    public CellData(Vector3Int offset)
    {
        this.OffsetCoor = offset;
        this.CubeCoor = CoordinateCalculator.OffsetToCube(offset);
    }

    public float GetMobileResistance()
    {
        switch (roadType)
        {
            case RoadType.无道路: return 1f;
            case RoadType.道路: return 0.5f;
            default: return 1f;
        }
    }
}
