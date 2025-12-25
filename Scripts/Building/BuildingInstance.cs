/*
 * 建筑实例类中的RO_系列属性 是指可以有其他数据推演
 * Current系列值属于自身则必须自行保存
 */


using UnityEngine;
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;



public enum BuildingStateValueType
{
    LevelIndex,
    CurrentExp,
    ExpToNext,
    MaxPopulation,
    CurrentPopulation,
    CurrentWorkers,
    MaxStorageCapacity,
    TransportationAbility,
    TransportationResistance,
    就业吸引力,
    产品列表,
    转运流量,
}


[Serializable]
public class BuildingStatModifiers
{
    // 升级所需经验
    public int Base_ExpToNextAdd = 0;
    public float Base_ExpToNextMul = 1f;
    public int Bonus_ExpToNextAdd = 0;
    public float Bonus_ExpToNextMul = 1f;
    public float Final_ExpToNextMul = 1f;

    // 最大人口数
    public int Base_MaxPopulationAdd = 0;
    public float Base_MaxPopulationMul = 1f;
    public int Bonus_MaxPopulationAdd = 0;
    public float Bonus_MaxPopulationMul = 1f;
    public float Final_MaxPopulationMul = 1f;


    // 最大工作岗位数（对应 MaxJobsPosition）
    public int Base_MaxJobsPositionAdd = 0;
    public float Base_MaxJobsPositionMul = 1f;
    public int Bonus_MaxJobsPositionAdd = 0;
    public float Bonus_MaxJobsPositionMul = 1f;
    public float Final_MaxJobsPositionMul = 1f;


    // 最大库存数
    public int Base_StorageCapacityAdd = 0;
    public float Base_StorageCapacityMul = 1f;
    public int Bonus_StorageCapacityAdd = 0;
    public float Bonus_StorageCapacityMul = 1f;
    public float Final_StorageCapacityMul = 1f;

    // 转运疲劳值
    public int Base_TransportationResistanceAdd = 0;
    public float Base_TransportationResistanceMul = 1f;
    public int Bonus_TransportationResistanceAdd = 0;
    public float Bonus_TransportationResistanceMul = 1f;
    public float Final_TransportationResistanceMul = 1f;

    // 转运范围（原 TransportStrength 改为 Radius，补全结构）
    public int Base_TransportRadiusAdd = 0;
    public float Base_TransportRadiusMul = 1f;
    public int Bonus_TransportRadiusAdd = 0;
    public float Bonus_TransportRadiusMul = 1f;
    public float Final_TransportRadiusMul = 1f;

    // 分发范围（原 DistributeStrength 改为 Radius，补全结构）
    public int Base_DistributeRadiusAdd = 0;
    public float Base_DistributeRadiusMul = 1f;
    public int Bonus_DistributeRadiusAdd = 0;
    public float Bonus_DistributeRadiusMul = 1f;
    public float Final_DistributeRadiusMul = 1f;

    // 转运流量 MaxTraffic（遵循统一属性结构：基础增减+基础倍率+额外增减+额外倍率+最终倍率）
    public int Base_MaxTrafficAdd = 0;          // 基础转运流量增减（固定值）
    public float Base_MaxTrafficMul = 1f;      // 基础转运流量倍率（乘法系数）
    public int Bonus_MaxTrafficAdd = 0;        // 额外转运流量增减（奖励/buff 叠加值）
    public float Bonus_MaxTrafficMul = 1f;     // 额外转运流量倍率（奖励/buff 叠加系数）
    public float Final_MaxTrafficMul = 1f;     // 最终转运流量总倍率（汇总所有倍率后的值）

    // 工作吸引力
    public int Base_JobAttractivenessAdd = 0;
    public float Base_JobAttractivenessMul = 1f;
    public int Bonus_JobAttractivenessAdd = 0;
    public float Bonus_JobAttractivenessMul = 1f;
    public float Final_JobAttractivenessMul = 1f;
}






public class BuildingInstance : MonoBehaviour
{
    #region 静态

    private static readonly HashSet<BuildingInstance> _activeInstances = new();

