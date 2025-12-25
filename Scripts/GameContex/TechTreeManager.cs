// Assets/Scripts/GameContex/TechTreeManager.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 科技树运行时管理器：
/// 1) 从 TechTreeAssets 资源初始化节点
/// 2) 记录与查询解锁状态
/// 3) 计算“当前可研究”的节点（依赖满足、未解锁、未在研究中）
/// 4) 维护“当前正在研究”的节点列表
/// 5) 查询研究进度（单个或全部）
/// 额外：开始/取消研究、投入研究点、导入/导出存档状态。
/// </summary>
public class TechTreeManager
{
    public TechTreeManager()
    {
        Init();
    }

    public event Action<TechNodeData> ResearchStarted;
 
    public event Action<TechNodeData> ResearchCompleted;

    // —— 数据源（编辑器里配的 ScriptableObject）——
    private TechTreeAssets _treeAssets; // 引用到你的 TechTreeAssets 资源（ScriptableObject）

    // —— 运行时状态 —— 
    private readonly Dictionary<string, TechNodeData> _DicNodesID =
        new Dictionary<string, TechNodeData>(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _unlocked =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, ResearchProgressSnapshot> _progressSnapshots =
        new Dictionary<string, ResearchProgressSnapshot>(StringComparer.OrdinalIgnoreCase);

    public string ActiveResearchId => _activeResearchId;

    private string _activeResearchId = string.Empty;

    /// <summary>可选：记录一个“起始节点ID”（若需要）</summary>
    public string StartingNodeId { get; private set; } = string.Empty;

    //========================== 对外主功能 ==========================




    /// <summary>
    /// 1) 通过 TechTreeAssets 资源初始化科技树。
    /// 可传入一批已解锁的ID（如来自存档）。
    /// </summary>
    public void Init(IEnumerable<string> preUnlockedIds = null, string startingNodeId = null)
    {
        

        _treeAssets = ResourceRouting.Instance.treeAssets;

    

        _DicNodesID.Clear();
        foreach (TechNodeData t in _treeAssets.techList)
        {
            if (t == null || string.IsNullOrWhiteSpace(t.id)) continue;
            _DicNodesID[t.id] = t;
        }

        _unlocked.Clear();
        if (preUnlockedIds != null)
        {
            foreach (var id in preUnlockedIds)
            {
                if (!string.IsNullOrWhiteSpace(id) && _DicNodesID.ContainsKey(id))
                    _unlocked.Add(id);
            }
        }

        _progressSnapshots.Clear();
        _activeResearchId = string.Empty;

        // 设定起始节点（若未指定，则挑第一个“无依赖”的作为起点，若存在）
        StartingNodeId = startingNodeId ?? FindFirstRootId();
    }

    /// <summary>
    /// 2) 查询某节点是否已解锁。
    /// </summary>
    public bool IsUnlocked(string techId)
    {
        if (string.IsNullOrWhiteSpace(techId)) return false;
        return _unlocked.Contains(techId);
    }

    /// <summary>
    /// 3) 获取当前可研究的节点（依赖满足 + 未解锁 + 未在研究中）。
    /// </summary>
    public List<TechNodeData> GetResearchableNodes()
    {
        EnsureTreeBound();

        // TechTreeAssets 已内置“依赖满足 → 可研究”的判定与筛选
        // 参见 TechTreeAssets.AreDependenciesMet / GetAvailableTechs
        var available = _treeAssets.GetAvailableTechs(_unlocked); // 依赖满足但未解锁的列表
        // 过滤掉已经在研究中的
        return available.Where(t => !_progressSnapshots.ContainsKey(t.id)).ToList();
    }

    /// <summary>
    /// 4) 获取当前已经启动的节点（包含激活与暂停状态）。
    /// </summary>
    public List<TechNodeData> GetResearchingNodes()
    {
        return _progressSnapshots.Values.Select(r => r.Node).ToList();
    }

    /// <summary>
    /// 5) 获取某个节点的当前研究进度（0~1）。
    /// 已解锁返回 1。
    /// 未在研究且未解锁返回 0。
    /// </summary>
    public float GetResearchProgress(string techId)
    {
        if (string.IsNullOrWhiteSpace(techId)) return 0f;
        if (_unlocked.Contains(techId)) return 1f;
        if (_progressSnapshots.TryGetValue(techId, out var task))
            return task.ProgressRatio;
        return 0f;
    }

    /// <summary>
    /// （便捷）获取所有正在研究节点的进度快照。
    /// </summary>
    public Dictionary<string, float> GetAllResearchProgress()
    {
        var dict = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in _progressSnapshots)
            dict[kv.Key] = kv.Value.ProgressRatio;
        return dict;
    }

    //========================== 进阶/常用操作（可选） ==========================

