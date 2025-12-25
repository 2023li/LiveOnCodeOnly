using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;



public static class ConditionUtility
{

    /// <summary>
    /// 逐项评估条件列表，若有失败返回 false 并写出失败原因。
    /// </summary>
    /// <param name="conditions">条件集合，允许为空。</param>
    /// <param name="self">当前建筑实例，可为空。</param>
    /// <param name="ctx">游戏上下文，允许为空但可能导致评估失败。</param>
    /// <param name="failedReason">失败原因，为空字符串表示全部通过。</param>
    public static bool TryEvaluateConditions(IEnumerable<Condition> conditions, BuildingInstance self, IGameContext ctx, out string failedReason)
    {
        failedReason = string.Empty;

        if (conditions == null)
        {
            return true;
        }

        foreach (Condition condition in conditions)
        {
            if (condition == null)
            {
                failedReason = "条件配置为空";
                return false;
            }

            try
            {
                if (condition.Evaluate(self, ctx, out string why))
                {
                    continue;
                }

                failedReason = string.IsNullOrWhiteSpace(why)
                    ? $"条件 {condition.GetType().Name} 未通过"
                    : why;
                return false;
            }
            catch (Exception ex)
            {
                failedReason = $"条件 {condition.GetType().Name} 评估异常：{ex.Message}";
                return false;
            }
        }

        return true;
    }
}





[Serializable]
public abstract class Condition
{
    public abstract bool Evaluate(BuildingInstance self, IGameContext ctx, out string why);
}







[Serializable]
public class C_永远不 : Condition
{
    public override bool Evaluate(BuildingInstance self, IGameContext ctx, out string why)
    {
        why = "这个条件永远不满足";
        return false;
    }
}


[Serializable]
public class C_需要科技 : Condition
{
    public string TechId;
    public override bool Evaluate(BuildingInstance self, IGameContext ctx, out string why)
    {
        why = "";
        return ctx != null && ctx.TechTree != null && ctx.TechTree.IsUnlocked(TechId);
    }
}



// Rules/Conditions.cs ——追加
[Serializable]
public class C_工人大于等于 : Condition
{
    public int Min;
    public override bool Evaluate(BuildingInstance self, IGameContext ctx, out string why)
    { why = ""; return self.Self_CurrentWorkers >= Min; }
}

[Serializable]
public class C_工人少于 : Condition
{
    public int MaxExclusive;
    public override bool Evaluate(BuildingInstance self, IGameContext ctx, out string why)
    { why = ""; return self.Self_CurrentWorkers < MaxExclusive; }
}

[Serializable]
public class WorkersEquals : Condition
{
    public int Count;
    public override bool Evaluate(BuildingInstance self, IGameContext ctx, out string why)
    { why = ""; return self.Self_CurrentWorkers == Count; }
}



[Serializable]
public class PopulationLessThan : Condition
{
    public int MaxExclusive;

    public override bool Evaluate(BuildingInstance self, IGameContext ctx, out string why)
    {
        why = "";
        return self.Self_CurrentPopulation < MaxExclusive;
    }
}

[Serializable]
public class PopulationAtLeast : Condition
{
    public int Min;
    public override bool Evaluate(BuildingInstance self, IGameContext ctx, out string why)
    {
        why = "";
        return self.Self_CurrentPopulation >= Min;
    }
}
