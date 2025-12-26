using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using Moyo.Unity;
using System;
using UnityEngine.Rendering;

/// <summary>
/// UI 节点连线的全局管理器。
/// 适用于 2D 项目中存在多个 World-Space Canvas 的情况。
/// 
/// 核心职责：
/// 1. 统一处理跨 Canvas 的射线检测（EventSystem.RaycastAll）。  
/// 2. 管理连线的创建 / 延长 / 删除。  
/// 3. 在拖拽过程中实时更新临时尾端位置，结束时固化拓扑。  
/// 
/// 规则要点（由当前逻辑隐含）：
/// - 只有 start 节点允许“新建”一条线段链（可多条）。  
/// - 非 start 节点只能“延长”自己作为尾节点的已有线。  
/// - 若尾节点存在多条线，则按“鼠标离哪条线更近”来选中延长目标，并加入误触阈值。  
/// - 新建时若 AB 段已存在则不重复创建。  
/// - start 节点拖空（没连到任何目标）视为撤销：删除该 start 最近创建的那条线。  
/// </summary>
public class ConnectionManager : MonoSingleton<ConnectionManager>
{
    #region 画线

    // 1. Define the eventArg (Passing the start node allows listeners to know WHERE it started)
    public event Action<UINode> OnLineDragStart;

    // Optional: Useful to know when it ends to stop effects
    public event Action<UINode> OnLineDragEnd;

    // ... existing events ...

    protected override bool IsDontDestroyOnLoad => false;
   

    [Header("Line Settings")]
    [LabelText("线宽")]
    [MinValue(0.0001f)]
    public float lineWidth = 0.02f;

    /// <summary>
    /// 当一个节点作为尾节点挂了多条线时，用于“挑中哪条线进行延长”。
    /// 计算方式： pickRadius = lineWidth * lineFalseTouchDistanceThreshold  
    /// 值越大，越容易选中远处的线；值越小，越不容易误触。
    /// </summary>
    [SerializeField, LabelText("操作线误触阈值(倍数)")]
    [MinValue(0f)]
    private float lineFalseTouchDistanceThreshold = 10f;

    /// <summary>
    /// 当前所有“已固化”的连线集合。
    /// </summary>
    private readonly List<ConnectionLine> _lines = new List<ConnectionLine>();
    public IReadOnlyList<ConnectionLine> Lines => _lines;

    /// <summary>
    /// 正在拖拽中的那条线（可能是新建，也可能是延长）。
    /// </summary>
    private ConnectionLine _activeLine;

    /// <summary>
    /// 当前拖拽模式。
    /// </summary>
    private DragMode _dragMode = DragMode.None;

    /// <summary>
    /// 连线创建计数，用于决定“同一 start 下最新创建的线”。
    /// </summary>
    private int _creationCounter = 0;

    // ====== 每次拖拽临时缓存 ======
    /// <summary>
    /// 拖拽世界坐标换算用的平面（以拖拽起点 Canvas 为基准）。
    /// </summary>
    private Plane _dragPlane;

    /// <summary>
    /// 本次拖拽使用的事件相机（优先 pressEventCamera，否则 main）。 
    /// </summary>
    private Camera _eventCam;

    /// <summary>
    /// 拖拽模式：  
    /// None：未拖拽  
    /// NewFromStart：从 start 新建一条线  
    /// ExtendExisting：延长一条已存在的线  
    /// </summary>
    private enum DragMode
    {
        None,
        NewFromStart,
        ExtendExisting
    }

    protected override void Awake()
    {
        base.Awake();

    }
    private void Start()
    {
       OnLineDragStart += HandleDragStart;
         OnLineDragEnd += HandleDragEnd;
        
    }
    protected override void OnDestroy()
    {
        base .OnDestroy();
        OnLineDragStart -= HandleDragStart;
        OnLineDragEnd -= HandleDragEnd;
    }

    [SerializeField]
    private ScopeCheckMode checkMode = ScopeCheckMode.AnyCellOverlap;

