using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moyo.Unity;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
/*

    UILayer各层级应该放置的UI示例：



    1. Background（背景层）

    - 游戏场景背景图

    - 环境装饰性UI元素

    - 远景UI（如远处的山脉、云层等）



    2. Scene（场景UI层）

    - 场景中的交互按钮（NPC对话按钮、场景物品交互）

    - 小地图、雷达图

    - 场景任务指引标记



    3. Normal（普通层）

    - 背包界面

    - 技能面板

    - 设置界面

    - 好友列表



    4. Main（主界面层）

    - 游戏主HUD（血量条、魔法条、经验条）

    - 角色状态栏

    - 快捷技能栏

    - 任务追踪界面



    5. Popup（弹窗层）

    - 确认对话框（"是否确认退出？"）

    - 物品详情窗口

    - 商店购买确认窗口

    - 系统设置弹窗



    6. Guide（引导层）

    - 新手引导箭头和高亮

    - 功能引导提示

    - 操作指引面板



    7. Notice（通知层）

    - 系统公告面板

    - 活动奖励领取窗口

    - 重要系统消息



    8. Toast（提示层）

    - 浮动提示信息（"获得金币+100"）

    - 小贴士文字提示

    - 非阻塞性状态提示



    9. Loading（加载层）

    - 游戏加载界面

    - 场景切换转场效果

    - 资源加载进度条



    10. DebugInfo（调试层）

    - 帧率显示

    - 调试信息面板

    - 开发者控制台

*/
namespace Moyo.Unity
{

    public class UIManager : MonoSingleton<UIManager>
    {
        [Serializable]
        public class UILayerConfig
        {
            public UILayer layerType;
            public string layerName;
            public int sortOrder;
            public bool isModal;
            public bool blocksRaycasts;
        }

        public enum UILayer
        {
            Background, Scene, Normal, Main, Popup, Guide, Notice, Toast, Loading, DebugInfo
        }

        [SerializeField]
        private UILayerConfig[] layerConfigs = {
        new UILayerConfig { layerType = UILayer.Background, layerName = "Background", sortOrder = 0, isModal = false, blocksRaycasts = false },
        new UILayerConfig { layerType = UILayer.Scene, layerName = "Scene", sortOrder = 1, isModal = false, blocksRaycasts = false },
        new UILayerConfig { layerType = UILayer.Normal, layerName = "Normal", sortOrder = 2, isModal = false, blocksRaycasts = true },
        new UILayerConfig { layerType = UILayer.Main, layerName = "Main", sortOrder = 3, isModal = false, blocksRaycasts = true },
        new UILayerConfig { layerType = UILayer.Popup, layerName = "Popup", sortOrder = 4, isModal = true, blocksRaycasts = true },
        new UILayerConfig { layerType = UILayer.Guide, layerName = "Guide", sortOrder = 5, isModal = true, blocksRaycasts = true },
        new UILayerConfig { layerType = UILayer.Notice, layerName = "Notice", sortOrder = 6, isModal = true, blocksRaycasts = true },
        new UILayerConfig { layerType = UILayer.Toast, layerName = "Toast", sortOrder = 7, isModal = false, blocksRaycasts = false },
        new UILayerConfig { layerType = UILayer.Loading, layerName = "Loading", sortOrder = 8, isModal = true, blocksRaycasts = true },
        new UILayerConfig { layerType = UILayer.DebugInfo, layerName = "DebugInfo", sortOrder = 9, isModal = false, blocksRaycasts = false }
    };

        private Dictionary<UILayer, Transform> layerParents = new Dictionary<UILayer, Transform>();
        private Dictionary<UILayer, CanvasGroup> layerCanvasGroups = new Dictionary<UILayer, CanvasGroup>();
        private Dictionary<UILayer, List<PanelBase>> activePanels = new Dictionary<UILayer, List<PanelBase>>();
        private Dictionary<Type, PanelBase> loadedPanels = new Dictionary<Type, PanelBase>();
        // --- 改进 ---：移除了冗余的`panelGameObjects`字典。我们可以从PanelBase组件获取GameObject。

        private UILayer? currentModalLayer = null;

        [LabelText("参考分辨率")]
        [SerializeField] private Vector2 canvasReferenceResolution = new Vector2(1920, 1080);



        [LabelText("同层仅显示一个面板")]
        [SerializeField] private bool enforceSinglePanelPerLayer = true;
        private Canvas mainCanvas;

        public Canvas GetMainCanvas()
        {
            return mainCanvas;
        }

        protected override void Awake()
        {
            base.Awake();
            InitializeLayers();

        }

        #region 初始化
        private void InitializeLayers()
        {
            SetupMainCanvas();
            layerConfigs = layerConfigs.OrderBy(c => c.sortOrder).ToArray();
            foreach (var config in layerConfigs)
            {
                CreateUILayer(config);
            }

        }

