

#region 光环

using System.Collections.Generic;
using System;

using UnityEngine;
using UnityEditor.Build.Utilities;

/// <summary>
/// 环境光环的单圈配置。
/// </summary>
[Serializable]
public struct AuraRing
{
    [Min(0)] public int Radius;
    public int Value;


    public AuraRing(int r,int v )
    {
        Radius = r;
        Value = v;
    }
}

/// <summary>
/// 光环类型：治安、医疗、美化。
/// </summary>
public enum AuraCategory
{
    Security,
    Health,
    Beauty
}
/// <summary>
/// 负责统计并查询城市环境类光环的辅助服务。
/// </summary>
public class CityEnvironment
{
    private struct AuraKey : IEquatable<AuraKey>
    {
        public AuraCategory Category;
        public CubeCoor Cell;

        public bool Equals(AuraKey other)
        {
            return Category == other.Category && Cell.Equals(other.Cell);
        }

        public override bool Equals(object obj)
        {
            if (obj is AuraKey other)
            {
                return Equals(other);
            }

            return false;
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 31 + Category.GetHashCode();
            hash = hash * 31 + Cell.GetHashCode();
            return hash;
        }
    }

    private class AuraRecord
    {
        public AuraCategory Category;
        public Dictionary<CubeCoor, int> CellValues = new Dictionary<CubeCoor, int>();
    }

    private readonly Dictionary<string, AuraRecord> activeAuras = new Dictionary<string, AuraRecord>();
    private readonly Dictionary<AuraKey, int> gridValues = new Dictionary<AuraKey, int>();

    /// <summary>
    /// 应用光环，旧数据会被覆盖。
    /// </summary>
    public void AddAura(string sourceId, CubeCoor center, AuraCategory category, IReadOnlyList<AuraRing> rings)
    {
        if (string.IsNullOrEmpty(sourceId))
        {
            return;
        }

        RemoveAura(sourceId);

        if (rings == null || rings.Count == 0)
        {
            return;
        }

        AuraRecord record = new AuraRecord
        {
            Category = category
        };

        for (int i = 0; i < rings.Count; i++)
        {
            AuraRing ring = rings[i];
            if (ring.Radius < 0 || ring.Value <= 0)
            {
                continue;
            }

            List<CubeCoor> cells = CoordinateCalculator.CellsInRadius(center, ring.Radius);
            for (int c = 0; c < cells.Count; c++)
            {
                CubeCoor cell = cells[c];
                if (record.CellValues.TryGetValue(cell, out int existing))
                {
                    if (ring.Value > existing)
                    {
                        record.CellValues[cell] = ring.Value;
                    }
                }
                else
                {
                    record.CellValues.Add(cell, ring.Value);
                }
            }
        }

        activeAuras[sourceId] = record;

        foreach (KeyValuePair<CubeCoor, int> pair in record.CellValues)
        {
            AuraKey key = new AuraKey
            {
                Category = record.Category,
                Cell = pair.Key
            };

            if (gridValues.TryGetValue(key, out int value))
            {
                gridValues[key] = value + pair.Value;
            }
            else
            {
                gridValues.Add(key, pair.Value);
            }
        }
    }

    public void AddAura(string sourceId, BuildingInstance building,AuraCategory category,params AuraRing[] args )
    {
        IReadOnlyList<AuraRing> rings = args;
        AddAura(sourceId,building.Self_CurrentCenterInGrid,category,rings);
    }