    public static IReadOnlyCollection<BuildingInstance> ActiveInstances => _activeInstances;

    private static Dictionary<CubeCoor, BuildingInstance> Static_OccupyMap = new Dictionary<CubeCoor, BuildingInstance>();
    // 优化后的 TryGetBuildingAtCell，复杂度从 O(N) 降为 O(1)
    public static bool TryGetBuildingAtCell(CubeCoor cell, out BuildingInstance inst)
    {
        return Static_OccupyMap.TryGetValue(cell, out inst);
    }

    #endregion









    #region 基础 & 状态字段 + 属性


    //----------------------------基础信息-----------------------------------


    [LabelText("实例ID"), ShowInInspector, ReadOnly]
    public string InstanceId { get; private set; } = Guid.NewGuid().ToString("N");

    [ReadOnly, ShowInInspector, LabelText("建筑定义数据")]
    public BuildingArchetype Def { get; set; }
    public string DisplayName => Def == null ? "未知数据" : Def.DisplayName;

    public event Action<BuildingInstance, BuildingStateValueType> OnStateChanged;

    private BuildingStatModifiers RO_StatModifiers = new BuildingStatModifiers();

    //----------------------------等级-----------------------------------


    [Header("等级"), LabelText("当前等级索引"), ShowInInspector, ReadOnly]
    private int _selfCurrentLevelIndex;
    public int Self_LevelIndex
    {
        get => _selfCurrentLevelIndex;
        private set
        {
            _selfCurrentLevelIndex = value;
            OnStateChanged?.Invoke(this, BuildingStateValueType.LevelIndex);
        }
    }

    [ShowInInspector, ReadOnly, LabelText("当前经验")]
    private int _selfCurrentExp;
    public int Self_CurrentExp
    {
        get => _selfCurrentExp;
        set
        {
            _selfCurrentExp = value;
        }
    }

    [ShowInInspector, ReadOnly, LabelText("升级所需经验(运行时)")]
    public int RO_ExpToNext
    {
        get
        {
            if (GetLevelData() == null)
            {
                return 0;
            }
            //计算基础值的修正
            float fBase = (GetLevelData().ExpToNext + RO_StatModifiers.Base_ExpToNextAdd) * RO_StatModifiers.Base_ExpToNextMul;
            float fBonus = RO_StatModifiers.Bonus_ExpToNextMul * RO_StatModifiers.Bonus_ExpToNextAdd;
            float f = (fBase + fBonus) * RO_StatModifiers.Final_ExpToNextMul;
            return (int)f;
        }

    }


    //----------------------------人口 & 就业-----------------------------------
    [ShowInInspector, ReadOnly, LabelText("运行时最大人口")]
    public int RO_MaxPopulation
    {
        get
        {
            if (GetLevelData() == null)
            {
                return 0;
            }

            //计算基础值的修正
            float fBase = (GetLevelData().BaseMaxPopulation + RO_StatModifiers.Base_MaxPopulationAdd) * RO_StatModifiers.Base_MaxPopulationMul;
            float fBonus = RO_StatModifiers.Bonus_MaxPopulationAdd * RO_StatModifiers.Bonus_MaxPopulationMul;
            float f = (fBase + fBonus) * RO_StatModifiers.Final_MaxPopulationMul;
            return (int)f;
        }

    }
    [ShowInInspector, ReadOnly, LabelText("当前人口")]
    private int _selfCurrentPopulation;
    public int Self_CurrentPopulation
    {
        get => _selfCurrentPopulation;
        set
        {
            // 1. 计算有效最大值（避免 RO_MaxPopulation 为负数的异常情况）
            int maxValid = Math.Max(RO_MaxPopulation, 0);
            // 2. 钳位 newValue：确保在 [0, maxValid] 范围内（不超上限、不小于0）
            int newValue = Math.Clamp(value, 0, maxValid);
            // 3. 只有值真的变化时，才赋值并触发事件（避免无效调用）
            if (_selfCurrentPopulation != newValue)
            {
                _selfCurrentPopulation = newValue;
                OnStateChanged?.Invoke(this, BuildingStateValueType.CurrentPopulation);
            }

        }
    }
    [ShowInInspector, ReadOnly, LabelText("当前工人")]
    private int _selfCurrentWorkers;
    public int Self_CurrentWorkers
    {

        set
        {
            if (value <= Ctx.HumanResourcesNetwork.Unemployed)
            {
                _selfCurrentWorkers = value;
                OnStateChanged?.Invoke(this, BuildingStateValueType.CurrentWorkers);
            }
        }

        get => _selfCurrentWorkers;
    }

