using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;


[Serializable]
public class BR_木材加工厂_LV0 : Rule
{
    public override object Clone()
    {
        return new BR_木材加工厂_LV0();
    }

    public override string GetDescription()
    {
        return "x";
    }

    public override string GetRuleName()
    {
        return "木材加工厂LV1";
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
