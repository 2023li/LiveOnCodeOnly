using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moyo.Unity;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 场景过渡控制器
/// 负责：显示进度条、清理内存、预加载资源、异步加载场景
/// </summary>
public class LoadTransition : MonoBehaviour
{
    public static LoadTransition Instance { get; private set; }

    [Header("UI Elements")]
    [LabelText("进度条")]
    public Slider progressBar;

    [LabelText("进度文本")]
    public TMP_Text progressText;

    [LabelText("提示文本")]
    public TMP_Text loadingTipText;

    [Header("Settings")]
    [LabelText("最小加载时间")]
    public float minLoadTime = 1.0f; // 建议至少 1秒，防止闪屏

    [LabelText("随机提示内容")]
    public string[] loadingTips = {
        "正在搬运资材...",
        "正在构建地形...",
        "正在召集工人...",
        "提示：合理规划道路可以提高效率",
        "提示：仓库已满时生产将停止"
    };

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // 1. 隐藏其他 UI，只保留 Loading 界面
        if (UIManager.Instance != null)
        {
            UIManager.Instance.gameObject.SetActive(false);
        }

        // 2. 随机显示一条提示
        if (loadingTipText != null && loadingTips != null && loadingTips.Length > 0)
        {
            loadingTipText.text = loadingTips[Random.Range(0, loadingTips.Length)];
        }

        // 3. 获取加载请求并开始流程
        // 注意：这里依赖 AppManager 中定义的 CurrentRequest 和 SceneLoadContext
        if (AppManager.Instance != null && AppManager.Instance.CurrentRequest != null)
        {
            StartCoroutine(ProcessLoadSequence(AppManager.Instance.CurrentRequest));
        }
        else
        {
            Debug.LogError("[LoadTransition] 没有找到加载请求或 AppManager 缺失，无法执行过渡！");
            // 兜底策略：如果出错，尝试直接切回 Main 或退出
            // SceneManager.LoadScene("Start"); 
        }
    }

    /// <summary>
    /// 核心加载流程协程
    /// </summary>
    private IEnumerator ProcessLoadSequence(AppManager.SceneLoadContext request)
    {
        float startTime = Time.time;
        UpdateProgressUI(0f);

        // =================================================
        // 阶段 1: 内存清理 (非常重要)
        // =================================================
        // 卸载未使用的资源 (Assets)
        yield return Resources.UnloadUnusedAssets();

        // 强制执行垃圾回收 (Managed Memory)
        System.GC.Collect();

        // 等待一帧确保清理完成
        yield return null;


        // =================================================
        // 阶段 2: 启动资源预加载 (并行处理)
        // =================================================
        Task preloadTask = Task.CompletedTask;
        if (request.PreloadAddresses != null && request.PreloadAddresses.Count > 0)
        {
            var tasks = new List<Task>();
            foreach (var addr in request.PreloadAddresses)
            {
                // 使用 AssetsManager 异步加载但不实例化，仅让资源进入内存缓存
                tasks.Add(AssetsManager.Instance.LoadAssetAsync<object>(addr));
            }
            // 创建一个并行的 Task
            preloadTask = Task.WhenAll(tasks);
        }


        // =================================================
        // 阶段 3: 异步加载目标场景
        // =================================================
        AsyncOperation sceneOp = SceneManager.LoadSceneAsync(request.TargetSceneName);
        sceneOp.allowSceneActivation = false; // 加载完先不切换，等待我们的逻辑控制


        // =================================================
        // 阶段 4: 循环更新进度条
        // =================================================
        bool isPreloadDone = false;

        while (!sceneOp.isDone)
        {
            // 4.1 检查预加载 Task 状态
            if (!isPreloadDone && preloadTask.IsCompleted)
            {
                isPreloadDone = true;
                if (preloadTask.IsFaulted)
                {
                    Debug.LogError($"[LoadTransition] 预加载资源时发生错误: {preloadTask.Exception}");
                }
            }

            // 4.2 计算各项进度
            // 场景加载进度 (0.9 是 Unity Ready 的阈值)
            float sceneProgress = Mathf.Clamp01(sceneOp.progress / 0.9f);

            // 预加载进度 (简化：未完成0.5，完成1.0。若需精确需封装 Task 进度报告)
            float assetsProgress = isPreloadDone ? 1f : 0.5f;

            // 时间进度 (确保 Loading 界面至少显示 minLoadTime 秒)
            float timeProgress = Mathf.Clamp01((Time.time - startTime) / minLoadTime);

            // 4.3 综合进度策略
            // 取最小值：只有当 场景载好、资源载好、时间够了 三者都满足时，进度条才能走满
            float totalProgress = Mathf.Min(sceneProgress, assetsProgress);

            // 为了视觉平滑，再和时间进度取个 Min (可选)
            float displayProgress = Mathf.Min(totalProgress, timeProgress);

            UpdateProgressUI(displayProgress);

            // 4.4 判断是否可以完成切换
            // 条件：场景已就绪(0.9) && 预加载已完成 && 最小展示时间已到
            if (sceneOp.progress >= 0.9f && isPreloadDone && timeProgress >= 1f)
            {
                // 视觉上给玩家看一眼 100%
                UpdateProgressUI(1f);
                yield return new WaitForSeconds(0.2f);

                // 恢复全局 UI (如果在 Start 里隐藏了的话)
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.gameObject.SetActive(true);
                }

                // 允许场景激活 -> 这会触发 SceneManager.sceneLoaded 事件
                // 进而触发 AppManager 中的回调逻辑
                sceneOp.allowSceneActivation = true;
            }

            yield return null;
        }
    }

    private void UpdateProgressUI(float value)
    {
        if (progressBar != null)
        {
            progressBar.value = value;
        }

        if (progressText != null)
        {
            progressText.text = $"{Mathf.FloorToInt(value * 100)}%";
        }
    }
}