    [ShowInInspector, ReadOnly, LabelText("运行时最大工作岗位数")]
    public int RO_MaxJobsPosition
    {
        get
        {
            if (GetLevelData() == null)
            {
                return 0;
            }

            // 计算基础值的修正（与最大人口数逻辑完全对齐）
            float fBase = (GetLevelData().BaseMaxJobsPosition + RO_StatModifiers.Base_MaxJobsPositionAdd) * RO_StatModifiers.Base_MaxJobsPositionMul;
            float fBonus = RO_StatModifiers.Bonus_MaxJobsPositionAdd * RO_StatModifiers.Bonus_MaxJobsPositionMul;
            float f = (fBase + fBonus) * RO_StatModifiers.Final_MaxJobsPositionMul;
            return (int)f;
        }
    }


    [ShowInInspector, ReadOnly, LabelText("岗位吸引力")]
    public float RO_JobAttractiveness
    {
        get
        {
            if (GetLevelData() == null)
            {
                return 0;
            }

            //计算基础值的修正
            float fBase = (GetLevelData().BaseAttractivenessPerJob + RO_StatModifiers.Base_JobAttractivenessAdd) * RO_StatModifiers.Base_JobAttractivenessMul;
            float fBonus = RO_StatModifiers.Bonus_JobAttractivenessAdd * RO_StatModifiers.Bonus_JobAttractivenessMul;
            float f = (fBase + fBonus) * RO_StatModifiers.Final_JobAttractivenessMul;
            return f;
        }
    }


    //----------------------------库存与运力-----------------------------------
    [ShowInInspector, ReadOnly, LabelText("运行时库存容量")]
    public int RO_MaxStorageCapacity
    {
        get
        {
            if (GetLevelData() == null)
            {
                return 0;
            }

            //计算基础值的修正
            float fBase = (GetLevelData().BaseStorageCapacity + RO_StatModifiers.Base_StorageCapacityAdd) * RO_StatModifiers.Base_StorageCapacityMul;
            float fBonus = RO_StatModifiers.Bonus_StorageCapacityAdd * RO_StatModifiers.Bonus_StorageCapacityMul;
            float f = (fBase + fBonus) * RO_StatModifiers.Final_StorageCapacityMul;
            return (int)f;
        }
    }

    [ShowInInspector, ReadOnly, LabelText("运行时转运范围")]
    public float RO_TransportRadius
    {
        get
        {
            if (GetLevelData() == null)
            {
                return 0f; // 浮点型返回 0f，更规范
            }

            // 完全对齐库存容量的计算逻辑，仅替换字段名
            float fBase = (GetLevelData().BaseTransportRadius + RO_StatModifiers.Base_TransportRadiusAdd) * RO_StatModifiers.Base_TransportRadiusMul;
            float fBonus = RO_StatModifiers.Bonus_TransportRadiusAdd * RO_StatModifiers.Bonus_TransportRadiusMul;
            float f = (fBase + fBonus) * RO_StatModifiers.Final_TransportRadiusMul;
            return f;
        }
    }