    /// <summary>
    /// 移除指定建筑的光环贡献。
    /// </summary>
    public void RemoveAura(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId))
        {
            return;
        }

        if (!activeAuras.TryGetValue(sourceId, out AuraRecord record))
        {
            return;
        }

        foreach (KeyValuePair<CubeCoor, int> pair in record.CellValues)
        {
            AuraKey key = new AuraKey
            {
                Category = record.Category,
                Cell = pair.Key
            };

            if (!gridValues.TryGetValue(key, out int value))
            {
                continue;
            }

            int reduced = value - pair.Value;
            if (reduced <= 0)
            {
                gridValues.Remove(key);
            }
            else
            {
                gridValues[key] = reduced;
            }
        }

        activeAuras.Remove(sourceId);
    }

    /// <summary>
    /// 查询某个格子的光环总值。
    /// </summary>
    public int GetValue(CubeCoor cell, AuraCategory category)
    {
        AuraKey key = new AuraKey
        {
            Category = category,
            Cell = cell
        };

        if (gridValues.TryGetValue(key, out int value))
        {
            return value;
        }

        return 0;
    }

    /// <summary>遍历所有有光环覆盖的格子。</summary>
    public IEnumerable<CubeCoor> EnumerateActiveCells()
    {
        HashSet<CubeCoor> yielded = new HashSet<CubeCoor>();
        foreach (KeyValuePair<AuraKey, int> pair in gridValues)
        {
            if (pair.Value <= 0)
            {
                continue;
            }

            if (yielded.Add(pair.Key.Cell))
            {
                yield return pair.Key.Cell;
            }
        }
    }

    /// <summary>遍历指定类型光环覆盖的所有格子。</summary>
    public IEnumerable<CubeCoor> EnumerateActiveCells(AuraCategory category)
    {
        foreach (KeyValuePair<AuraKey, int> pair in gridValues)
        {
            if (pair.Key.Category != category || pair.Value <= 0)
            {
                continue;
            }

            yield return pair.Key.Cell;
        }
    }

    /// <summary>根据数值条件筛选格子。</summary>
    public IEnumerable<CubeCoor> EnumerateCells(AuraCategory category, Func<int, bool> predicate)
    {
        if (predicate == null)
        {
            yield break;
        }

        foreach (KeyValuePair<AuraKey, int> pair in gridValues)
        {
            if (pair.Key.Category != category)
            {
                continue;
            }

            int value = pair.Value;
            if (value <= 0)
            {
                continue;
            }

            if (predicate(value))
            {
                yield return pair.Key.Cell;
            }
        }
    }

    /// <summary>判断格子光环是否大于等于指定阈值。</summary>
    public bool MeetsMinimum(CubeCoor cell, AuraCategory category, int minValue)
    {
        return GetValue(cell, category) >= minValue;
    }

    /// <summary>判断格子光环是否小于等于指定阈值。</summary>
    public bool MeetsMaximum(CubeCoor cell, AuraCategory category, int maxValue)
    {
        return GetValue(cell, category) <= maxValue;
    }

    /// <summary>判断格子光环是否等于指定数值。</summary>
    public bool MeetsExact(CubeCoor cell, AuraCategory category, int value)
    {
        return GetValue(cell, category) == value;
    }

    /// <summary>生成“至少为”条件。</summary>
    public Func<CubeCoor, bool> CreateMinimumCondition(AuraCategory category, int minValue)
    {
        return cell => MeetsMinimum(cell, category, minValue);
    }

    /// <summary>生成“至多为”条件。</summary>
    public Func<CubeCoor, bool> CreateMaximumCondition(AuraCategory category, int maxValue)
    {
        return cell => MeetsMaximum(cell, category, maxValue);
    }

    /// <summary>生成“等于”条件。</summary>
    public Func<CubeCoor, bool> CreateExactCondition(AuraCategory category, int value)
    {
        return cell => MeetsExact(cell, category, value);
    }

    /// <summary>根据条件筛选格子。</summary>
    public IEnumerable<CubeCoor> EnumerateCellsSatisfying(params Func<CubeCoor, bool>[] conditions)
    {
        if (conditions == null || conditions.Length == 0)
        {
            yield break;
        }

        foreach (CubeCoor cell in EnumerateActiveCells())
        {
            bool pass = true;
            for (int i = 0; i < conditions.Length; i++)
            {
                Func<CubeCoor, bool> condition = conditions[i];
                if (condition == null)
                {
                    continue;
                }

                if (!condition(cell))
                {
                    pass = false;
                    break;
                }
            }

            if (pass)
            {
                yield return cell;
            }
        }
    }

    /// <summary>根据条件列表筛选格子。</summary>
    public IEnumerable<CubeCoor> EnumerateCellsSatisfying(IReadOnlyList<Func<CubeCoor, bool>> conditions)
    {
        if (conditions == null || conditions.Count == 0)
        {
            yield break;
        }

        foreach (CubeCoor cell in EnumerateActiveCells())
        {
            bool pass = true;
            for (int i = 0; i < conditions.Count; i++)
            {
                Func<CubeCoor, bool> condition = conditions[i];
                if (condition == null)
                {
                    continue;
                }

                if (!condition(cell))
                {
                    pass = false;
                    break;
                }
            }

            if (pass)
            {
                yield return cell;
            }
        }
    }
}




#endregion
