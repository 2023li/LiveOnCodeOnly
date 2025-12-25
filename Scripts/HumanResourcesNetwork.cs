using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
public class HumanResourcesNetwork
{
    public event Action OnHumanResourcesChange;

    private readonly Dictionary<BuildingInstance, int> _pop = new();
    private readonly Dictionary<BuildingInstance, int> _work = new();

    public int TotalPopulation => _pop.Values.Sum();
    public int TotalWorkers => _work.Values.Sum();
    public int Unemployed => Mathf.Max(0, TotalPopulation - TotalWorkers);


    public void Register(BuildingInstance building)
    {
        if (!_pop.ContainsKey(building))
        {
            _pop.Add(building, building.Self_CurrentPopulation);
        }
        if (!_work.ContainsKey(building))
        {
            _work.Add(building,building.Self_CurrentWorkers);
        }
        building.OnStateChanged += Handle_BuildingStateChange;
        OnHumanResourcesChange?.Invoke();
    }
    public void UnRegister(BuildingInstance building)
    {
        if (_pop.ContainsKey(building))
        {
            _pop.Remove(building);
        }
        if (_work.ContainsKey(building))
        {
            _work.Remove(building);
        }
        building.OnStateChanged -= Handle_BuildingStateChange;
        OnHumanResourcesChange?.Invoke();
    }



    private void Handle_BuildingStateChange(BuildingInstance building,BuildingStateValueType type)
    {
        switch (type)
        { 
            case BuildingStateValueType.CurrentPopulation:
                if (_pop.ContainsKey(building))
                {
                    _pop[building] = building.Self_CurrentPopulation;
                }

                break;
            case BuildingStateValueType.CurrentWorkers:

                if (_work.ContainsKey(building))
                {
                    _work[building] = building.Self_CurrentWorkers;
                }
                break;

        }
        OnHumanResourcesChange?.Invoke();
    }

    internal HumanResourcesNetworkSaveData Save()
    {
        //暂时没什么需要存
        return new HumanResourcesNetworkSaveData();
    }
}

[Serializable]
public class HumanResourcesNetworkSaveData
{


}