    [ShowInInspector, ReadOnly, LabelText("运行时分发范围")]
    public float RO_DistributeRadius
    {
        get
        {
            if (GetLevelData() == null)
            {
                return 0f; // 浮点型返回 0f，更规范
            }

            // 完全对齐库存容量的计算逻辑，仅替换字段名
            float fBase = (GetLevelData().BaseDistributeRadius + RO_StatModifiers.Base_DistributeRadiusAdd) * RO_StatModifiers.Base_DistributeRadiusMul;
            float fBonus = RO_StatModifiers.Bonus_DistributeRadiusAdd * RO_StatModifiers.Bonus_DistributeRadiusMul;
            float f = (fBase + fBonus) * RO_StatModifiers.Final_DistributeRadiusMul;
            return f;
        }
    }
    [ShowInInspector, ReadOnly, LabelText("最大转运容量")]
    public float RO_MaxTraffic
    {
        get
        {
            if (GetLevelData() == null)
            {
                return 0f; // 浮点型返回 0f，更规范
            }

            // 完全对齐 RO_DistributeRadius 计算逻辑，仅替换 MaxTraffic 对应字段名
            float fBase = (GetLevelData().BaseMaxTraffic + RO_StatModifiers.Base_MaxTrafficAdd) * RO_StatModifiers.Base_MaxTrafficMul;
            float fBonus = RO_StatModifiers.Bonus_MaxTrafficAdd * RO_StatModifiers.Bonus_MaxTrafficMul;
            float f = (fBase + fBonus) * RO_StatModifiers.Final_MaxTrafficMul;
            return f;
        }
    }

    private float _currentTraffic;
    [ShowInInspector, ReadOnly, LabelText("当前转运流量")]
    public float RO_CurrentTraffic
    {
        get => _currentTraffic;
        set
        {
            if (_currentTraffic != value)
            {
                _currentTraffic = value;
                OnStateChanged?.Invoke(this, BuildingStateValueType.转运流量);
            }

        }
    }

    [ShowInInspector, ReadOnly, LabelText("剩余转运容量")]
    public float SurplusTraffic
    {
        get => RO_MaxTraffic - RO_CurrentTraffic;
    }



    [ShowInInspector, ReadOnly, LabelText("参与转运系统")]
    public bool RO_TransportationAbility
    {
        get => RO_MaxTraffic > 0;
    }

    [ShowInInspector, ReadOnly, LabelText("转运阻力")]
    public int RO_TransportationResistance
    {
        get
        {
            if (GetLevelData() == null)
            {
                return 0;
            }

            float fBase = (GetLevelData().BaseTransportationResistance + RO_StatModifiers.Base_TransportationResistanceAdd) * RO_StatModifiers.Base_TransportationResistanceMul;
            float fBonus = RO_StatModifiers.Bonus_TransportationResistanceAdd * RO_StatModifiers.Bonus_TransportationResistanceMul;
            float f = (fBase + fBonus) * RO_StatModifiers.Final_TransportationResistanceMul;
            return Mathf.Max(0, Mathf.RoundToInt(f));
        }
    }



    [ShowInInspector, ReadOnly, LabelText("产品列表")]
    public HashSet<SupplyDef> RO_CurrentProductList { get; private set; } = new HashSet<SupplyDef>(0); //目前只是作为产品源头 没有什么其他作用

    public bool AddProduct(SupplyDef product)
    {
        if (product == null) return false;
        if (RO_CurrentProductList == null) { RO_CurrentProductList = new HashSet<SupplyDef>(); }
        bool a = RO_CurrentProductList.Add(product);
        if (a)
        {
            OnStateChanged?.Invoke(this, BuildingStateValueType.产品列表);
        }
        return a;
    }
    public bool RemoveProduct(SupplyDef product)
    {
        if (product == null) return false;
        if (RO_CurrentProductList == null) return false;

        bool removed = RO_CurrentProductList.Remove(product);
        if (removed)
        {
            OnStateChanged?.Invoke(this, BuildingStateValueType.产品列表);
        }

        return removed;
    }


    //----------------------------地图占用（这些一般不触发状态事件，如需要也可改同样写法）-----------------------------------

    [ShowInInspector, ReadOnly, LabelText("占用格子")]
    public CubeCoor[] Self_CurrentOccupy { get; private set; } = Array.Empty<CubeCoor>();

    [ShowInInspector, ReadOnly, LabelText("中心坐标(网格)")]
    public CubeCoor Self_CurrentCenterInGrid { get; private set; }



    //----------------------------上下文 & 运行时缓存数据------------------------

