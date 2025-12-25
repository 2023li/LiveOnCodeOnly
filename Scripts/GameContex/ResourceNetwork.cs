using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 资源网络：集中管理所有资源的库存、容量和运输覆盖范围。
/// 容量提供者（仓库）与转运节点（运输建筑）完全解耦。
/// </summary>
public class ResourceNetwork
{
    #region 容量系统
    //目前主要用于更新库存容量
    public event Action OnResourceNetworkStateChange;
    // ========= 基础数据 =========

    /// <summary>全局资源库存：按资源类型存储当前数量。</summary>
    private readonly Dictionary<SupplyDef, int> _resourceAmounts = new Dictionary<SupplyDef, int>();

    /// <summary>总容量上限（由所有容量提供者提供）与已用容量。</summary>
    private int _totalCapacity;
    private int _usedCapacity;

    public int TotalCapacity { get { return _totalCapacity; } }
    public int UsedCapacity { get { return _usedCapacity; } }

    /// <summary>
    /// 容量提供者：
    /// 键：建筑实例；值：该建筑当前提供的容量。
    /// 满足 RO_MaxStorageCapacity > 0 即可。
    /// </summary>
    private readonly Dictionary<BuildingInstance, int> _capacityProviders = new Dictionary<BuildingInstance, int>();

    // ========= 资源增减 =========

    public int GetSupplyAmount(SupplyDef resource)
    {
        if (resource == null) return 0;
        return _resourceAmounts.TryGetValue(resource, out var v) ? v : 0;
    }

    

    /// <summary>
    /// 获取当前所有资源及其数量的快照
    /// </summary>
    public IEnumerable<SupplyAmount> GetAllResourcesSnapshot()
    {
        foreach (var kv in _resourceAmounts)
        {
            if (kv.Key == null)
                continue;

            yield return new SupplyAmount
            {
                Resource = kv.Key,
                Amount = kv.Value
            };
        }
    }


    /// <summary>
    /// 获取可用库存
    /// </summary>
    /// <returns>可用库存</returns>
    public int GetFreeCapacity()
    {
        int free = _totalCapacity - _usedCapacity;
        return free > 0 ? free : 0;
    }

    public bool TryAddResource(SupplyDef resource, int amount, out string reason)
    {
        reason = string.Empty;

        if (resource == null || amount <= 0)
        {
            reason = "资源无效或数量必须为正数";
            return false;
        }

        int need = amount * resource.OccupationUnit;
        int free = _totalCapacity - _usedCapacity;
        if (need > free)
        {
            reason = $"容量不足，需要 {need}，仅剩 {free}";
            return false;
        }

        if (_resourceAmounts.TryGetValue(resource, out var current))
        {
            long nv = (long)current + amount;
            if (nv > int.MaxValue)
            {
                reason = "数量过大，超出上限";
                return false;
            }
            _resourceAmounts[resource] = (int)nv;
        }
        else
        {
            _resourceAmounts[resource] = amount;
        }

        _usedCapacity += need;
        if (_usedCapacity < 0) _usedCapacity = 0;

        OnResourceNetworkStateChange?.Invoke();

        return true;
    }

    /// <summary>
    /// 尝试消耗某一个物资
    /// </summary>
    public bool TryConsumeResource(SupplyDef resource, int amount, out string reason)
    {
        reason = string.Empty;

        if (resource == null || amount <= 0)
        {
            reason = "资源无效或数量必须为正数";
            return false;
        }

        if (!_resourceAmounts.TryGetValue(resource, out var current) || current < amount)
        {
            int have = current > 0 ? current : 0;
            reason = $"库存不足：需要 {amount}，仅有 {have}";
            return false;
        }

        int nv = current - amount;
        if (nv <= 0)
            _resourceAmounts.Remove(resource);
        else
            _resourceAmounts[resource] = nv;

        int freed = amount * resource.OccupationUnit;
        _usedCapacity -= freed;
        if (_usedCapacity < 0) _usedCapacity = 0;
        return true;
    }
    /// <summary>
    /// 尝试消耗某一个种类的物资
    /// </summary>
    public bool TryConsumeResource(SupplyCategory category, int amount)
    {
        // 基本校验
        if (amount <= 0)
            return false;

        // 收集所有这个类别的资源以及数量
        var candidates = new List<(SupplyDef def, int count)>();
        int total = 0;

        foreach (var kvp in _resourceAmounts)
        {
            var def = kvp.Key;
            if (def == null) continue;

            if (def.Category == category && kvp.Value > 0)
            {
                candidates.Add((def, kvp.Value));
                total += kvp.Value;
            }
        }

        // 总量不够，直接失败，不修改库存
        if (total < amount)
            return false;

        // 够的话，开始逐个资源扣减
        int remaining = amount;
        foreach (var item in candidates)
        {
            if (remaining <= 0)
                break;

            int take = Math.Min(item.count, remaining);

            // 利用已有的按 SupplyDef 消耗逻辑
            string reason;
            if (!TryConsumeResource(item.def, take, out reason))
            {
                // 理论上这里不会失败（前面已经检查过库存），
                // 为了安全打印一下日志。
                Debug.LogError($"[ResourceNetwork] 按类别消耗资源失败：{item.def.name}，原因：{reason}");
                return false;
            }

            remaining -= take;
        }

        return remaining == 0;
    }


