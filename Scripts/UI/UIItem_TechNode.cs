using System;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIItem_TechNode : MonoBehaviour
{
    [SerializeField,LabelText("当前节点")]
    private Button btn_TheNode;
    [SerializeField,LabelText("节点图标")]
    private Image img_TechIcon;
    [SerializeField,LabelText("节点名称")]
    private TMP_Text text_NodeName;
    [SerializeField,LabelText("研究进度条")]
    private Slider slider_ResearchProgress;
    [SerializeField,LabelText("节点描述")]
    private TMP_Text text_NodeDescription;

    [SerializeField,LabelText("连线入口点")]
    private RectTransform linePoint_Enter;
    [SerializeField,LabelText("连线出口")]
    private RectTransform linePoint_Export;



    [SerializeField, LabelText("匹配的节点ID")]
    private string _nodeId;

    


    private TechNodeData _data;
    private TechTreeManager _manager;
    private Action<string> _onRequestResearch;

    private bool _hasWarnedEmptyId;
    private string _lastWarnedMissingNodeId = string.Empty;

    public RectTransform LinePointEnter => linePoint_Enter;
    public RectTransform LinePointExport => linePoint_Export;

    public string NodeId => _nodeId;

    public void Bind(TechTreeManager manager, Action<string> onRequestResearch)
    {
        _manager = manager;
        _onRequestResearch = onRequestResearch;

        if (btn_TheNode != null)
        {
            btn_TheNode.onClick.RemoveListener(OnNodeButtonClicked);
            btn_TheNode.onClick.AddListener(OnNodeButtonClicked);
        }

        UpdateStaticInfo();
    }

    public void Refresh(bool canResearch, bool isResearching, float progress, bool isUnlocked)
    {
        if (!TryResolveData())
        {
            ApplyUnavailableState();
            return;
        }

        if (slider_ResearchProgress != null)
        {
            float clampedProgress = Mathf.Clamp01(progress);
            slider_ResearchProgress.value = isUnlocked ? 1f : clampedProgress;
            bool showProgress = isResearching || isUnlocked || clampedProgress > 0f;
            slider_ResearchProgress.gameObject.SetActive(showProgress);
        }

        if (btn_TheNode != null)
        {
            bool interactable = canResearch && !isUnlocked && _manager != null;
            btn_TheNode.interactable = interactable;
        }
    }

    private void OnNodeButtonClicked()
    {
        if (!TryResolveData())
        {
            return;
        }

        _onRequestResearch?.Invoke(_nodeId);
    }

    private void OnDestroy()
    {
        if (btn_TheNode != null)
        {
            btn_TheNode.onClick.RemoveListener(OnNodeButtonClicked);
        }
    }

    private bool TryResolveData()
    {
        if (string.IsNullOrWhiteSpace(_nodeId))
        {
            if (!_hasWarnedEmptyId)
            {
                Debug.LogWarning($"[{nameof(UIItem_TechNode)}] 节点 {name} 未配置节点ID。", this);
                _hasWarnedEmptyId = true;
            }
            _data = null;
            return false;
        }

        _hasWarnedEmptyId = false;

        if (_manager == null)
        {
            _data = null;
            return false;
        }

        if (!_manager.TryGetNode(_nodeId, out var nodeData) || nodeData == null)
        {
            if (!string.Equals(_lastWarnedMissingNodeId, _nodeId, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"[{nameof(UIItem_TechNode)}] 节点ID {_nodeId} 在科技树中未找到（{name}）。", this);
                _lastWarnedMissingNodeId = _nodeId;
            }
            _data = null;
            return false;
        }

        _lastWarnedMissingNodeId = string.Empty;
        _data = nodeData;
        return true;
    }

    private void UpdateStaticInfo()
    {
        if (!TryResolveData())
        {
            ClearStaticInfo();
            return;
        }

        if (text_NodeName != null)
        {
            text_NodeName.text = _data != null ? _data.name : string.Empty;
        }

        if (text_NodeDescription != null)
        {
            text_NodeDescription.text = _data != null ? _data.description : string.Empty;
        }

        if (img_TechIcon != null)
        {
            img_TechIcon.sprite = _data != null ? _data.icon : null;
            img_TechIcon.enabled = img_TechIcon.sprite != null;
        }
    }

    private void ClearStaticInfo()
    {
        if (text_NodeName != null)
        {
            text_NodeName.text = string.Empty;
        }

        if (text_NodeDescription != null)
        {
            text_NodeDescription.text = string.Empty;
        }

        if (img_TechIcon != null)
        {
            img_TechIcon.sprite = null;
            img_TechIcon.enabled = false;
        }
    }

    private void ApplyUnavailableState()
    {
        if (slider_ResearchProgress != null)
        {
            slider_ResearchProgress.gameObject.SetActive(false);
        }

        if (btn_TheNode != null)
        {
            btn_TheNode.interactable = false;
        }
    }
}