    public IGameContext Ctx { get => GameContext.Instance; }

    private Dictionary<string, int> specificData_int;
    private Dictionary<string, float> specificData_float;
    private Dictionary<string, Vector3> specificData_v3;
    private Dictionary<string, string> specificData_string;
    // --- Set 方法 (编译时静态绑定，无装箱) ---
    public void SetData(string key, int value) { if (specificData_int == null) specificData_int = new Dictionary<string, int>(); specificData_int[key] = value; }
    public void SetData(string key, float value) { if (specificData_float == null) specificData_float = new Dictionary<string, float>();  specificData_float[key] = value; }
    public void SetData(string key, Vector3 value) { if (specificData_v3 == null) specificData_v3 = new Dictionary<string, Vector3>(); specificData_v3[key] = value; }
    public void SetData(string key, string value){ if (specificData_string == null) specificData_string = new Dictionary<string, string>(); specificData_string[key] = value; }

    // --- Get 方法 ---
    public int GetInt(string key) => specificData_int.TryGetValue(key, out var v) ? v : 0;
    public float GetFloat(string key) => specificData_float.TryGetValue(key, out var v) ? v : 0f;
    public Vector3 GetVector3(string key) => specificData_v3.TryGetValue(key, out var v) ? v : Vector3.zero;
    public string GetString(string key) => specificData_string.TryGetValue(key, out var v) ? v : null;


    #endregion




    #region Rule相关

    [ShowInInspector, ReadOnly, LabelText("当前规则列表")]
    public List<Rule> CurrentRules { get; private set; } = new List<Rule>();
    // 缓存待添加/移除的规则，防止遍历时修改集合报错
    private List<Rule> _pendingToAdd = new List<Rule>();
    private List<Rule> _pendingToRemove = new List<Rule>();

    public void AddRule(Rule rule)
    {
        if (rule == null) return;

        _pendingToAdd.Add(rule);

        // 立即触发 OnAdd (注意：Rule的OnAdd里如果涉及复杂逻辑需确保安全)
        rule.OnAdd(this);
    }
    public void RemoveRule(Rule rule)
    {
        if (rule == null) return;

        // 如果规则还在生效列表中，触发移除回调并标记删除
        if (CurrentRules.Contains(rule) || _pendingToAdd.Contains(rule))
        {
            rule.OnRemove(this);
            _pendingToRemove.Add(rule);
        }
    }
    private void ApplyPendingRuleChanges()
    {
        // 1. 处理移除
        if (_pendingToRemove.Count > 0)
        {
            foreach (var rule in _pendingToRemove)
            {
                CurrentRules.Remove(rule);
            }
            _pendingToRemove.Clear();
        }

        // 2. 处理添加
        if (_pendingToAdd.Count > 0)
        {
            CurrentRules.AddRange(_pendingToAdd);
            _pendingToAdd.Clear();
        }
    }

    //执行
    private void ExecutionRules(TurnPhase phase)
    {
        foreach (var rule in CurrentRules)
        {
            rule.OnUpdate(this, phase);
            if (phase==TurnPhase.回合结束阶段 && rule.Lifecycle==RuleLifecycle.TimeBased)
            {
                rule.RemainingRounds--;
                if (rule.RemainingRounds <= 0)
                {
                    RemoveRule(rule);
                }
            }
        }

       
    }
    // [新增] 加载基础规则
    private void LoadBaseRules()
    {
        if (Def?.BaseRules == null) return;

        foreach (var src in Def.BaseRules)
        {
            if (src == null) continue;
            var cloned = src.Clone() as Rule;
            if (cloned != null)
            {
                // 确保基础规则的生命周期正确（通常是 Persistent）
                cloned.Lifecycle = src.Lifecycle;
                cloned.RemainingRounds = src.RemainingRounds;
                AddRule(cloned);
            }
        }
    }
    private void LoadLevelRules(int levelIndex)
    {
        if (Def?.LevelsList == null || Def.LevelsList.Count == 0) return;
        int last = Def.LevelsList.Count - 1;
        var lvl = Def.LevelsList[Mathf.Clamp(levelIndex, 0, last)];
        var list = lvl?.Rules;
        if (list == null) return;

        for (int i = 0; i < list.Count; i++)
        {
            Rule src = list[i];
            Rule cloned = src?.Clone() as Rule;
            if (cloned == null)
            {
                Debug.LogWarning($"[BuildingInstance] 等级规则为空或克隆失败（index={i}）", this);
                continue;
            }
          
            AddRule(cloned);
        }
    }
   