    /// <summary>
    /// 注册一个建筑到资源网络。
    /// 同时作为：容量提供者 + 运输节点（由其当前运力决定是否参与路径计算）。
    /// </summary>
    public void Register(BuildingInstance building)
    {
        if (building == null)
            return;

        // ---- 容量提供者处理 ----
        int newCapacity = Mathf.Max(0, building.RO_MaxStorageCapacity);

        // 先移除旧记录，避免重复注册导致异常或容量叠加错误
        if (_capacityProviders.TryGetValue(building, out var oldCapacity))
        {
            _totalCapacity -= oldCapacity;
            _capacityProviders.Remove(building);
        }

        // 只有容量>0 的建筑才视为仓库
        if (newCapacity > 0)
        {
            _capacityProviders[building] = newCapacity;
            _totalCapacity += newCapacity;
        }

        if (_totalCapacity < 0)
            _totalCapacity = 0;

        // ---- 运输节点处理 ----
        bool canTransport = building.RO_TransportationAbility;
       

      

        // 为了避免重复订阅，先尝试取消再订阅一次
        building.OnStateChanged -= Handle_BuildingValueChange;
        building.OnStateChanged += Handle_BuildingValueChange;


        UpdateBuildingProducts(building);

        OnResourceNetworkStateChange?.Invoke();
    }

    /// <summary>
    /// 将建筑从资源网络中移除。
    /// </summary>
    public void UnRegister(BuildingInstance building)
    {
        if (building == null)
            return;

        // ---- 容量提供者移除 ----
        if (_capacityProviders.TryGetValue(building, out var oldCapacity))
        {
            _capacityProviders.Remove(building);
            _totalCapacity -= oldCapacity;
            if (_totalCapacity < 0)
                _totalCapacity = 0;
        }




        UpdateBuildingProducts(building);

        building.OnStateChanged -= Handle_BuildingValueChange;



        OnResourceNetworkStateChange?.Invoke();
    }



    #endregion


    #region 转运系统




    //当前能够生产的材料集合
    public HashSet<SupplyDef> CurrentProducibleMaterialEnums { get ;private set; } = new HashSet<SupplyDef>(20);


    // 内部计数：每个产品有多少建筑在生产
    private readonly Dictionary<SupplyDef, int> _producibleCounts  = new Dictionary<SupplyDef, int>();

    // 每栋建筑的“产品列表快照”，用于对比增删
    private readonly Dictionary<BuildingInstance, HashSet<SupplyDef>> _buildingProductSnapshot = new Dictionary<BuildingInstance, HashSet<SupplyDef>>();

    private static readonly HashSet<SupplyDef> EmptyProductSet = new HashSet<SupplyDef>();


    private void IncreaseProducible(SupplyDef def)
    {
        if (def == null) return;

        if (_producibleCounts.TryGetValue(def, out int count))
        {
            count++;
            _producibleCounts[def] = count;
        }
        else
        {
            _producibleCounts[def] = 1;
            CurrentProducibleMaterialEnums.Add(def);
        }
    }

    private void DecreaseProducible(SupplyDef def)
    {
        if (def == null) return;

        if (!_producibleCounts.TryGetValue(def, out int count))
            return;

        count--;
        if (count <= 0)
        {
            _producibleCounts.Remove(def);
            CurrentProducibleMaterialEnums.Remove(def);
        }
        else
        {
            _producibleCounts[def] = count;
        }
    }