        private void SetupMainCanvas()
        {
            mainCanvas = GetComponent<Canvas>();
            if (mainCanvas == null)
            {
                mainCanvas = gameObject.AddComponent<Canvas>();
                mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var canvasScaler = gameObject.AddComponent<CanvasScaler>();
                canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasScaler.referenceResolution = canvasReferenceResolution;
                canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                canvasScaler.matchWidthOrHeight = 0.5f;
                gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        private void CreateUILayer(UILayerConfig config)
        {
            var layerObj = new GameObject(config.layerName);
            layerObj.transform.SetParent(transform);
            layerObj.transform.localPosition = Vector3.zero;
            layerObj.transform.localScale = Vector3.one;
            layerObj.transform.SetSiblingIndex(config.sortOrder);
            var rectTransform = layerObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;
            var canvasGroup = layerObj.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = config.blocksRaycasts;
            canvasGroup.interactable = true;
            layerParents[config.layerType] = layerObj.transform;
            layerCanvasGroups[config.layerType] = canvasGroup;
            activePanels[config.layerType] = new List<PanelBase>();
        }
        #endregion

        // --- 改进 ---：添加了`args`参数用于向面板传递数据。
        public async Task<T> ShowPanel<T>(UILayer layer, string address = null, params object[] args) where T : PanelBase
        {
            // 1) 基础校验：目标层必须已初始化
            if (!layerParents.ContainsKey(layer))
            {
                Debug.LogError($"层级 {layer} 未初始化！");
                return null;
            }

            Type panelType = typeof(T);

            // 2) 面板已加载：复用并切层、传参、显示
            if (loadedPanels.TryGetValue(panelType, out var existingPanel) && existingPanel != null)
            {
                // 若已加载但父节点不是目标层，先归层
                if (existingPanel.transform.parent != layerParents[layer])
                {
                    existingPanel.transform.SetParent(layerParents[layer], false);
                    UpdatePanelLayer(existingPanel, layer);
                }

                // 即使已存在也传入最新数据
                existingPanel.OnPanelCreated(args);

                // 同层只留一个激活面板
                if (enforceSinglePanelPerLayer)
                {
                    EnsureSinglePanelInLayer(layer, existingPanel);
                }
                else
                {
                    if (!activePanels[layer].Contains(existingPanel))
                        activePanels[layer].Add(existingPanel);
                }

                // 显示（携带参数）
                existingPanel.Show(args);

                

                // 模态层处理
                if (IsModalLayer(layer))
                {
                    SetModalLayer(layer);
                }

                return existingPanel as T;
            }

            // 3) 面板未加载：异步加载、挂载到层、记录、显示
            var newPanel = await LoadAndCreatePanel<T>(address, args);
            if (newPanel != null)
            {
                // 设置父节点为目标层
                newPanel.transform.SetParent(layerParents[layer], false);

                // 同层只留一个激活面板
                if (enforceSinglePanelPerLayer)
                {
                    EnsureSinglePanelInLayer(layer, newPanel);
                }
                else
                {
                    activePanels[layer].Add(newPanel);
                }

                // 记录已加载
                loadedPanels[panelType] = newPanel;

                // 模态层处理
                if (IsModalLayer(layer))
                {
                    SetModalLayer(layer);
                }

                // 显示（携带参数）
                newPanel.Show(args);
            }

            return newPanel;
        }

        private async Task<T> LoadAndCreatePanel<T>(string address = null, params object[] args) where T : PanelBase
        {
            address ??= typeof(T).Name;


            var panelAsset = await AssetsManager.Instance.LoadAssetAsync<GameObject>(address);
            if (panelAsset == null)
            {
                Debug.LogError($"加载UI面板预制体失败：{address}");
                return null;
            }
            var panelObj = Instantiate(panelAsset);
            panelObj.name = typeof(T).Name;
            var panelComponent = panelObj.GetComponent<T>() ?? panelObj.AddComponent<T>();

            // --- 改进 ---：调用创建生命周期方法。
            panelComponent.OnPanelCreated(args);

            return panelComponent;


        }

        public void HidePanel<T>() where T : PanelBase
        {
            if (loadedPanels.TryGetValue(typeof(T), out var panel) && panel != null && panel.gameObject.activeSelf)
            {
                panel.Hide();
                RemovePanelFromActiveList(panel);
                UpdateModalLayerState();
            }
        }



        public void DestroyPanel<T>() where T : PanelBase
        {
            Type panelType = typeof(T);
            if (loadedPanels.TryGetValue(panelType, out var panel) && panel != null)
            {
                RemovePanelFromActiveList(panel);
                loadedPanels.Remove(panelType);

                // --- 改进 ---：显式释放资源。
                // 具体实现取决于您的AssetsManager。
                string address = panel.name; // 假设地址与类名/游戏对象名相同。
                AssetsManager.Instance.ReleaseAsset(address);

                Destroy(panel.gameObject);
                UpdateModalLayerState();
            }
        }

        private void RemovePanelFromActiveList(PanelBase panel)
        {
            foreach (var layerList in activePanels.Values)
            {
                if (layerList.Contains(panel))
                {
                    layerList.Remove(panel);
                    return; // 假设一个面板只能在一个列表中。
                }
            }
        }

        private void UpdatePanelLayer(PanelBase panel, UILayer newLayer)
        {
            RemovePanelFromActiveList(panel);
            activePanels[newLayer].Add(panel);
        }

        #region 模态逻辑

        private void SetModalLayer(UILayer modalLayer)
        {
            if (!IsModalLayer(modalLayer)) return;

            currentModalLayer = modalLayer;
            int modalSortOrder = GetLayerSortOrder(modalLayer);

            foreach (var config in layerConfigs)
            {
                // 当前模态层以下的层级不可交互。
                // 模态层及以上的层级保持其默认交互性。
                bool isInteractable = config.sortOrder >= modalSortOrder;
                SetLayerInteractable(config.layerType, isInteractable);
            }
        }

        private void UpdateModalLayerState()
        {
            // 查找最高层级的活跃模态层
            UILayer? nextModalLayer = null;
            foreach (var config in layerConfigs.OrderByDescending(c => c.sortOrder))
            {
                if (config.isModal && activePanels.ContainsKey(config.layerType) && activePanels[config.layerType].Count > 0)
                {
                    nextModalLayer = config.layerType;
                    break;
                }
            }

            if (nextModalLayer.HasValue)
            {
                SetModalLayer(nextModalLayer.Value);
            }
            else
            {
                // 没有活跃的模态面板，恢复所有层级的默认状态。
                currentModalLayer = null;
                foreach (var config in layerConfigs)
                {
                    SetLayerInteractable(config.layerType, true); // 恢复所有层级的潜在交互性
                }
            }
        }

        private void SetLayerInteractable(UILayer layer, bool isPotentiallyInteractable)
        {
            if (layerCanvasGroups.TryGetValue(layer, out var canvasGroup) &&
                layerConfigs.FirstOrDefault(c => c.layerType == layer) is { } config)
            {
                // --- 改进（BUG修复） ---：图层仅在其本身应阻挡射线检测且模态状态允许的情况下才会阻挡射线检测。
                // 这保留了Toast和DebugInfo等层级的原始设置。
                canvasGroup.blocksRaycasts = isPotentiallyInteractable && config.blocksRaycasts;
            }
        }

        private bool IsModalLayer(UILayer layer) => layerConfigs.FirstOrDefault(c => c.layerType == layer)?.isModal ?? false;
        private int GetLayerSortOrder(UILayer layer) => layerConfigs.FirstOrDefault(c => c.layerType == layer)?.sortOrder ?? 0;

        #endregion

        #region 工具方法

        public T GetPanel<T>() where T : PanelBase
        {
            return loadedPanels.TryGetValue(typeof(T), out var panel) ? panel as T : null;
        }

        public bool IsPanelLoaded<T>() where T : PanelBase => loadedPanels.ContainsKey(typeof(T)) && loadedPanels[typeof(T)] != null;
        public bool IsPanelShowing<T>() where T : PanelBase => GetPanel<T>()?.gameObject.activeInHierarchy ?? false;

        // ...其他工具方法如DestroyAllPanels、GetCurrentModalLayer等基本保持不变，但已更新以移除panelGameObjects
        public void DestroyAllPanels()
        {
            foreach (var panel in loadedPanels.Values.Where(p => p != null))
            {
                // 销毁前释放资源
                AssetsManager.Instance.ReleaseAsset(panel.name);
                Destroy(panel.gameObject);
            }

            loadedPanels.Clear();

            foreach (var layer in layerConfigs)
            {
                if (activePanels.ContainsKey(layer.layerType))
                {
                    activePanels[layer.layerType].Clear();
                }
                else
                {
                    activePanels[layer.layerType] = new List<PanelBase>();
                }
            }

            currentModalLayer = null;

            // 恢复所有层级的默认交互状态
            foreach (var config in layerConfigs)
            {
                SetLayerInteractable(config.layerType, true);
            }
        }



        /// <summary>
        /// 确保同一 UILayer 仅保留一个激活面板（keep），其他全部隐藏并从活跃列表移除。
        /// 不会销毁面板对象，便于之后快速重新显示。
        /// </summary>
        private void EnsureSinglePanelInLayer(UILayer layer, PanelBase keep = null)
        {
            if (!activePanels.TryGetValue(layer, out var list)) return;

            // 拷贝一份，避免遍历时修改集合
            var snapshot = list.ToList();
            foreach (var p in snapshot)
            {
                if (p == null) continue;
                if (p == keep) continue;

                // 隐藏并从该层活跃列表移除
                p.Hide();
            }

            list.Clear();
            if (keep != null) list.Add(keep);
        }

        #endregion
    }
}
