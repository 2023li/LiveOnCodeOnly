using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class UINode : MonoBehaviour,IBeginDragHandler, IDragHandler, IEndDragHandler
{

    public static readonly List<UINode> ActiveNodes = new List<UINode>();
    #region 生命周期
    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _nodeCanvasGroup = GetComponent<CanvasGroup>();
    }
    private void OnEnable()
    {
        ActiveNodes.Add(this);

    }
    private void Start()
    {
        UpdateInteractiveState();

        ConnectionManager.Instance.OnHideTransfer += Hide;
        ConnectionManager.Instance.OnSelectSupply += Handle_ConnectionManager_OnSelect;
        ConnectionManager.Instance.OnShowTransfer += Show;
        gameObject.SetActive(false);
        
    }

    private void OnDestroy()
    {
        if (ConnectionManager.HasInstance)
        {
            ConnectionManager.Instance.OnHideTransfer -= Hide;
            ConnectionManager.Instance.OnSelectSupply -= Handle_ConnectionManager_OnSelect;
            ConnectionManager.Instance.OnShowTransfer -= Show;
        }

        if (SelfBuilding != null)
        {
            SelfBuilding.OnStateChanged -= Handle_BuildStateChange;
        }

    }
    #endregion

    #region 画线
    [Header("拓扑")]
    public bool isStart = false;

    private bool _interactive = true;
    [ShowInInspector]
    public bool Interactive
    {
        get { return _interactive; }
        set
        {
            _interactive = value;
            UpdateInteractiveState();
        }
    }

    private CanvasGroup _nodeCanvasGroup;

    private void UpdateInteractiveState()
    {
        // Safety check: In case this is called before Awake (unlikely but possible via scripts)
        if (_nodeCanvasGroup == null)
            _nodeCanvasGroup = GetComponent<CanvasGroup>();

        if (_nodeCanvasGroup != null)
        {
            _nodeCanvasGroup.interactable = _interactive;
            _nodeCanvasGroup.blocksRaycasts = _interactive; // IMPORTANT: usually you want to block raycasts too if not interactive
            _nodeCanvasGroup.alpha = _interactive ? 1f : 0.7f;
        }
    }

    [Header("左右端口偏移")]
    public float horizontalMargin = 0.5f;   // 经过节点时左右点距节点边缘的额外偏移（世界单位）
    [LabelText("水平排列距离")]
    public float laneSpacing = 0.2f;         // 多条线经过同节点时的水平排列间距（世界单位）

    private RectTransform _rt;

    // 暴露 laneLines 给 manager/handle 用
    public IReadOnlyList<ConnectionLine> LaneLines => _laneLines;

    public float handleSizeWorld = 0.08f;   // 2D世界单位大小
    public float handleOffsetWorld = 0.12f; // 右侧偏移

    // 所有“占用该节点水平通道”的线（中间或末端拖拽时）
    [ShowInInspector,ReadOnly]
    private readonly List<ConnectionLine> _laneLines = new List<ConnectionLine>();
    private readonly Dictionary<ConnectionLine, int> _laneIndex = new Dictionary<ConnectionLine, int>();

    public RectTransform RectT => _rt;

  

  

    #region Drag Forward
    public void OnBeginDrag(PointerEventData eventData)
    {
        Debug.Log("开始拖拽");
        ConnectionManager.Instance.StartDrag(this, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        ConnectionManager.Instance.Drag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        ConnectionManager.Instance.EndDrag(this, eventData);
    }


  

    #endregion

    #region 路线
    /// <summary>
    /// 注册到线
    /// </summary>
    /// <param name="line"></param>
    public void RegisterLaneLine(ConnectionLine line)
    {
        if (_laneLines.Contains(line)) return;       
        _laneLines.Add(line);



        if (SelfBuilding)
        {
            SelfBuilding.RO_CurrentTraffic += line.SelfSupply.BaseTrafficOccupancy;
            UpdateBar();
        }
    }

    /// <summary>
    /// 从线中注销
    /// </summary>
    /// <param name="line"></param>
    public void UnregisterLaneLine(ConnectionLine line)
    {


        if (SelfBuilding)
        {
            SelfBuilding.RO_CurrentTraffic -= line.SelfSupply.BaseTrafficOccupancy;
            UpdateBar();
        }

        _laneLines.Remove(line);
        _laneIndex.Remove(line);
    }

    // Manager 在拓扑变化后调用
    public void RecalculateLanes()
    {
        // 固定排序保证 lane 稳定（可换成按创建时间）
        _laneLines.Sort((a, b) => a.CreationOrder.CompareTo(b.CreationOrder));
        _laneIndex.Clear();
        for (int i = 0; i < _laneLines.Count; i++)
            _laneIndex[_laneLines[i]] = i;
    }

    private float GetLaneYOffset(ConnectionLine line)
    {
        if (!_laneIndex.TryGetValue(line, out int idx))
            idx = 0;

        int count = _laneLines.Count;
        // 居中排列：[-1,0,1] 这种效果
        float centered = idx - (count - 1) * 0.5f;
        return centered * laneSpacing;
    }

    private float HalfWidthWorld()
    {
        // RectTransform width * lossyScale => 世界宽度
        return _rt.rect.width * _rt.lossyScale.x * 0.5f;
    }

    public Vector3 CenterLanePoint(ConnectionLine line)
    {
        return transform.position + _rt.up * GetLaneYOffset(line);
    }

    public Vector3 LeftPoint(ConnectionLine line)
    {
        float hw = HalfWidthWorld();
        return transform.position
               - _rt.right * (hw + horizontalMargin)
               + _rt.up * GetLaneYOffset(line);
    }

    public Vector3 RightPoint(ConnectionLine line)
    {
        float hw = HalfWidthWorld();
        return transform.position
               + _rt.right * (hw + horizontalMargin)
               + _rt.up * GetLaneYOffset(line);
    }

    #endregion
    #endregion

    #region 与建筑集成

    public SupplyDef CurrentActiveSupplyDef { get; set; }

    public float UnusedTrafficOccupancy
    {
        get
        {
            if (SelfBuilding == null)
            {
                return 0f;
            }
            return SelfBuilding.RO_MaxTraffic - SelfBuilding.RO_CurrentTraffic;
        }
    }

    [SerializeField,LabelText("进度条填充")]
    public Image barFull;

    [SerializeField, LabelText("物资图标")]
    public Image supplyDefIcon;


    public BuildingInstance SelfBuilding { get; set; }

    public void BuidBuildingInstance(BuildingInstance self)
    {
        SelfBuilding = self;

        SelfBuilding.OnStateChanged += Handle_BuildStateChange;
    }

    public void Show()
    {
       
        if (SelfBuilding.RO_TransportationAbility)
        {
            gameObject.SetActive(true);
        }
        Interactive = true;
        UpdateBar();
    }


    public void Handle_ConnectionManager_OnSelect(SupplyDef def)
    {
        //不是生产者 又 无转运能力
        if (!SelfBuilding.RO_CurrentProductList.Contains(def)&&!SelfBuilding.RO_TransportationAbility)
        {
            gameObject.SetActive(false);
        }

        //所选 类型的生产者
        if (SelfBuilding.RO_CurrentProductList.Contains(def))
        {
            CurrentActiveSupplyDef = def;
            supplyDefIcon.sprite = def.Icon;

            isStart = true;
        }

    }

    public void Hide()
    {
        gameObject?.SetActive(false);
    }

    [Button]
    public void UpdateBar()
    {
        if (SelfBuilding == null || SelfBuilding.RO_MaxTraffic <= 0)
        {
            barFull.fillAmount = 0f;
            return;
        }

        float percentage = SelfBuilding.RO_CurrentTraffic / SelfBuilding.RO_MaxTraffic;
        barFull.fillAmount = percentage;
    }


    private void Handle_BuildStateChange(BuildingInstance instance, BuildingStateValueType type)
    {
        if (SelfBuilding != instance)
        {
            return;
        }

        switch (type)
        {
            case BuildingStateValueType.LevelIndex:
                break;
            case BuildingStateValueType.CurrentExp:
                break;
            case BuildingStateValueType.ExpToNext:
                break;
            case BuildingStateValueType.MaxPopulation:
                break;
            case BuildingStateValueType.CurrentPopulation:
                break;
            case BuildingStateValueType.CurrentWorkers:
                break;
            case BuildingStateValueType.MaxStorageCapacity:
                break;
            case BuildingStateValueType.TransportationAbility:
                break;
            case BuildingStateValueType.TransportationResistance:
                break;
            case BuildingStateValueType.就业吸引力:
                break;
            case BuildingStateValueType.产品列表:
                break;
            case BuildingStateValueType.转运流量:
                UpdateBar();
                break;
            default:
                break;
        }
    }


    #endregion

}