    #endregion


    private void OnEnable()
    {
        _activeInstances.Add(this);


        RegisterToGame();
    }
    private void Start()
    {
        View = transform.GetComponentInChildren<BuildingView>();
        View.Init(this);
    }

    private void OnDisable()
    {
        UnRegisterToGame();


        _activeInstances.Remove(this);
    }



    public void Initialize(BuildingArchetype def, CubeCoor[] occupyCells, CubeCoor center, BuildingSaveData data = null)
    {
        Def = def;
        if (Def?.LevelsList == null || Def.LevelsList.Count == 0)
        {
            Debug.LogError("[BuildingInstance] 建筑定义缺少等级数据", this);
            return;
        }


        Self_CurrentOccupy = occupyCells ?? Array.Empty<CubeCoor>();
        Self_CurrentCenterInGrid = center;


        //无数据
        if (data == null)
        {
            LoadBaseRules();
            LoadLevelRules(0);
        }
        else
        {
           

            InstanceId = data.instanceId;
            Self_LevelIndex = data.level;
            Self_CurrentExp = data.currentEXP;
            Self_CurrentPopulation = data.currentPopulation;
            Self_CurrentWorkers = data.currentWorkers;

            specificData_int = data.intData;
            specificData_float = data.floatData;
            specificData_v3 = data.v3Data;
            specificData_string = data.stringData;

            LoadBaseRules();
            LoadLevelRules(Self_LevelIndex);
        }





        ApplyPendingRuleChanges();
    }

    public class BuildingSaveData
    {
        //常规数据
        public string archetypeID;
        public CubeCoor[] occupyCells;
        public CubeCoor currentCenterInGrid;
        public string instanceId;
        public int level;
        public int currentEXP;
        public int currentPopulation;
        public int currentWorkers;

        //特殊数据
        public Dictionary<string, string> stringData;
        public Dictionary<string , int> intData;
        public Dictionary<string, Vector3> v3Data;
        public Dictionary<string,float> floatData;

        public static BuildingSaveData GetData(BuildingInstance instance)
        {
           return instance.Save();
        }


    }

 
    public BuildingSaveData Save()
    {
        BuildingSaveData data = new BuildingSaveData();
        data.archetypeID = Def.Id;
        data.occupyCells = Self_CurrentOccupy;
        data.currentCenterInGrid = Self_CurrentCenterInGrid;
        data.instanceId = InstanceId;
        data.level = Self_LevelIndex;
        data.currentEXP = Self_CurrentExp;
        data.currentPopulation = Self_CurrentPopulation;
        data.currentWorkers = Self_CurrentWorkers;

        data.intData = specificData_int;
        data.floatData = specificData_float;
        data.v3Data = specificData_v3;
        data.stringData = specificData_string;    
        return data;
    }


    private void RegisterToGame()
    {
        Ctx.HumanResourcesNetwork.Register(this);
        Ctx.ResourceNetwork.Register(this);
        TurnSystem.OnTurnPhaseChange += HandleTurnPhase;

        foreach (CubeCoor pos in Self_CurrentOccupy)
        {
            Static_OccupyMap[pos] = this;
        }

    }


    private void UnRegisterToGame()
    {
        Ctx.HumanResourcesNetwork.UnRegister(this);
        Ctx.ResourceNetwork.UnRegister(this);
        TurnSystem.OnTurnPhaseChange -= HandleTurnPhase;

        foreach (var pos in Self_CurrentOccupy) Static_OccupyMap.Remove(pos);
    }