    private void HandleDragStart(UINode startNode)
    {
        // 1. 安全检查
        if (startNode == null || startNode.SelfBuilding == null) return;

        // 2. 获取起始节点的转运半径
        float radius = startNode.SelfBuilding.RO_TransportRadius;

        // 获取所有可达的格子列表
        List<CubeCoor> reachableCells = CoordinateCalculator.GetReachableCellsByMovePower(startNode.SelfBuilding, radius);

        // 【关键优化Step 1】：设置高亮
        GridSystem.Instance.SetHighlight(reachableCells,TileLib.GetTile(GameTileEnum.Tile_边框),1);

        // 【关键优化Step 2】：将列表转为 HashSet，实现 O(1) 快速查询
        HashSet<CubeCoor> reachableSet = new HashSet<CubeCoor>(reachableCells);

        // 3. 遍历所有活跃的 UINode
        foreach (var targetNode in UINode.ActiveNodes)
        {
            if (targetNode == null) continue;

            // 总是保持自己是可交互的
            if (targetNode == startNode)
            {
                targetNode.Interactive = true;
                continue;
            }

            if (targetNode.SelfBuilding == null)
            {
                targetNode.Interactive = false;
                continue;
            }

            // 4. 【修改处】不再进行寻路，而是直接检查 targetNode 是否在 reachableSet 中
            bool isReachable = false;

            // 根据你的 CheckMode 选择判定方式
            // 假设你要的是 AnyCellOverlap (只要沾边就算) -> 推荐这个，体验最好
            if (checkMode == ScopeCheckMode.AnyCellOverlap)
            {
                // 只要建筑占用的任何一个格子在可达集合里，就是 true
                foreach (var cell in targetNode.SelfBuilding.Self_CurrentOccupy)
                {
                    if (reachableSet.Contains(cell))
                    {
                        isReachable = true;
                        break;
                    }
                }
            }
            else // CenterOnly
            {
                // 只有中心点在可达集合里才算
                CubeCoor center = targetNode.SelfBuilding.Self_CurrentCenterInGrid;
                if (reachableSet.Contains(center))
                {
                    isReachable = true;
                }
            }

            // 5. 设置状态
            targetNode.Interactive = isReachable;
        }
    }
    /// <summary>
    /// 结束拖拽：还原所有节点为可交互状态
    /// </summary>
    private void HandleDragEnd(UINode startNode)
    {


        GridSystem.Instance.ClearHighlight();
        // 遍历所有节点，统一恢复 Interactive = true
        foreach (var node in UINode.ActiveNodes)
        {
            if (node != null)
            {
                node.Interactive = true;
            }
        }
    }
    /// <summary>
    /// 在运行时创建一份 ConnectionLine 模板对象。  
    /// 通过代码补齐 LineRenderer 的默认设置，保证表现一致。
    /// </summary>
    private ConnectionLine CreateLineInstance()
    {
        var go = new GameObject("ConnectionLine");
        go.transform.SetParent(transform, false);

        var line = go.AddComponent<ConnectionLine>();
        var lr = go.GetComponent<LineRenderer>();

        // 这里把你原来在 CreateLineTemplate 里填的配置都搬过来
        lr.useWorldSpace = true;
        lr.numCornerVertices = 4;
        lr.numCapVertices = 4;
        lr.textureMode = LineTextureMode.Tile;
        lr.alignment = LineAlignment.View;
        lr.sortingLayerName = "UI";
        lr.sortingOrder = 10;

        return line;
    }



    
    #region 拖拽 API（由 UINode 调用）

