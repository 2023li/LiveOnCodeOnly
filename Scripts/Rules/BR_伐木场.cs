using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BR_伐木场 : Rule
{


    SupplyDef SD_木材;
    

  

    public override object Clone()
    {
        return new BR_伐木场();
    }

    public override string GetDescription()
    {
        return "xx";
    }

    public override string GetRuleName() => "伐木场规则";
  

    public override void OnAdd(BuildingInstance self)
    {
        if (SD_木材 == null)
        {
            SD_木材 = SupplyLib.GetSupplyDef(SupplyEnum.SD_原木);
        }
        self.AddProduct(SD_木材);
    }

    public override void OnRemove(BuildingInstance self)
    {
      
        self.RemoveProduct(SD_木材) ;
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
                break;
            case TurnPhase.开始准备阶段:
                break;
            default:
                break;
        }
    }

   
}
