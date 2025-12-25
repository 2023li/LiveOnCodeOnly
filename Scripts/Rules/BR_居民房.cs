using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BR_居民房_LV1 : Rule
{



    public override string GetRuleName() => $"BR_居民房LV1";

    

    public override object Clone()
    {
        return new BR_居民房_LV1 ();
    }

    public override void OnAdd(BuildingInstance self)
    {


    }

    public override void OnRemove(BuildingInstance self)
    {
      

    }

    public override void OnUpdate(BuildingInstance self, TurnPhase phase)
    {

        switch (phase)
        {
            case TurnPhase.结束准备阶段:
                break;
            case TurnPhase.资源消耗阶段:


                if (self.Ctx.ResourceNetwork.TryConsumeResource(SupplyCategory.一级食物, 2))
                {

                    if (self.Self_CurrentPopulation < self.RO_MaxPopulation)
                    {
                        self.Self_CurrentPopulation += 2;
                    }
                }
                else
                {
                    Debug.LogWarning("消耗食物失败,待处理");
                }


                break;
            case TurnPhase.资源生产阶段:
                break;
            case TurnPhase.回合结束阶段:
                break;
            case TurnPhase.开始准备阶段:
                break;
            default:
                break;
        }
    }

    public override string GetDescription()
    {
        return "x";
    }
}