    /// <summary>
    /// 开始拖拽：  
    /// - 根据起点确定拖拽平面和相机。  
    /// - 决定是新建连线还是延长已有连线。
    /// </summary>
    public void StartDrag(UINode origin, PointerEventData eventData)
    {
        _activeLine = null;
        _dragMode = DragMode.None;

        // 本次拖拽使用的事件相机
        _eventCam = eventData.pressEventCamera != null ? eventData.pressEventCamera : Camera.main;

        // 以 origin 所在 Canvas 的朝向定义拖拽平面
        // 2D World-Space 通常为 XY 平面，normal 指向 Canvas.forward（一般为 +Z）
        var originCanvas = origin.GetComponentInParent<Canvas>();
        if (originCanvas != null)
            _dragPlane = new Plane(originCanvas.transform.forward, originCanvas.transform.position);
        else
            _dragPlane = new Plane(Vector3.forward, origin.transform.position);

        if (origin.isStart)
        {
            // start 节点：允许无上限新建一条线
            _activeLine = CreateLineInstance();
            _activeLine.Init(origin, origin.CurrentActiveSupplyDef, lineWidth, ++_creationCounter);
            _dragMode = DragMode.NewFromStart;
        }
        else
        {
            // 非 start：只能延长自己作为尾节点的已有线
            List<ConnectionLine> tailLines = _lines.Where(l => l.LastNode == origin).ToList();

            if (tailLines.Count == 1)
            {
                // 只有一条候选线时，直接选中
                _activeLine = tailLines[0];
                _dragMode = DragMode.ExtendExisting;
            }
            else if (tailLines.Count > 1)
            {
                // 多条候选线时：选离鼠标最近的那条
                Vector3 mouseWorld = GetDragWorld(eventData);

                float best = float.MaxValue;
                ConnectionLine bestLine = null;

                foreach (var l in tailLines)
                {
                    float d2 = MinDistanceSqToPolyline(mouseWorld, l.CachedPoints);
                    if (d2 < best)
                    {
                        best = d2;
                        bestLine = l;
                    }
                }

                // 加一个误触阈值，避免鼠标离线太远仍被选中
                float pickRadius = lineWidth * lineFalseTouchDistanceThreshold;
                if (bestLine != null && best <= pickRadius * pickRadius)
                {
                    _activeLine = bestLine;
                    _dragMode = DragMode.ExtendExisting;
                }
            }
        }

        // 初始化临时尾端，用于拖拽时显示“跟随鼠标的尾巴”
        if (_activeLine != null)
        {
            _activeLine.SetTempTail(GetDragWorld(eventData));
            OnLineDragStart?.Invoke(origin);
        }

      
    }

    /// <summary>
    /// 拖拽中：实时更新临时尾端位置。
    /// </summary>
    public void Drag(PointerEventData eventData)
    {
        if (_activeLine == null) return;
        _activeLine.SetTempTail(GetDragWorld(eventData));
    }

    /// <summary>
    /// 结束拖拽：  
    /// - 计算鼠标下的目标节点。  
    /// - 根据拖拽模式固化或回滚连线。  
    /// - 最后刷新所有节点槽位与线形。
    /// </summary>
    public void EndDrag(UINode origin, PointerEventData eventData)
    {
        if (_activeLine == null) return;

        UINode target = GetNodeUnderPointer(eventData, origin);

        if (_dragMode == DragMode.NewFromStart)
        {
            if (target != null)
            {
                // 当前正在画线的物资类型
                var supply = _activeLine.SelfSupply;

                // 只在 A-B 之间已经存在【同一 supply】的连线时，视为重复，不允许再连
                if (FindLineWithSegment(origin, target, supply) != null)
                {
                    _activeLine.DetachAll();
                    Destroy(_activeLine.gameObject);
                }
                else
                {
                    _activeLine.ClearTempTail();
                    _activeLine.AppendNode(target);
                    _lines.Add(_activeLine);
                }
            }
            else
            {
                _activeLine.DetachAll();
                DeleteLastLineFromStart(origin);
                Destroy(_activeLine.gameObject);
            }
        }
        else if (_dragMode == DragMode.ExtendExisting)
        {
            if (target != null && target != origin && target.Interactive)
            {
                // 防止形成环或重复节点
                if (!_activeLine.nodes.Contains(target))
                {
                    _activeLine.ClearTempTail();
                    _activeLine.AppendNode(target);
                }
                else
                {
                    // 命中已存在节点：仅清掉临时尾，不做修改
                    _activeLine.ClearTempTail();
                }
            }
            else
            {
                // 未命中任何节点：保持原线不变
                _activeLine.ClearTempTail();
            }
        }

        // 3. Trigger the end eventArg before clearing _activeLine
        if (_activeLine != null)
        {
            OnLineDragEnd?.Invoke(origin);
        }

        // 重置拖拽状态
        _activeLine = null;
        _dragMode = DragMode.None;

        // 刷新所有节点的“出入口槽位”与所有线的坐标
        RecalculateAllLanes();
        RebuildAllLines();
    }


