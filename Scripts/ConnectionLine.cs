using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(LineRenderer))]
public class ConnectionLine : MonoBehaviour
{


  

    #region 画线




    public UINode startNode;
    public readonly List<UINode> nodes = new List<UINode>();

    private LineRenderer _lr;
    private Vector3? _tempTailWorld;

    public int CreationOrder { get; private set; }
    public UINode LastNode => nodes.Count > 0 ? nodes[nodes.Count - 1] : null;

    private void Awake()
    {
        _lr = GetComponent<LineRenderer>();
        _lr.useWorldSpace = true;
    }
    private void Start()
    {
        ConnectionManager.Instance.OnShowTransfer += Show;
        ConnectionManager.Instance.OnHideTransfer += Hide;
        ConnectionManager.Instance.OnSelectSupply += Handle_OnSelectSupply;

    }


    private void OnDestroy()
    {

        if (ConnectionManager.HasInstance)
        {
            ConnectionManager.Instance.OnShowTransfer -= Show;
            ConnectionManager.Instance.OnHideTransfer -= Hide;
            ConnectionManager.Instance.OnSelectSupply -= Handle_OnSelectSupply;
        }
    }


    public void Init(UINode start, SupplyDef supply, float width, int creationOrder)
    {
        startNode = start;
        CreationOrder = creationOrder;

        nodes.Clear();
        nodes.Add(start);
        SelfSupply = supply;

        _lr.material = SelfSupply.lineMat;
        _lr.widthMultiplier = width;

        start.RegisterLaneLine(this);
        RebuildPositions();
    }

    public void SetTempTail(Vector3 world)
    {
        _tempTailWorld = world;
        RebuildPositions();
    }

    public void ClearTempTail()
    {
        _tempTailWorld = null;
        RebuildPositions();
    }

    public void AppendNode(UINode node)
    {
        nodes.Add(node);
        node.RegisterLaneLine(this);
        RebuildPositions();
    }

    public void DetachAll()
    {
        foreach (var n in nodes)
            n.UnregisterLaneLine(this);
    }

    public bool ContainsSegment(UINode a, UINode b)
    {
        for (int i = 0; i < nodes.Count - 1; i++)
            if (nodes[i] == a && nodes[i + 1] == b) return true;
        return false;
    }
    public IReadOnlyList<Vector3> CachedPoints => _cachedPoints;
    private readonly List<Vector3> _cachedPoints = new();
    public void RebuildPositions()
    {
        var pts = new List<Vector3>();

        // --- 只有起点一个节点时，拖拽中实时可见 A -> 鼠标 ---
        if (nodes.Count == 1)
        {
            var p0 = nodes[0].CenterLanePoint(this);
            pts.Add(p0);
            pts.Add(_tempTailWorld ?? p0);

            _lr.positionCount = pts.Count;
            _lr.SetPositions(pts.ToArray());
            return;
        }

        // --- nodes.Count >= 2 正常构造 ---
        for (int i = 0; i < nodes.Count; i++)
        {
            UINode curr = nodes[i];
            bool isFirst = i == 0;
            bool isLast = i == nodes.Count - 1;

            if (isFirst)
            {
                pts.Add(curr.CenterLanePoint(this));
                continue;
            }

            UINode prev = nodes[i - 1];

            // 判断线是从左进还是从右进（用当前节点的 right 轴判断，适配旋转 Canvas）
            Vector3 rightAxis = curr.RectT != null ? curr.RectT.right : Vector3.right;
            bool comingFromRight = Vector3.Dot(prev.transform.position - curr.transform.position, rightAxis) > 0f;

            Vector3 entry = comingFromRight ? curr.RightPoint(this) : curr.LeftPoint(this);
            Vector3 exit = comingFromRight ? curr.LeftPoint(this) : curr.RightPoint(this);

            if (!isLast)
            {
                // 中间节点：保持“通过节点的水平段”，但按方向决定先后（问题1）
                pts.Add(entry);
                pts.Add(exit);
                continue;
            }

            // 最后节点：只保留进入侧的水平突出，不再两端都水平突出（问题2）
            if (_tempTailWorld.HasValue)
            {
                // 拖拽中：进入侧水平段 -> 节点中心 -> 鼠标
                pts.Add(entry);
                pts.Add(curr.CenterLanePoint(this));
                pts.Add(_tempTailWorld.Value);
            }
            else
            {
                // 落地后：进入侧水平段 -> 节点中心
                pts.Add(entry);
                pts.Add(curr.CenterLanePoint(this));
            }
        }

        _lr.positionCount = pts.Count;
        _lr.SetPositions(pts.ToArray());

        _cachedPoints.Clear();
        _cachedPoints.AddRange(pts);
    }
    #endregion

    #region 与建筑系统集成
    public SupplyDef SelfSupply {  get; private set; }

    public List<BuildingInstance> GetBuildingsForLine()
    {
        List<BuildingInstance> buildings = new List<BuildingInstance>();
        foreach (var item in nodes)
        {
            buildings.Add(item.SelfBuilding);
        }
        return buildings; 
    }


    public void Show()
    {
        gameObject.SetActive(true);
    }
    public void Hide()
    {
        gameObject?.SetActive(false);
    }
    private void Handle_OnSelectSupply(SupplyDef supply)
    {
        if (supply.Id == SelfSupply.Id)
        {

          
            Show();
        }
        else
        {
            Hide();
        }

        Debug.Log($"{supply.Id} {SelfSupply.Id}");
    }

    #endregion


    public ConnectionLineSaveData GetSaveData()
    {
        var data = new ConnectionLineSaveData();

        // 1. 保存物资 ID
        if (SelfSupply != null)
        {
            data.SupplyID = SelfSupply.Id;
        }

        // 2. 保存创建顺序
        data.CreationOrder = this.CreationOrder;

        // 3. 保存路径节点坐标
        data.NodePathCoordinates = new List<CubeCoor>();
        foreach (var node in nodes)
        {
            if (node != null && node.SelfBuilding != null)
            {
                // 使用建筑的中心坐标作为查找 Key
                data.NodePathCoordinates.Add(node.SelfBuilding.Self_CurrentCenterInGrid);
            }
        }

        return data;
    }

}

[Serializable]
public class ConnectionLineSaveData
{
    /// <summary>
    /// 物资类型的唯一ID
    /// </summary>
    public string SupplyID;

    /// <summary>
    /// 线条的创建顺序 (用于图层覆盖顺序)
    /// </summary>
    public int CreationOrder;

    /// <summary>
    /// 路径上所有节点的坐标列表 (有序：Start -> Middle -> End)
    /// 假设 CubeCoor 是可序列化的，如果不可序列化，需转换为 Vector3Int 或自定义结构
    /// </summary>
    public List<CubeCoor> NodePathCoordinates = new List<CubeCoor>();
}
