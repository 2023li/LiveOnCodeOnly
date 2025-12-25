using System;
using System.Collections.Generic;
using UnityEngine;

public enum DistanceMetric
{
    Hexagonal, // 六边形步数距离
    Euclidean  // 物理欧几里得距离
}

public enum GridDirection
{
    None, NorthEast, East, SouthEast, SouthWest, West, NorthWest
}

public enum ScopeCheckMode
{
    CenterOnly,
    AnyCellOverlap
}

public static class CoordinateCalculator
{
    // ---------------------------------------------------------
    // 1. 坐标转换核心 (Odd-R Pointy Top)
    // ---------------------------------------------------------

    /// <summary>
    /// Unity Offset (Vector3Int) -> CubeCoor
    /// </summary>
    public static CubeCoor OffsetToCube(Vector3Int offset)
    {
        var q = offset.x - (offset.y - (offset.y & 1)) / 2;
        var r = offset.y;
        return new CubeCoor(q, r, -q - r);
    }

    /// <summary>
    /// CubeCoor -> Unity Offset (Vector3Int)
    /// </summary>
    public static Vector3Int CubeToOffset(CubeCoor cube)
    {
        var col = cube.q + (cube.r - (cube.r & 1)) / 2;
        var row = cube.r;
        return new Vector3Int(col, row, 0);
    }

    // ---------------------------------------------------------
    // 2. 方向定义
    // ---------------------------------------------------------

    private static readonly CubeCoor[] _directionsHex = new CubeCoor[]
    {
        new CubeCoor(1, -1, 0),  // NorthEast (0)
        new CubeCoor(1, 0, -1),  // East (1)
        new CubeCoor(0, 1, -1),  // SouthEast (2)
        new CubeCoor(-1, 1, 0),  // SouthWest (3)
        new CubeCoor(-1, 0, 1),  // West (4)
        new CubeCoor(0, -1, 1)   // NorthWest (5)
    };

    // ---------------------------------------------------------
    // 3. 几何与范围 (纯 CubeCoor)
    // ---------------------------------------------------------

    /// <summary>
    /// 获取以 center 为中心，radius 为半径的区域
    /// </summary>
    public static List<CubeCoor> CellsInRadius(CubeCoor center, int radius, bool includeEdge = true)
    {
        var result = new List<CubeCoor>();
        if (radius < 0) return result;

        for (int q = -radius; q <= radius; q++)
        {
            int r1 = Mathf.Max(-radius, -q - radius);
            int r2 = Mathf.Min(radius, -q + radius);

            for (int r = r1; r <= r2; r++)
            {
                int s = -q - r;
                CubeCoor offset = new CubeCoor(q, r, s);
                CubeCoor current = center + offset;

                if (includeEdge || offset.Length() < radius)
                {
                    result.Add(current);
                }
            }
        }
        return result;
    }

    // ---------------------------------------------------------
    // 4. 寻路与移动 (A* / Dijkstra)
    // ---------------------------------------------------------

    public static List<CubeCoor> GetReachableCellsByMovePower(BuildingInstance buildingInstance,float movePower)
    {
        return GetReachableCellsByMovePower(buildingInstance.Self_CurrentOccupy, movePower);
    }
    /// <summary>
    /// 获取可到达的格子 (返回 CubeCoor 列表)
    /// </summary>
    public static List<CubeCoor> GetReachableCellsByMovePower(IEnumerable<CubeCoor> originCells, float movePower)
    {
        var result = new List<CubeCoor>();
        if (originCells == null || movePower <= 0f) return result;

        var costSoFar = new Dictionary<CubeCoor, float>();
        var frontier = new Queue<CubeCoor>();

        foreach (var cell in originCells)
        {
            costSoFar[cell] = 0f;
            frontier.Enqueue(cell);
        }

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();

            // 遍历 6 个方向
            foreach (var dir in _directionsHex)
            {
                CubeCoor next = current + dir;

                // 核心修复：调用 GridSystem 时传入 CubeCoor
                float resistance = GridSystem.Instance.GetMobileResistance(next);

                // 阻力 < 0 代表不可通行
                if (resistance < 0f || float.IsInfinity(resistance)) continue;

                float newCost = costSoFar[current] + resistance;
                if (newCost > movePower) continue;

                if (!costSoFar.TryGetValue(next, out float oldCost) || newCost < oldCost)
                {
                    costSoFar[next] = newCost;
                    frontier.Enqueue(next);
                }
            }
        }

        result.AddRange(costSoFar.Keys);
        return result;
    }

    /// <summary>
    /// A* 寻路 (返回 CubeCoor 路径)
    /// </summary>
    public static List<CubeCoor> GetPath(CubeCoor start, CubeCoor end, float maxCost = float.MaxValue)
    {
        if (start == end) return new List<CubeCoor>();

        var openSet = new List<CubeCoor> { start };
        var cameFrom = new Dictionary<CubeCoor, CubeCoor>();
        var gScore = new Dictionary<CubeCoor, float> { { start, 0 } };
        var fScore = new Dictionary<CubeCoor, float> { { start, start.DistanceTo(end) } };

        while (openSet.Count > 0)
        {
            // 简单排序取最小 F
            openSet.Sort((a, b) =>
            {
                float fa = fScore.ContainsKey(a) ? fScore[a] : float.MaxValue;
                float fb = fScore.ContainsKey(b) ? fScore[b] : float.MaxValue;
                return fa.CompareTo(fb);
            });

            CubeCoor current = openSet[0];
            if (current == end) return ReconstructPath(cameFrom, current);

            openSet.RemoveAt(0);

            foreach (var dir in _directionsHex)
            {
                CubeCoor neighbor = current + dir;

                float resistance = GridSystem.Instance.GetMobileResistance(neighbor);
                if (resistance < 0) continue;

                float tentativeG = gScore[current] + resistance;
                if (tentativeG > maxCost) continue;

                if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    fScore[neighbor] = tentativeG + neighbor.DistanceTo(end);

                    if (!openSet.Contains(neighbor)) openSet.Add(neighbor);
                }
            }
        }
        return null;
    }

    private static List<CubeCoor> ReconstructPath(Dictionary<CubeCoor, CubeCoor> cameFrom, CubeCoor current)
    {
        var totalPath = new List<CubeCoor> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            totalPath.Add(current);
        }
        totalPath.Reverse();
        return totalPath;
    }

    // ---------------------------------------------------------
    // 5. 辅助功能
    // ---------------------------------------------------------

    public static GridDirection GetDirection(CubeCoor from, CubeCoor to)
    {
        CubeCoor diff = to - from;
        for (int i = 0; i < _directionsHex.Length; i++)
        {
            if (diff == _directionsHex[i]) return IndexToDirection(i);
        }
        return GridDirection.None;
    }

    private static GridDirection IndexToDirection(int index)
    {
        switch (index)
        {
            case 0: return GridDirection.NorthEast;
            case 1: return GridDirection.East;
            case 2: return GridDirection.SouthEast;
            case 3: return GridDirection.SouthWest;
            case 4: return GridDirection.West;
            case 5: return GridDirection.NorthWest;
            default: return GridDirection.None;
        }
    }
}
