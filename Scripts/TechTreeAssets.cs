using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum TechNodeState
{
    已研究,
    可研究,
    正在研究,
    不可研究,
}

[System.Serializable]
public class TechNodeData
{
    public string id;                           // 使用 string ID
    public string name;
    public string description;
    public Sprite icon;
    public int cost;
    public List<string> dependencies = new List<string>();
    public Vector2 position;
}

[CreateAssetMenu(menuName = "LifeOn/TechTreeAssets", fileName = "TechTreeAssets")]
public class TechTreeAssets : ScriptableObject
{
    public List<TechNodeData> techList = new List<TechNodeData>();

    // 采用忽略大小写的字典，避免大小写不一致造成的找不到
    private Dictionary<string, TechNodeData> techDict = new Dictionary<string, TechNodeData>(StringComparer.OrdinalIgnoreCase);

    private void OnEnable()
    {
        BuildLookup();
    }

    // 任意改动后自动重建字典，降低不同步风险
    private void OnValidate()
    {
        BuildLookup();
    }

    private void BuildLookup()
    {
        techDict.Clear();
        foreach (var t in techList)
        {
            if (t == null) continue;
            if (string.IsNullOrEmpty(t.id)) continue;
            techDict[t.id] = t;
        }
    }

    // 外部可显式重建
    public void RebuildLookup() => BuildLookup();

    // 节点 ID 修改时调用（由编辑器视图在改 ID 后触发）
    public void NotifyIdChanged(string oldId, string newId, TechNodeData node)
    {
        if (!string.IsNullOrEmpty(oldId)) techDict.Remove(oldId);
        if (!string.IsNullOrEmpty(newId) && node != null) techDict[newId] = node;
    }

    public TechNodeData GetTech(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (techDict.TryGetValue(id, out var t)) return t;

        // 兜底：当字典不同步时进行线性查找并回填
        var t2 = techList.Find(x => x != null && !string.IsNullOrEmpty(x.id)
            && string.Equals(x.id, id, StringComparison.OrdinalIgnoreCase));
        if (t2 != null) techDict[id] = t2;
        return t2;
    }

    public bool ContainsTech(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        return techDict.ContainsKey(id);
    }

    // 生成不重复的新ID（T001, T002 ...）
    public string GenerateNewId()
    {
        int max = 0;
        foreach (var t in techList)
        {
            if (t == null || string.IsNullOrEmpty(t.id)) continue;
            // 支持 "T###" 格式解析
            if (t.id.StartsWith("T", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(t.id.Substring(1), out int n))
                    max = Mathf.Max(max, n);
            }
        }
        return $"T{(max + 1).ToString("D3")}";
    }

    public TechNodeData AddTech(string name = "新科技", string description = "", int cost = 0, Sprite icon = null)
    {
        var id = GenerateNewId();
        var node = new TechNodeData
        {
            id = id,
            name = name,
            description = description,
            cost = cost,
            icon = icon,
            dependencies = new List<string>(),
            position = Vector2.zero
        };
        techList.Add(node);
        techDict[id] = node;
        return node;
    }

    public bool RemoveTech(string id)
    {
        var t = GetTech(id);
        if (t == null) return false;
        techList.Remove(t);
        techDict.Remove(id);
        // 清理它在其他节点依赖中的引用（忽略大小写）
        foreach (var n in techList)
        {
            if (n?.dependencies == null) continue;
            n.dependencies.RemoveAll(d => string.Equals(d, id, StringComparison.OrdinalIgnoreCase));
        }
        return true;
    }

    public bool AddDependency(string prereqId, string techId)
    {
        var tech = GetTech(techId);
        var pre = GetTech(prereqId);
        if (tech == null || pre == null) return false;
        if (string.Equals(tech.id, prereqId, StringComparison.OrdinalIgnoreCase)) return false; // 自依赖拦截

        if (tech.dependencies == null) tech.dependencies = new List<string>();
        if (tech.dependencies.Any(d => string.Equals(d, prereqId, StringComparison.OrdinalIgnoreCase)))
            return false;

        tech.dependencies.Add(prereqId);
        return true;
    }

    public bool RemoveDependency(string prereqId, string techId)
    {
        var tech = GetTech(techId);
        if (tech == null || tech.dependencies == null) return false;
        int removed = tech.dependencies.RemoveAll(d => string.Equals(d, prereqId, StringComparison.OrdinalIgnoreCase));
        return removed > 0;
    }

    public bool AreDependenciesMet(string techId, HashSet<string> unlocked)
    {
        var t = GetTech(techId);
        if (t == null) return false;
        foreach (var dep in t.dependencies)
            if (!unlocked.Contains(dep)) return false;
        return true;
    }

    public List<TechNodeData> GetAvailableTechs(HashSet<string> unlocked)
    {
        var list = new List<TechNodeData>();
        foreach (var t in techList)
        {
            if (t == null) continue;
            if (unlocked.Contains(t.id)) continue;
            bool ok = true;
            foreach (var dep in t.dependencies)
            {
                if (!unlocked.Contains(dep)) { ok = false; break; }
            }
            if (ok) list.Add(t);
        }
        return list;
    }
}