    #endregion

    #region 拓扑操作（删除/断开/重建）

    /// <summary>
    /// 删除整条线：  
    /// 1) 从关联节点解绑  
    /// 2) 从集合移除  
    /// 3) 销毁对象  
    /// 4) 全局重算/重建
    /// </summary>
    public void DeleteLine(ConnectionLine line)
    {
        if (line == null) return;

        line.DetachAll();
        _lines.Remove(line);
        Destroy(line.gameObject);

        RecalculateAllLanes();
        RebuildAllLines();
    }

    /// <summary>
    /// 规则：若线上的任意一段 AB 被断开，则整条线移除。
    /// </summary>
    public void DisconnectSegment(UINode a, UINode b)
    {
        var line = FindLineWithSegment(a, b);
        if (line != null)
            DeleteLine(line);
    }

    /// <summary>
    /// 删除某 start 节点最新创建的那条线（用于 start 拖空撤销）。
    /// </summary>
    private void DeleteLastLineFromStart(UINode start)
    {
        var candidates = _lines.Where(l => l.startNode == start)
                               .OrderByDescending(l => l.CreationOrder)
                               .ToList();
        if (candidates.Count > 0)
            DeleteLine(candidates[0]);
    }

    /// <summary>
    /// 查找包含线段 AB 的连线（用于判重/断开）。
    /// </summary>
    private ConnectionLine FindLineWithSegment(UINode a, UINode b)
    {
        return _lines.FirstOrDefault(l => l.ContainsSegment(a, b));
    }


    /// <summary>
    /// 查找包含线段 AB 且物资为指定 supply 的连线。
    /// </summary>
    private ConnectionLine FindLineWithSegment(UINode a, UINode b, SupplyDef supply)
    {
        return _lines.FirstOrDefault(l =>
            l.ContainsSegment(a, b) &&
            l.SelfSupply == supply    // 同一物资才算重复
        );
    }


    /// <summary>
    /// 收集所有参与连线的节点，重算它们的槽位/车道信息。
    /// </summary>
    private void RecalculateAllLanes()
    {
        var allNodes = new HashSet<UINode>();
        foreach (var line in _lines)
            foreach (var n in line.nodes)
                allNodes.Add(n);

        foreach (var n in allNodes)
            n.RecalculateLanes();
    }

    /// <summary>
    /// 重建所有线的渲染点（比如节点移动后需要更新曲线/折线）。
    /// </summary>
    private void RebuildAllLines()
    {
        foreach (var l in _lines)
            l.RebuildPositions();
    }

    #endregion

    #region 射线检测 / 拖拽世界坐标

    /// <summary>
    /// 跨 Canvas 的 UI 射线检测。  
    /// 要求：  
    /// - 每个 World-Space Canvas 挂 GraphicRaycaster  
    /// - 节点上有可被 Raycast 的 Graphic  
    /// </summary>
    private UINode GetNodeUnderPointer(PointerEventData eventData, UINode exclude)
    {
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (var r in results)
        {
            var node = r.gameObject.GetComponentInParent<UINode>();
            if (node != null && node != exclude)
                return node;
        }
        return null;
    }