    private void HandleTurnPhase(TurnPhase phase)
    {
        switch (phase)
        {
            case TurnPhase.结束准备阶段:
                break;
            case TurnPhase.资源消耗阶段:
                break;
            case TurnPhase.资源生产阶段:
                break;
            case TurnPhase.回合结束阶段:
                TryUpgrade();
                break;
            case TurnPhase.开始准备阶段:
                ApplyPendingRuleChanges();
                break;
            default:
                Debug.Log($"{phase} 未处理");
                break;
        }

        ExecutionRules(phase);
    }



    private BuildingLevelDef GetLevelData()
    {
        if (Def == null || Def.LevelsList == null || Def.LevelsList.Count == 0)
            return null;

        return Def.LevelsList[Mathf.Clamp(Self_LevelIndex, 0, Def.LevelsList.Count - 1)];
    }




  
    

    public bool TryUpgrade()
    {
        // 1) 基础数据校验
        if (Def?.LevelsList == null || Def.LevelsList.Count == 0)
            return false;

        // 2) 满级判定
        int lastIndex = Def.LevelsList.Count - 1;
        if (Self_LevelIndex >= lastIndex)
            return false;

        // 3) 经验是否足够（优先使用运行时计算的 RO_ExpToNext）
        int expToNext = RO_ExpToNext > 0 ? RO_ExpToNext : Mathf.Max(0, GetLevelData()?.ExpToNext ?? 0);
        if (Self_CurrentExp < expToNext)
            return false;

        // 4) 升级条件判定（通常使用“当前等级”的允许升级条件；若为空可按需替换为“下一等级解锁条件”）
        var currentLevel = GetLevelData();
        var nextLevelIndex = Self_LevelIndex + 1;
        var nextLevel = Def.LevelsList[Mathf.Clamp(nextLevelIndex, 0, lastIndex)];

        List<Condition> conditions = currentLevel?.ConditionsForAllowingUpgrades; // 若你希望检查下一等级的解锁条件，可改为：nextLevel?.ConditionsForAllowingUpgrades 或 nextLevel?.UnlockConditions
        if (conditions != null && !ConditionUtility.TryEvaluateConditions(conditions, this, Ctx, out string reason))
        {
            Debug.LogWarning($"[BuildingInstance] 建筑 {Def.DisplayName} 无法升级：{reason}", this);
            return false;
        }

        // 5) 执行升级（保留多余经验）
        Self_LevelIndex = nextLevelIndex;
        Self_CurrentExp -= expToNext;
        if (Self_CurrentExp < 0) Self_CurrentExp = 0;

        // 策略：遍历现有规则，找到 Lifecycle == LevelBase 的全部移除
        for (int i = 0; i < CurrentRules.Count; i++)
        {
            var r = CurrentRules[i];
            if (r.Lifecycle == RuleLifecycle.LevelBase)
            {
                RemoveRule(r);
            }
        }

        LoadLevelRules(Self_LevelIndex);
        ApplyPendingRuleChanges();

        // 8) 通知状态变化（等级变化会影响多项运行时数值，按需补充/裁剪）
        OnStateChanged?.Invoke(this, BuildingStateValueType.LevelIndex);
        OnStateChanged?.Invoke(this, BuildingStateValueType.ExpToNext);
        OnStateChanged?.Invoke(this, BuildingStateValueType.MaxPopulation);
        OnStateChanged?.Invoke(this, BuildingStateValueType.MaxStorageCapacity);
        OnStateChanged?.Invoke(this, BuildingStateValueType.就业吸引力);

        return true;
    }

  





    public void DestroyBuilding()
    {
        Destroy(gameObject);
    }






    #region 视觉效果


    public BuildingView View {  get; private set; } 







    #endregion


}



public static class BuildingInstanceExtensions
{
    public static bool BE_TryAddResource(this BuildingInstance self,SupplyAmount item)
    {

        return BE_TryAddResource(self,item.Resource,item.Amount);
       
    }

    public static bool BE_TryAddResource(this BuildingInstance self, SupplyDef def,int num)
    {
        if (!self.Ctx.ResourceNetwork.TryAddResource(def, num, out string r))
        {
            Debug.Log(r);
            return false;
        }
        return true;
    }


}
