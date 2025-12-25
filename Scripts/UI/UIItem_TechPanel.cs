using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

public class UIItem_TechPanel : MonoBehaviour
{
    [SerializeField, LabelText("节点容器")]
    private RectTransform _nodeContainer;

    private readonly Dictionary<string, UIItem_TechNode> _nodeViews = new Dictionary<string, UIItem_TechNode>(StringComparer.OrdinalIgnoreCase);

    private readonly List<UIItem_TechNode> _nodeItems = new List<UIItem_TechNode>();

    private TechTreeManager _techTree;

    public IReadOnlyDictionary<string, UIItem_TechNode> NodeViews => _nodeViews;

    private void Awake()
    {
        EnsureManager();
        RebuildNodeCollections();
    }

    private void OnEnable()
    {
        RefreshAllNodes();
    }

    public void OnRequestResearch(string techId)
    {
        if (string.IsNullOrWhiteSpace(techId))
        {
            return;
        }

        EnsureManager();
        if (_techTree == null)
        {
            return;
        }

        if (_techTree.SetActiveResearch(techId))
        {
            RefreshAllNodes();
        }
    }

    public void RefreshAllNodes()
    {
        EnsureManager();
        RebuildNodeCollections();

        HashSet<string> seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in _nodeItems)
        {
            if (node == null)
            {
                continue;
            }

            var nodeId = node.NodeId;
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                Debug.LogWarning($"[{nameof(UIItem_TechPanel)}] 节点 {node.name} 未配置节点ID。因为节点节点id为空", node);
                continue;
            }

            if (!seenIds.Add(nodeId))
            {
                Debug.LogWarning($"[{nameof(UIItem_TechPanel)}] 节点ID {nodeId} 存在重复，请检查摆放。", node);
            }

            if (_techTree != null && !_techTree.TryGetNode(nodeId, out _))
            {
                Debug.LogWarning($"[{nameof(UIItem_TechPanel)}] 节点ID {nodeId} 在科技树数据中不存在。", node);
            }
        }

        if (_techTree == null)
        {
            foreach (var node in _nodeItems)
            {
                node?.Refresh(false, false, 0f, false);
            }
            return;
        }

        IEnumerable<TechNodeData> allNodes = _techTree.GetAllNodes();
        if (allNodes != null)
        {
            
            
            foreach (TechNodeData nodeData in allNodes)
            {
                
                if (nodeData == null)
                {
                    Debug.LogWarning("nodeData为Null");
                    continue;
                }

                string nodeId = nodeData.id;
               
                if (string.IsNullOrWhiteSpace(nodeId))
                {
                    Debug.LogWarning("nodeID为空");
                    continue;
                }

                if (_nodeViews.ContainsKey(nodeId))
                {
                    continue;
                }

                string nodeName = string.IsNullOrWhiteSpace(nodeData.name) ? "<未命名>" : nodeData.name;
                Debug.LogWarning($"[{nameof(UIItem_TechPanel)}] 科技节点 {nodeId} ({nodeName}) 未找到对应的 UI 节点。",this);
            }
        }

        var activeId = _techTree.ActiveResearchId;
        var availableSet = new HashSet<string>(
            _techTree.GetResearchableNodes().Select(n => n.id),
            StringComparer.OrdinalIgnoreCase);

        foreach (var node in _nodeItems)
        {
            if (node == null)
            {
                continue;
            }

            var id = node.NodeId;
            if (string.IsNullOrWhiteSpace(id))
            {
                node.Refresh(false, false, 0f, false);
                continue;
            }

            if (!_techTree.TryGetNode(id, out _))
            {
                node.Refresh(false, false, 0f, false);
                continue;
            }

            bool isUnlocked = _techTree.IsUnlocked(id);
            bool isActive = !string.IsNullOrEmpty(activeId) &&
                            activeId.Equals(id, StringComparison.OrdinalIgnoreCase);
            bool canResearch = !isUnlocked &&
                               (availableSet.Contains(id) || _techTree.IsResearching(id));
            float progress = _techTree.GetResearchProgress(id);

            node.Refresh(canResearch, isActive, progress, isUnlocked);
        }
    }

    private void RebuildNodeCollections()
    {
        _nodeItems.Clear();

        if (_nodeContainer == null)
        {
            Debug.LogWarning($"[{nameof(UIItem_TechPanel)}] 未配置节点容器。", this);
            _nodeViews.Clear();
            return;
        }

        UIItem_TechNode[] nodes = _nodeContainer.GetComponentsInChildren<UIItem_TechNode>(true);
        _nodeItems.AddRange(nodes);

        _nodeViews.Clear();
        foreach (UIItem_TechNode node in _nodeItems)
        {
            if (node == null)
            {
                continue;
            }

            node.Bind(_techTree, OnRequestResearch);

            var nodeId = node.NodeId;
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                continue;
            }

            if (_nodeViews.ContainsKey(nodeId))
            {
                continue;
            }

            _nodeViews.Add(nodeId, node);
        }
    }

    private void EnsureManager()
    {
        if (_techTree == null)
        {
            _techTree = GameContext.Instance?.TechTree;
        }
    }
}