    /// <summary>
    /// 获取拖拽时的世界坐标：  
    /// 1) 若 UI Raycast 命中且提供 worldPosition，则直接用它（跨 Canvas 更准）。  
    /// 2) 否则用屏幕射线与拖拽平面求交点。  
    /// </summary>
    private Vector3 GetDragWorld(PointerEventData eventData)
    {
        // 优先使用 UI 命中提供的 worldPosition
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        if (results.Count > 0)
        {
            var wp = results[0].worldPosition;
            if (wp != Vector3.zero)
                return wp;
        }

        if (_eventCam == null) _eventCam = Camera.main;

        Ray ray = _eventCam.ScreenPointToRay(eventData.position);
        if (_dragPlane.Raycast(ray, out float enter))
            return ray.GetPoint(enter);

        // 理论上不会走到这里，兜底返回射线起点
        return ray.origin;
    }

    #endregion

    /// <summary>
    /// 从“线的尾端拖拽手柄”开始拖拽，强制进入延长模式。  
    /// 用于你在 ConnectionLine 上做的可视化拖拽点。
    /// </summary>
    public void StartDragHandle(ConnectionLine line, UINode ownerNode, PointerEventData eventData)
    {
        _activeLine = null;
        _dragMode = DragMode.None;

        _eventCam = eventData.pressEventCamera != null ? eventData.pressEventCamera : Camera.main;

        // 拖拽平面仍用 ownerNode 所在 Canvas
        var originCanvas = ownerNode.GetComponentInParent<Canvas>();
        if (originCanvas != null)
            _dragPlane = new Plane(originCanvas.transform.forward, originCanvas.transform.position);
        else
            _dragPlane = new Plane(Vector3.forward, ownerNode.transform.position);

        // 强制指定这条线为当前延长线
        _activeLine = line;
        _dragMode = DragMode.ExtendExisting;

        if (_activeLine != null)
        {
            _activeLine.SetTempTail(GetDragWorld(eventData));
            OnLineDragStart?.Invoke(ownerNode);
        }
    }

    /// <summary>
    /// 计算点 p 到一条折线 poly 的最小距离平方。  
    /// 用于“鼠标离哪条线最近”的判断。
    /// </summary>
    private float MinDistanceSqToPolyline(Vector3 p, IReadOnlyList<Vector3> poly)
    {
        float best = float.MaxValue;
        for (int i = 0; i < poly.Count - 1; i++)
        {
            best = Mathf.Min(best, DistanceSqPointSegment(p, poly[i], poly[i + 1]));
        }
        return best;
    }