    private void UpdateBuildingProducts(BuildingInstance building)
    {
        if (building == null) return;

        // 旧快照
        if (!_buildingProductSnapshot.TryGetValue(building, out var oldSet) || oldSet == null)
            oldSet = EmptyProductSet;

        // 当前真实列表（建筑里的 HashSet）
        var currentList = building.RO_CurrentProductList;

        // 拷贝一份作为新的快照，避免直接引用到建筑内部的集合
        var newSet = currentList != null
            ? new HashSet<SupplyDef>(currentList)
            : new HashSet<SupplyDef>();

        // 1) 处理被移除的产品：旧有、新没有 -> 总计数 -1
        foreach (var def in oldSet)
        {
            if (!newSet.Contains(def))
            {
                DecreaseProducible(def);
            }
        }

        // 2) 处理新增的产品：新有、旧没有 -> 总计数 +1
        foreach (var def in newSet)
        {
            if (!oldSet.Contains(def))
            {
                IncreaseProducible(def);
            }
        }

        // 覆盖快照
        _buildingProductSnapshot[building] = newSet;
    }


    #endregion

    private void Handle_BuildingValueChange(BuildingInstance instance, BuildingStateValueType type)
    {
        switch (type)
        {
            // 等级变化：可能会间接影响多项数值（容量、运力等）
            // 这里不直接处理，由具体数值事件来刷新。
            case BuildingStateValueType.LevelIndex:
                break;

            case BuildingStateValueType.CurrentExp:
            case BuildingStateValueType.ExpToNext:
            case BuildingStateValueType.MaxPopulation:
            case BuildingStateValueType.CurrentPopulation:
            case BuildingStateValueType.CurrentWorkers:
            case BuildingStateValueType.就业吸引力:
                // 这些与资源网络无直接关系
                break;

            // 仓库容量变化
            case BuildingStateValueType.MaxStorageCapacity:
                {
                    int newCap = Mathf.Max(0, instance.RO_MaxStorageCapacity);

                    if (_capacityProviders.TryGetValue(instance, out var oldCap))
                    {
                        _totalCapacity -= oldCap;
                        _capacityProviders.Remove(instance);
                    }

                    if (newCap > 0)
                    {
                        _capacityProviders[instance] = newCap;
                        _totalCapacity += newCap;
                    }

                    if (_totalCapacity < 0)
                        _totalCapacity = 0;

                    break;
                }

            // 是否允许作为运输节点
            case BuildingStateValueType.TransportationAbility:
                
                 
                  

                    // 运力开关变化会改变可达范围
              
                    break;
                

            // 运输阻力变化
            case BuildingStateValueType.TransportationResistance:
                // 阻力影响可达路径与损耗，直接清空覆盖缓存
             
                break;
            case BuildingStateValueType.产品列表:
                UpdateBuildingProducts(instance);


                break;
            case BuildingStateValueType.转运流量:
                break;
        }


        OnResourceNetworkStateChange?.Invoke();
    }

  


    // ========= 覆盖查询 =========

    public bool CanCellReceive(SupplyDef resource, Vector3Int cell)
    {
        Debug.Log("目前是全图可达");
        return true;
    }

    internal ResourceNetworkSaveData Save()
    {
        ResourceNetworkSaveData data = new ResourceNetworkSaveData();
        data.allSupplys =  _resourceAmounts.ToDictionary(
        kvp => kvp.Key.Id,
        kvp => kvp.Value);

        return data;
    }
    internal void Load(ResourceNetworkSaveData data)
    {
        if (data == null) return;

        _resourceAmounts.Clear();

        foreach (var kvp in data.allSupplys)
        {
            // 根据 ID 字符串获取对应的 SupplyDef 实例
            SupplyDef supply = SupplyDef.GetSupplyDef(kvp.Key);

            // 仅当资源定义存在时才还原，防止脏数据导致 NullReference
            if (supply != null)
            {
                //_resourceAmounts[supply] = kvp.Value;
                if(!TryAddResource(supply, kvp.Value,out string r))
                {
                    Debug.LogWarning(r);
                }
            }
            else
            {
                Debug.LogWarning($"[ResourceManager] 还原失败：找不到 ID 为 {kvp.Key} 的 SupplyDef");
            }
        }
    }
}

[Serializable]
public class ResourceNetworkSaveData
{
    public Dictionary<string, int> allSupplys;


    public ResourceNetworkSaveData()
    {
        
    }
}