    /// <summary>
    /// 开始研究指定节点。
    /// - 要求：存在、未解锁、依赖满足、未在研究中
    /// - cost<=0 将直接解锁
    /// </summary>
    public bool StartResearch(string techId)
    {
        EnsureTreeBound();
        if (!_DicNodesID.TryGetValue(techId, out var node)) return false;
        if (_unlocked.Contains(techId)) return false;

        // 依赖满足校验（TechTreeAssets 自带方法）
        if (!_treeAssets.AreDependenciesMet(techId, _unlocked)) return false;

        if (!string.IsNullOrEmpty(_activeResearchId) &&
            string.Equals(_activeResearchId, techId, StringComparison.OrdinalIgnoreCase) &&
            _progressSnapshots.TryGetValue(techId, out var activeSnapshot) &&
            !activeSnapshot.IsPaused)
        {
            return true;
        }

        if (node.cost <= 0)
        {
            // 零成本：直接解锁
            UnlockInternal(techId);
            return true;
        }

        bool isNewTask = false;
        if (!_progressSnapshots.TryGetValue(techId, out var task))
        {
            task = new ResearchProgressSnapshot(node);
            _progressSnapshots[techId] = task;
            isNewTask = true;
        }

        if (!string.IsNullOrEmpty(_activeResearchId) &&
            !string.Equals(_activeResearchId, techId, StringComparison.OrdinalIgnoreCase) &&
            _progressSnapshots.TryGetValue(_activeResearchId, out var currentActive))
        {
            currentActive.IsPaused = true;
        }

        _activeResearchId = techId;
        task.IsPaused = false;

        if (isNewTask)
        {
            ResearchStarted?.Invoke(node);
        }

        return true;
    }

    public bool SetActiveResearch(string techId)
    {
        if (string.IsNullOrWhiteSpace(techId)) return false;
        if (_unlocked.Contains(techId)) return false;
        return StartResearch(techId);
    }

    /// <summary>
    /// 取消当前的研究（不清空进度；若需要可把 keepProgress 设为 false）。
    /// </summary>
    public bool CancelResearch(string techId, bool keepProgress = true)
    {
        if (!_progressSnapshots.TryGetValue(techId, out var task)) return false;

        if (string.Equals(_activeResearchId, techId, StringComparison.OrdinalIgnoreCase))
        {
            _activeResearchId = string.Empty;
        }

        if (!keepProgress)
        {
            task.Accumulated = 0;
            _progressSnapshots.Remove(techId);
        }
        else
        {
            task.IsPaused = true;
        }
        return true;
    }