    /// <summary>
    /// 点到线段的距离平方（避免开方提升性能）。
    /// </summary>
    private float DistanceSqPointSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float t = Vector3.Dot(p - a, ab) / ab.sqrMagnitude;
        t = Mathf.Clamp01(t);
        Vector3 proj = a + t * ab;
        return (p - proj).sqrMagnitude;
    }
    #endregion

    #region 与建筑系统集成

   
    //显示 //这个事件需要显示所有的线 显示所有的节点（起始节点添加额外的光环）
    public event Action OnShowTransfer;
    //选择一个资源
    public event Action<SupplyDef> OnSelectSupply;
    public event Action OnHideTransfer;


    public void EnterEditorMode()
    {
        OnShowTransfer?.Invoke();
    }

    public void OnSelect(SupplyDef def)
    {
        OnSelectSupply?.Invoke(def);
    }

    public void ExitEditorMode()
    {
        OnHideTransfer?.Invoke();
    }

    #endregion



    // 在 ConnectionManager.cs 中添加

    #region 持久化系统 (Save & Load)

    /// <summary>
    /// 获取所有连线的存档数据
    /// </summary>
    public ConnectionManagerSaveData Save()
    {
        var saveData = new ConnectionManagerSaveData();
        foreach (var line in _lines)
        {
            if (line != null)
            {
                saveData.AllLines.Add(line.GetSaveData());
            }
        }
        return saveData;
    }

    /// <summary>
    /// 加载存档：清空当前所有线并根据数据重建
    /// </summary>
    public void Load(ConnectionManagerSaveData saveData)
    {
        // 1. 清理现有场景
        ClearAllLines();

        if (saveData == null || saveData.AllLines == null) return;

        // 2. 按保存时的顺序排序，确保渲染层级正确
        saveData.AllLines.Sort((a, b) => a.CreationOrder.CompareTo(b.CreationOrder));

        // 3. 逐条还原
        foreach (var lineData in saveData.AllLines)
        {
            RestoreSingleLine(lineData);
        }

        // 4. 全局刷新
        RecalculateAllLanes();
        RebuildAllLines();
    }

    /// <summary>
    /// 清空当前所有连线
    /// </summary>
    public void ClearAllLines()
    {
        // 倒序删除避免集合修改错误
        for (int i = _lines.Count - 1; i >= 0; i--)
        {
            var line = _lines[i];
            if (line != null)
            {
                line.DetachAll();
                Destroy(line.gameObject);
            }
        }
        _lines.Clear();
        _creationCounter = 0; // 重置计数器
    }

    /// <summary>
    /// 核心还原方法：根据数据重建一条线
    /// </summary>
    /// <param name="data">单条线的存档数据</param>
    /// <param name="supplyLookup">回调函数：通过ID查找SupplyDef配置</param>
    private void RestoreSingleLine(ConnectionLineSaveData data)
    {
        if (data == null || data.NodePathCoordinates == null || data.NodePathCoordinates.Count < 2)
            return;

        // A. 查找物资配置
        SupplyDef supply = SupplyDef.GetSupplyDef(data.SupplyID);
        if (supply == null)
        {
            Debug.LogError($"[ConnectionManager] Load failed: Cannot find SupplyDef with ID {data.SupplyID}");
            return;
        }

        // B. 查找起始节点 (Start Node)
        UINode startNode = FindNodeByCoordinate(data.NodePathCoordinates[0]);
        if (startNode == null)
        {
            // 如果起点建筑被拆了，这条线就无法恢复
            return;
        }

        // C. 创建线条实例
        ConnectionLine newLine = CreateLineInstance();

        // 恢复计数器，防止新创建的线序号冲突
        if (data.CreationOrder > _creationCounter) _creationCounter = data.CreationOrder;

        // D. 初始化起点
        newLine.Init(startNode, supply, lineWidth, data.CreationOrder);

        // E. 追加后续节点
        for (int i = 1; i < data.NodePathCoordinates.Count; i++)
        {
            UINode nextNode = FindNodeByCoordinate(data.NodePathCoordinates[i]);

            if (nextNode != null)
            {
                newLine.AppendNode(nextNode);
            }
            else
            {
                // 如果中间某个节点找不到了（比如建筑被销毁），
                // 策略：
                // 1. 直接中断（当前的逻辑）：线断了
                // 2. 尝试连下一个（如果逻辑允许跳过）
                // 这里采用中断策略，避免数据错乱
                Debug.LogWarning($"[ConnectionManager] Line broken at index {i}, node missing.");
                break;
            }
        }

        // F. 加入管理器列表
        _lines.Add(newLine);
    }

    /// <summary>
    /// 辅助方法：通过坐标在 ActiveNodes 中查找对应的 UINode
    /// </summary>
    private UINode FindNodeByCoordinate(CubeCoor coor)
    {
        // 假设 CubeCoor 重写了 Equals 或 == 
        foreach (var node in UINode.ActiveNodes)
        {
            if (node != null && node.SelfBuilding != null)
            {
                if (node.SelfBuilding.Self_CurrentCenterInGrid.Equals(coor))
                {
                    return node;
                }
            }
        }
        return null;
    }

    #endregion



}


// 对应整个管理器的存档数据
[Serializable]
public class ConnectionManagerSaveData
{
    public List<ConnectionLineSaveData> AllLines = new List<ConnectionLineSaveData>();
}
