using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;
using System.Text.RegularExpressions;
using System.Text;
using System.IO;

public class TechTreeEditorWindow : EditorWindow
{
    [MenuItem("SSBX/Tech Tree Editor")]
    public static void OpenWindow()
    {
        var win = GetWindow<TechTreeEditorWindow>();
        win.titleContent = new GUIContent("Tech Tree Editor");
        win.Show();
    }

    private const string PrefKey_LastTreeGuid = "LifeOn.TechTreeEditor.LastTreeGuid";

    [SerializeField] private TechTreeAssets _currentTree;
    [SerializeField] private bool _isDirty;

    private TechTreeGraphView _graphView;
    private ObjectField _treeField;

    private void OnEnable()
    {
        if (_currentTree == null)
        {
            var guid = EditorPrefs.GetString(PrefKey_LastTreeGuid, string.Empty);
            if (!string.IsNullOrEmpty(guid))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<TechTreeAssets>(path);
                if (asset != null) _currentTree = asset;
            }
        }
        Undo.undoRedoPerformed += OnUndoRedoPerformed;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;

        if (_currentTree != null && _isDirty)
        {
            bool ok = EditorUtility.DisplayDialog("保存更改？",
                "科技树有未保存的更改，是否保存？", "保存", "不保存");
            if (ok) SaveWithValidation();
        }
    }

    private void MarkDirty()
    {
        _isDirty = true;
        if (titleContent != null) titleContent.text = "Tech Tree Editor *";
    }

    private void ClearDirty()
    {
        _isDirty = false;
        if (titleContent != null) titleContent.text = "Tech Tree Editor";
    }

    public void CreateGUI()
    {
        rootVisualElement.style.flexDirection = FlexDirection.Column;
        rootVisualElement.style.flexGrow = 1f;

        var toolbar = new Toolbar();

        _treeField = new ObjectField("TechTreeAssets 资产")
        {
            objectType = typeof(TechTreeAssets),
            allowSceneObjects = false,
            value = _currentTree
        };

        _treeField.style.width = 300;

        _treeField.RegisterValueChangedCallback(evt =>
        {
            var newTree = evt.newValue as TechTreeAssets;
            if (newTree != _currentTree)
            {
                _currentTree = newTree;

                if (_currentTree != null)
                {
                    var path = AssetDatabase.GetAssetPath(_currentTree);
                    var guid = AssetDatabase.AssetPathToGUID(path);
                    EditorPrefs.SetString(PrefKey_LastTreeGuid, guid);
                }
                else
                {
                    EditorPrefs.DeleteKey(PrefKey_LastTreeGuid);
                }

                RebuildGraphView();
            }
        });
        toolbar.Add(_treeField);

        //生成保存按钮
        ToolbarButton saveBtn = new ToolbarButton(() => SaveWithValidation()) { text = "保存 (Ctrl/Cmd + S)" };
        toolbar.Add(saveBtn);

        //生成枚举按钮

        ToolbarButton genEnumBtn = new ToolbarButton(GenerateTechIDEnum){ text = "生成 TechIDEnum" };
        toolbar.Add(genEnumBtn);

        rootVisualElement.Add(toolbar);

        // 捕获 Ctrl/Cmd + S（UIElements）
        rootVisualElement.RegisterCallback<KeyDownEvent>(evt =>
        {
            if ((evt.ctrlKey || evt.commandKey) && evt.keyCode == KeyCode.S)
            {
                SaveWithValidation();
                evt.StopImmediatePropagation();
            }
        }, TrickleDown.TrickleDown);

        if (_currentTree == null)
        {
            var tip = new Label("请在上方选择一个 TechTreeAssets 资产进行编辑。");
            tip.style.flexGrow = 1f;
            tip.style.unityTextAlign = TextAnchor.MiddleCenter;
            rootVisualElement.Add(tip);
        }
        else
        {
            RebuildGraphView();
        }
    }

    // 兜底捕获 Ctrl/Cmd + S（IMGUI）
    private void OnGUI()
    {
        var e = Event.current;
        if (e != null && e.type == EventType.KeyDown && (e.control || e.command) && e.keyCode == KeyCode.S)
        {
            SaveWithValidation();
            e.Use();
        }
    }

    private void OnUndoRedoPerformed()
    {
        // 避免当帧 UI 重建引发异常
        EditorApplication.delayCall += () =>
        {
            _graphView?.ReloadFromTreeData();
            Repaint();
        };
    }

    private void RebuildGraphView()
    {
        if (_graphView != null)
        {
            rootVisualElement.Remove(_graphView);
            _graphView = null;
        }

        // 清掉 Toolbar 以外的旧元素（Toolbar 在索引 0）
        for (int i = rootVisualElement.childCount - 1; i >= 1; i--)
            rootVisualElement.RemoveAt(i);

        if (_currentTree == null) return;

        // 传入通知委托，GraphView 内部用它来显示提示
        _graphView = new TechTreeGraphView(
            _currentTree,
            MarkDirty,
            msg => ShowNotification(new GUIContent(msg))
        );
        //_graphView.StretchToParentSize();
        _graphView.style.flexGrow = 1;
        rootVisualElement.Add(_graphView);

        ClearDirty();
    }

    // —— 保存 & 校验 —— //
    private void SaveWithValidation()
    {
        if (_currentTree == null)
        {
            ShowNotification(new GUIContent("未选择 TechTreeAssets 资产"));
            return;
        }

        // 空ID检测
        var empties = _currentTree.techList
            .Where(t => t == null || string.IsNullOrWhiteSpace(t.id))
            .Select(t => t?.name ?? "(未命名)")
            .ToList();
        if (empties.Count > 0)
        {
            EditorUtility.DisplayDialog("保存失败：存在空ID",
                "以下节点ID为空，请填写后再保存：\n" + string.Join("\n", empties), "好的");
            return;
        }

        // 重复ID检测（忽略大小写）
        var dup = _currentTree.techList
            .GroupBy(t => t.id.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .OrderBy(x => x)
            .ToList();

        if (dup.Count > 0)
        {
            EditorUtility.DisplayDialog("保存失败：存在重复的科技ID",
                "以下 ID 出现重复：\n" + string.Join(", ", dup) + "\n\n请修正后再保存。", "好的");
            return;
        }

        // 环依赖检测
        if (HasCycle(_currentTree, out var cyclePath))
        {
            EditorUtility.DisplayDialog("保存失败：存在环依赖",
                "发现循环依赖（示例路径）：\n" + string.Join(" -> ", cyclePath), "好的");
            return;
        }

        EditorUtility.SetDirty(_currentTree);
        AssetDatabase.SaveAssets();
        ClearDirty();
        ShowNotification(new GUIContent("保存成功"));
    }

    // DFS 检测环
    private static bool HasCycle(TechTreeAssets tree, out List<string> cyclePath)
    {
        cyclePath = null;

        var idToNode = tree.techList
            .Where(t => t != null && !string.IsNullOrEmpty(t.id))
            .ToDictionary(t => t.id, StringComparer.OrdinalIgnoreCase);

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var parent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        List<string> foundCycle = null;

        bool Dfs(string id)
        {
            visited.Add(id);
            stack.Add(id);

            var node = idToNode[id];
            var deps = node.dependencies;
            if (deps == null)
            {
                deps = new List<string>();
                node.dependencies = deps;
            }

            foreach (var dep in deps)
            {
                if (string.IsNullOrEmpty(dep) || !idToNode.ContainsKey(dep)) continue;

                if (!visited.Contains(dep))
                {
                    parent[dep] = id;
                    if (Dfs(dep)) return true;
                }
                else if (stack.Contains(dep))
                {
                    // 构造环路
                    var path = new List<string> { dep };
                    var cur = id;
                    while (!string.Equals(cur, dep, StringComparison.OrdinalIgnoreCase))
                    {
                        path.Add(cur);
                        if (!parent.TryGetValue(cur, out cur))
                        {
                            cur = dep;
                            break;
                        }
                    }
                    path.Reverse();
                    foundCycle = path;
                    return true;
                }
            }

            stack.Remove(id);
            return false;
        }

        foreach (var id in idToNode.Keys)
        {
            if (!visited.Contains(id) && Dfs(id))
            {
                cyclePath = foundCycle ?? new List<string>();
                return true;
            }
        }
        return false;
    }

    //==================== GraphView ====================

    private class TechTreeGraphView : GraphView
    {
        private readonly TechTreeAssets _tree;
        private readonly Action _markDirty;
        private readonly Action<string> _notify; // 通知委托

        internal readonly PortEdgeConnectorListener edgeConnectorListener;

        private readonly Dictionary<string, TechNodeView> _nodeViews =
            new Dictionary<string, TechNodeView>(StringComparer.OrdinalIgnoreCase);

        private readonly Vector2 _defaultNodeSize = new Vector2(240, 200);

        // 重建/程序化操作时抑制 Undo & 图变化副作用
        private bool _reloading = false;
        private bool _suppressGraphChanges = false;

        public TechTreeGraphView(TechTreeAssets tree, Action markDirty, Action<string> notify)
        {
            _tree = tree;
            _markDirty = markDirty;
            _notify = notify;

            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            graphViewChanged += OnGraphViewChanged;

            // Delete 键删除
            this.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace)
                {
                    DeleteSelection();
                    evt.StopImmediatePropagation();
                }
            });

            // 端口拖拽监听
            edgeConnectorListener = new PortEdgeConnectorListener(this);

            // 空白处点击创建
            nodeCreationRequest = ctx =>
            {
                Vector2 pos = contentViewContainer.WorldToLocal(ctx.screenMousePosition);
                CreateTechNodeAt(pos);
            };

            ReloadFromTreeData();
        }

        internal void Notify(string msg) => _notify?.Invoke(msg); // 供子视图调用

        public void ReloadFromTreeData()
        {
            _reloading = true;
            _suppressGraphChanges = true;

            // 清空现有图元素（仅移除图形元素）
            foreach (var e in graphElements.ToList())
                RemoveElement(e);

            _nodeViews.Clear();

            // 创建节点
            foreach (var tech in _tree.techList)
            {
                if (tech == null) continue;
                var nv = new TechNodeView(tech, _tree, this);
                nv.SetPosition(new Rect(tech.position, _defaultNodeSize));
                AddElement(nv);
                if (!string.IsNullOrEmpty(tech.id))
                    _nodeViews[tech.id] = nv;
            }

            // 创建连线
            foreach (var tech in _tree.techList)
            {
                if (tech == null) continue;
                var deps = tech.dependencies ?? (tech.dependencies = new List<string>());
                foreach (var depId in deps)
                {
                    if (string.IsNullOrEmpty(depId)) continue;
                    if (_nodeViews.TryGetValue(depId, out var from) &&
                        _nodeViews.TryGetValue(tech.id, out var to))
                    {
                        var edge = from.outputPort.ConnectTo(to.inputPort);
                        AddElement(edge);
                    }
                }
            }

            _suppressGraphChanges = false;
            _reloading = false;
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);
            if (evt.target is GraphView || evt.target is GridBackground)
            {
                evt.menu.AppendAction("添加新科技节点", _ =>
                {
                    Vector2 pos = contentViewContainer.WorldToLocal(evt.mousePosition);
                    CreateTechNodeAt(pos);
                });
                evt.menu.AppendAction("Frame All", _ => FrameAll());
            }
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatible = new List<Port>();
            foreach (var p in ports)
            {
                if (p != startPort && p.node != startPort.node &&
                    p.direction != startPort.direction &&
                    p.portType == startPort.portType)
                {
                    compatible.Add(p);
                }
            }
            return compatible;
        }

        private void CreateTechNodeAt(Vector2 graphPos)
        {
            Undo.RecordObject(_tree, "Add Tech Node");

            // 只使用 AddTech() 生成的 ID（不再进行二次修改）
            var newTech = _tree.AddTech("新科技", "", 0, null);
            newTech.name = $"新科技 {newTech.id}";
            newTech.position = graphPos;
            newTech.dependencies ??= new List<string>();

            var nv = new TechNodeView(newTech, _tree, this);
            nv.SetPosition(new Rect(graphPos, _defaultNodeSize));
            AddElement(nv);
            _nodeViews[newTech.id] = nv;

            SetDirty();
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (_suppressGraphChanges) return change;

            // 这里只处理用户删除（增连线在 OnDrop 中处理）
            if (change.elementsToRemove != null)
            {
                foreach (var e in change.elementsToRemove)
                {
                    if (e is Edge edge)
                    {
                        var from = edge.output.node as TechNodeView;
                        var to = edge.input.node as TechNodeView;
                        if (from != null && to != null)
                        {
                            Undo.RecordObject(_tree, "Remove Dependency");
                            _tree.RemoveDependency(from.techData.id, to.techData.id);
                            SetDirty();
                        }
                    }
                    else if (e is TechNodeView nv)
                    {
                        Undo.RecordObject(_tree, "Remove Tech Node");
                        string id = nv.techData.id;
                        _tree.RemoveTech(id);
                        _nodeViews.Remove(id);
                        SetDirty();
                    }
                }
            }
            return change;
        }

        internal void SetDirty()
        {
            _markDirty?.Invoke();
            EditorUtility.SetDirty(_tree);
        }

        // —— 端口拖拽监听：支持端口对端口、端口到空白 —— //
        internal class PortEdgeConnectorListener : IEdgeConnectorListener
        {
            private readonly TechTreeGraphView _view;
            public PortEdgeConnectorListener(TechTreeGraphView view) { _view = view; }

            // 端口对端口
            public void OnDrop(GraphView graphView, Edge tempEdge)
            {
                var outNode = tempEdge.output?.node as TechNodeView;
                var inNode = tempEdge.input?.node as TechNodeView;
                if (outNode == null || inNode == null) return;
                if (outNode == inNode) return;

                inNode.techData.dependencies ??= new List<string>();

                // 防重复依赖：如果数据已有依赖但界面缺线，补线即可
                if (inNode.techData.dependencies.Any(d => string.Equals(d, outNode.techData.id, StringComparison.OrdinalIgnoreCase)))
                {
                    var already = outNode.outputPort.connections.Any(e => e.input == inNode.inputPort);
                    if (!already)
                    {
                        var edge = outNode.outputPort.ConnectTo(inNode.inputPort);
                        graphView.AddElement(edge);
                    }
                    return;
                }

                if (string.IsNullOrEmpty(outNode.techData.id) || string.IsNullOrEmpty(inNode.techData.id))
                    return;

                Undo.RecordObject(_view._tree, "Add Dependency");
                if (_view._tree.AddDependency(outNode.techData.id, inNode.techData.id))
                {
                    _view.SetDirty();
                    var newEdge = outNode.outputPort.ConnectTo(inNode.inputPort);
                    graphView.AddElement(newEdge);
                }
                else
                {
                    _view.Notify("添加依赖失败（ID 无效、重复或自依赖）");
                }
            }

            // 端口到空白：新建节点并连接
            public void OnDropOutsidePort(Edge edge, Vector2 position)
            {
                Vector2 graphPos = _view.contentViewContainer.WorldToLocal(position);

                Undo.RecordObject(_view._tree, "Create Node By Drag");

                // 只使用 AddTech() 生成的 ID（不再进行二次修改）
                var newTech = _view._tree.AddTech("新科技", "", 0, null);
                newTech.name = $"新科技 {newTech.id}";
                newTech.position = graphPos;
                newTech.dependencies ??= new List<string>();

                var newNode = new TechNodeView(newTech, _view._tree, _view);
                newNode.SetPosition(new Rect(graphPos, _view._defaultNodeSize));
                _view.AddElement(newNode);
                _view._nodeViews[newTech.id] = newNode;

                bool linked = false;
                if (edge.output != null && edge.output.node is TechNodeView from)
                {
                    if (_view._tree.AddDependency(from.techData.id, newTech.id))
                    {
                        var newEdge = from.outputPort.ConnectTo(newNode.inputPort);
                        _view.AddElement(newEdge);
                        linked = true;
                    }
                }
                else if (edge.input != null && edge.input.node is TechNodeView to)
                {
                    if (_view._tree.AddDependency(newTech.id, to.techData.id))
                    {
                        var newEdge = newNode.outputPort.ConnectTo(to.inputPort);
                        _view.AddElement(newEdge);
                        linked = true;
                    }
                }

                if (!linked)
                    _view.Notify("添加依赖失败（ID 无效、重复或自依赖）");

                _view.SetDirty();
            }
        }

        //==================== 节点视图 ====================
        private class TechNodeView : Node
        {
            public TechNodeData techData { get; private set; }
            public Port inputPort { get; private set; }
            public Port outputPort { get; private set; }

            private readonly TechTreeAssets _tree;
            private readonly TechTreeGraphView _owner;

            private Image _iconImage;

            public TechNodeView(TechNodeData data, TechTreeAssets tree, TechTreeGraphView owner)
            {
                techData = data;
                _tree = tree;
                _owner = owner;

                title = string.IsNullOrEmpty(data.name) ? $"Tech {data.id}" : data.name;
                tooltip = data.description;

                capabilities |= Capabilities.Movable | Capabilities.Deletable | Capabilities.Selectable;

                inputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
                inputPort.portName = "";
                inputPort.AddManipulator(new EdgeConnector<Edge>(_owner.edgeConnectorListener));
                inputContainer.Add(inputPort);

                outputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
                outputPort.portName = "";
                outputPort.AddManipulator(new EdgeConnector<Edge>(_owner.edgeConnectorListener));
                outputContainer.Add(outputPort);

                _iconImage = new Image { style = { width = 18, height = 18 } };
                if (data.icon) _iconImage.image = data.icon.texture;
                titleContainer.Insert(0, _iconImage);

                // ===== 可编辑字段（延迟提交，避免 Undo 过于频繁） =====

                var idField = new TextField("科技ID") { value = data.id, isDelayed = true };
                idField.RegisterValueChangedCallback(evt =>
                {
                    var newId = (evt.newValue ?? "").Trim();
                    var oldId = techData.id;

                    if (newId.Equals(oldId, StringComparison.OrdinalIgnoreCase)) return;
                    if (string.IsNullOrWhiteSpace(newId))
                    {
                        idField.SetValueWithoutNotify(oldId);
                        _owner.Notify("ID 不能为空");
                        return;
                    }

                    bool duplicate = _tree.techList.Any(n =>
                        !ReferenceEquals(n, techData) &&
                        n != null &&
                        !string.IsNullOrEmpty(n.id) &&
                        newId.Equals(n.id, StringComparison.OrdinalIgnoreCase));

                    if (duplicate)
                    {
                        idField.SetValueWithoutNotify(oldId);
                        _owner.Notify($"ID \"{newId}\" 已存在");
                        return;
                    }

                    Undo.RecordObject(_tree, "Edit Tech ID");

                    techData.id = newId;

                    // 更新所有依赖中的引用
                    foreach (var n in _tree.techList)
                    {
                        if (n == null || n.dependencies == null) continue;
                        for (int i = 0; i < n.dependencies.Count; i++)
                            if (string.Equals(n.dependencies[i], oldId, StringComparison.OrdinalIgnoreCase))
                                n.dependencies[i] = newId;
                    }

                    // 同步更新字典（关键）
                    _tree.NotifyIdChanged(oldId, newId, techData);

                    // 更新视图索引
                    if (_owner._nodeViews.ContainsKey(oldId))
                    {
                        _owner._nodeViews.Remove(oldId);
                        _owner._nodeViews[newId] = this;
                    }

                    _owner.SetDirty();
                });
                extensionContainer.Add(idField);

                var nameField = new TextField("科技名称") { value = data.name, isDelayed = true };
                nameField.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue == techData.name) return;
                    Undo.RecordObject(_tree, "Edit Tech Name");
                    techData.name = evt.newValue;
                    title = string.IsNullOrEmpty(evt.newValue) ? $"Tech {techData.id}" : evt.newValue;
                    _owner.SetDirty();
                });
                extensionContainer.Add(nameField);

                var descField = new TextField("描述") { value = data.description, multiline = true, isDelayed = true };
                descField.style.minHeight = 60;
                descField.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue == techData.description) return;
                    Undo.RecordObject(_tree, "Edit Tech Description");
                    techData.description = evt.newValue;
                    tooltip = evt.newValue;
                    _owner.SetDirty();
                });
                extensionContainer.Add(descField);

                var iconField = new ObjectField("科技图标") { objectType = typeof(Sprite), value = data.icon };
                iconField.RegisterValueChangedCallback(evt =>
                {
                    var newSprite = evt.newValue as Sprite;
                    if (newSprite == techData.icon) return;
                    Undo.RecordObject(_tree, "Edit Tech Icon");
                    techData.icon = newSprite;
                    _iconImage.image = techData.icon ? techData.icon.texture : null;
                    _owner.SetDirty();
                });
                extensionContainer.Add(iconField);

                var costField = new IntegerField("需要的科研点") { value = data.cost, isDelayed = true };
                costField.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue == techData.cost) return;
                    Undo.RecordObject(_tree, "Edit Tech Cost");
                    techData.cost = evt.newValue;
                    _owner.SetDirty();
                });
                extensionContainer.Add(costField);

                RefreshExpandedState();
                RefreshPorts();
            }

            public override void SetPosition(Rect newPos)
            {
                base.SetPosition(newPos);

                if (_owner._reloading) return;

                if (techData.position != newPos.position)
                {
                    Undo.RecordObject(_tree, "Move Tech Node");
                    techData.position = newPos.position;
                    _owner.SetDirty();
                }
            }
        }
    }


    private void GenerateTechIDEnum()
    {
        if (_currentTree == null)
        {
            ShowNotification(new GUIContent("未选择资产，无法生成"));
            return;
        }

        string relativeDir = "Assets/Scripts/AutoGenerate";
        string fullDir = Path.Combine(Application.dataPath, "Scripts/AutoGenerate");

        if (!Directory.Exists(fullDir))
        {
            Directory.CreateDirectory(fullDir);
        }

        string filePath = Path.Combine(fullDir, "TechIDEnum_Auto.cs");

        var validEntries = new Dictionary<string, string>();

        foreach (var tech in _currentTree.techList)
        {
            if (tech == null || string.IsNullOrWhiteSpace(tech.id)) continue;

            string rawId = tech.id.Trim();

            // ================= 修改重点 =================
            // 原代码: @"[^a-zA-Z0-9_]" (只允许英文和数字)
            // 新代码: @"[^\w]" 
            // 说明: \w 在 C# 中匹配所有单词字符(包括中文)，
            // [^\w] 意味着只替换标点符号、空格等特殊字符，而保留中文。
            string enumName = Regex.Replace(rawId, @"[^\w]", "_");
            // ===========================================

            // 如果是以数字开头，在前面加下划线 (C# 变量不能以数字开头)
            if (string.IsNullOrEmpty(enumName) || char.IsDigit(enumName[0]))
            {
                enumName = "_" + enumName;
            }

            if (!validEntries.ContainsKey(enumName))
            {
                validEntries.Add(enumName, rawId);
            }
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("// =========================================================");
        sb.AppendLine("//  此文件由 TechTreeEditorWindow 自动生成，请勿手动修改。");
        sb.AppendLine($"//  生成时间: {DateTime.Now}");
        sb.AppendLine("// =========================================================");
        sb.AppendLine();
        sb.AppendLine("public enum TechIDEnum");
        sb.AppendLine("{");

        sb.AppendLine("    None = 0,");

        foreach (var kvp in validEntries)
        {
            if (kvp.Key != kvp.Value)
            {
                sb.AppendLine($"    // Original ID: {kvp.Value}");
            }
            sb.AppendLine($"    {kvp.Key},");
        }

        sb.AppendLine("}");

        try
        {
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();
            ShowNotification(new GUIContent("Enum 生成成功！"));
            Debug.Log($"TechIDEnum generated at: {filePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"生成 TechIDEnum 失败: {e.Message}");
            ShowNotification(new GUIContent("生成失败，看控制台"));
        }
    }


}