    /// <summary>
    /// 为指定节点投入研究点（例如每回合/每秒产出的科研值，仅对激活节点生效）。
    /// 当达到或超过 cost 时自动解锁。
    /// 返回：是否已在本次投放后解锁。
    /// </summary>
    public bool ContributeResearch(string techId, int points)
    {
        if (points <= 0) return false;
        if (_unlocked.Contains(techId)) return true; // 已解锁

        if (!_progressSnapshots.TryGetValue(techId, out var task))
        {
            // 如果依赖满足并且没在研究，允许快捷开始
            if (!StartResearch(techId)) return false;
            if (_unlocked.Contains(techId)) return true;
            if (!_progressSnapshots.TryGetValue(techId, out task)) return false;
        }

        if (!string.Equals(_activeResearchId, techId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        task.Accumulated += points;

        if (task.Accumulated >= task.Cost)
        {
            UnlockInternal(techId);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 批量投入研究点；当前实现仅作用于激活中的节点。
    /// </summary>
    public void DistributeResearchPoints(int totalPoints)
    {
        if (totalPoints <= 0) return;
        if (string.IsNullOrEmpty(_activeResearchId)) return;

        ContributeResearch(_activeResearchId, totalPoints);
    }


    /// <summary>
    /// 向当前激活的研究投入指定科研点并返回是否完成。
    /// 返回：true 表示本次投入后解锁该科技。
    /// </summary>
    public bool AddProgressToActiveResearch(int researchPoints)
    {
        if (researchPoints <= 0) return false;
        if (string.IsNullOrEmpty(_activeResearchId)) return false;

        return ContributeResearch(_activeResearchId, researchPoints);
    }


    /// <summary>
    /// 直接解锁（用于剧情/奖励等）。
    /// </summary>
    public bool ForceUnlock(string techId)
    {
        if (!_DicNodesID.ContainsKey(techId)) return false;
        UnlockInternal(techId);
        return true;
    }

    /// <summary>
    /// （存档）导出当前状态：已解锁集合 + 正在研究的进度
    /// </summary>
    public TechSystemSaveData Save()
    {
        var sd = new TechSystemSaveData
        {
            unlocked = _unlocked.ToList(),
            researching = _progressSnapshots.Values.Select(r => new TechSystemSaveData.ResearchingItem
            {
                id = r.Node.id,
                accumulated = r.Accumulated,
                paused = r.IsPaused
            }).ToList(),
            activeResearchId = _activeResearchId
        };
        return sd;
    }

    /// <summary>
    /// （读档）导入状态：恢复已解锁与在研进度（需在 Init 之后调用）
    /// </summary>
    public void Load(TechSystemSaveData data)
    {
        if (data == null) return;

        _unlocked.Clear();
        foreach (var id in data.unlocked)
            if (_DicNodesID.ContainsKey(id)) _unlocked.Add(id);

        _progressSnapshots.Clear();
        _activeResearchId = string.Empty;
        if (data.researching != null)
        {
            foreach (var item in data.researching)
            {
                if (!_DicNodesID.TryGetValue(item.id, out var node)) continue;
                if (_unlocked.Contains(item.id)) continue; // 已经解锁则跳过
                var task = new ResearchProgressSnapshot(node)
                {
                    Accumulated = Math.Max(0, item.accumulated),
                    IsPaused = item.paused
                };
                if (task.Accumulated >= task.Cost)
                {
                    UnlockInternal(item.id);
                }
                else
                {
                    _progressSnapshots[item.id] = task;
                }
            }
        }

        if (!string.IsNullOrEmpty(data.activeResearchId) &&
            _progressSnapshots.TryGetValue(data.activeResearchId, out var activeTask))
        {
            _activeResearchId = data.activeResearchId;
            activeTask.IsPaused = false;
            foreach (var kv in _progressSnapshots)
            {
                if (!string.Equals(kv.Key, _activeResearchId, StringComparison.OrdinalIgnoreCase))
                {
                    kv.Value.IsPaused = true;
                }
            }
        }
        else
        {
            foreach (var snapshot in _progressSnapshots.Values)
            {
                snapshot.IsPaused = true;
            }
        }
    }

    //========================== 查询/工具 ==========================

    public bool HasNode(string id) => !string.IsNullOrWhiteSpace(id) && _DicNodesID.ContainsKey(id);

    public bool TryGetNode(string id, out TechNodeData node) =>
        _DicNodesID.TryGetValue(id ?? string.Empty, out node);

    public IEnumerable<TechNodeData> GetAllNodes()
    {
      
       return _DicNodesID.Values;
    }

    public IReadOnlyCollection<string> GetUnlockedIds() => _unlocked;

    public bool IsResearching(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        return _progressSnapshots.ContainsKey(id);
    }

//========================== 内部实现 ==========================

    private void UnlockInternal(string techId)
    {
        if (!_unlocked.Add(techId))
        {
            _progressSnapshots.Remove(techId);
            if (string.Equals(_activeResearchId, techId, StringComparison.OrdinalIgnoreCase))
            {
                _activeResearchId = string.Empty;
            }
            return;
        }

        _progressSnapshots.Remove(techId);
        if (string.Equals(_activeResearchId, techId, StringComparison.OrdinalIgnoreCase))
        {
            _activeResearchId = string.Empty;
        }
        if (_DicNodesID.TryGetValue(techId,out var node))
        {
            ResearchCompleted?.Invoke(node);
        }

    }

    private void EnsureTreeBound()
    {
        _treeAssets = ResourceRouting.Instance.treeAssets;
        if (_treeAssets == null)
            throw new InvalidOperationException($"{nameof(TechTreeManager)} 还未 Init，请先调用 Init(TechTreeAssets ...)。");
    }

    private string FindFirstRootId()
    {
        foreach (var kv in _DicNodesID)
        {
            var node = kv.Value;
            if (node.dependencies == null || node.dependencies.Count == 0)
                return node.id;
        }
        return string.Empty;
    }

   
    //========================== 内部结构体 & 存档结构 ==========================

    private sealed class ResearchProgressSnapshot
    {
        public TechNodeData Node { get; }
        public int Cost { get; }
        public int Accumulated;
        public bool IsPaused;

        public float ProgressRatio => Cost <= 0 ? 1f : Mathf.Clamp01(Accumulated / (float)Cost);

        public ResearchProgressSnapshot(TechNodeData node)
        {
            Node = node ?? throw new ArgumentNullException(nameof(node));
            Cost = Math.Max(0, node.cost);
            Accumulated = 0;
            IsPaused = false;
        }
    }

    [Serializable]
    public class TechSystemSaveData
    {
        public List<string> unlocked = new List<string>();

        [Serializable]
        public class ResearchingItem
        {
            public string id;
            public int accumulated;
            public bool paused;
        }

        public List<ResearchingItem> researching = new List<ResearchingItem>();
        public string activeResearchId = string.Empty;
    }
}
