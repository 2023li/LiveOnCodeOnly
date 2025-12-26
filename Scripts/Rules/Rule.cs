using UnityEngine;
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;



public enum RuleLifecycle
{
    //伴随建筑到销毁
    Persistent = 0,
    //持续一定回合数
    TimeBased = 1,
    //持续到建筑升级
    LevelBase = 2,
}

[Serializable]
public abstract class Rule:ICloneable
{
    [ShowInInspector, MultiLineProperty(3),PropertyOrder(-1), HideLabel]
    [FoldoutGroup("描述")]
    public string DescriptionDisplay => GetDescription();
    public abstract string GetRuleName();

    public RuleLifecycle Lifecycle = RuleLifecycle.Persistent;
    [ShowIf(nameof(Lifecycle), RuleLifecycle.TimeBased)] 
    public int RemainingRounds = -1;
    

    public abstract string GetDescription();                  // 规则说明


    public abstract void OnAdd(BuildingInstance self);

    public abstract void OnUpdate(BuildingInstance self, TurnPhase phase);

    public abstract void OnRemove(BuildingInstance self);
   

    public abstract object Clone();
  
}


[Serializable]
public class R_填充就业 : Rule
{

    public override string GetRuleName() => $"填补就业人口";

    public override object Clone()
    {
        return new R_填充就业();
    }

    public override void OnAdd(BuildingInstance self)
    {
    }
    public override void OnUpdate(BuildingInstance self, TurnPhase phase)
    {
        switch (phase)
        {
            case TurnPhase.回合结束阶段:
                Debug.Log("执行");
                if (self.Self_CurrentWorkers<self.RO_MaxJobsPosition)
                {
                    Debug.Log("有空位");
                    if(self.Ctx.HumanResourcesNetwork.Unemployed > 0)
                    {
                        self.Self_CurrentWorkers++;

                        Debug.LogWarning("以后需要优化");
                    }


                }
                break;
        }
    }
    public override void OnRemove(BuildingInstance self)
    {
    }

    public override string GetDescription()
    {
        return "x";
    }
}



[Serializable]
public class R_回合结束时获取经验 : Rule
{

    public override string GetRuleName() => $"回合结束时增加{AddExp}exp";

    public int AddExp = 1;

   

    public override object Clone()
    {
        var  r =  new R_回合结束时获取经验();
        r.AddExp = AddExp;
        return r;
    }

    public override void OnAdd(BuildingInstance self)
    {
    }
    public override void OnUpdate(BuildingInstance self, TurnPhase phase)
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
                self.Self_CurrentExp += AddExp;
                break;
            case TurnPhase.开始准备阶段:
                break;
            default:
                break;
        }
    }

    public override void OnRemove(BuildingInstance self)
    {
    }

    public override string GetDescription()
    {
        return "X";
    }
}

[Serializable]
public class R_野生浆果丛规则:Rule
{

    public override string GetRuleName() { return $"生成{supplyAmount.Count}个浆果"; }

    public List<SupplyAmount> supplyAmount;

    public override object Clone()
    {
        var r =  new R_野生浆果丛规则();
        r.supplyAmount = supplyAmount;
        return r;
    }

    public override void OnAdd(BuildingInstance self)
    {
        foreach (SupplyAmount item in supplyAmount)
        {
            self.AddProduct(item.Resource);
        }


        //添加一个光环
        self.Ctx.Environment.AddAura("aaa", self, AuraCategory.Beauty, new AuraRing(5, 1));
        self.Ctx.Environment.AddAura("bbb", self, AuraCategory.Beauty, new AuraRing(4, 2));
        self.Ctx.Environment.AddAura("ccc", self, AuraCategory.Beauty, new AuraRing(3, 3));
        Debug.Log("添加光环");

    }

    public override void OnRemove(BuildingInstance self)
    {
        foreach (SupplyAmount item in supplyAmount)
        {
            self.AddProduct(item.Resource);
        }


        self.Ctx.Environment.RemoveAura("ccc");
        self.Ctx.Environment.RemoveAura("aaa");
        self.Ctx.Environment.RemoveAura("bbb");
    }

    public override void OnUpdate(BuildingInstance self, TurnPhase phase)
    {
        switch (phase)
        {
            case TurnPhase.结束准备阶段:
                break;
            case TurnPhase.资源消耗阶段:
                break;
            case TurnPhase.资源生产阶段:

                foreach (SupplyAmount item in supplyAmount)
                {
                    if (!self.Ctx.ResourceNetwork.TryAddResource(item.Resource,item.Amount,out string r))
                    {
                        Debug.Log(r);
                    }
                }

                break;
            case TurnPhase.回合结束阶段:
                break;
            case TurnPhase.开始准备阶段:

                

                break;
        }
    }

    public override string GetDescription()
    {
        return "";
    }
}
