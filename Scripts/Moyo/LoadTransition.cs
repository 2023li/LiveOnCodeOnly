using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moyo.Unity;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 引入 Localization 命名空间
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class LoadTransition : MonoBehaviour
{
    public static LoadTransition Instance { get; private set; }

    [Header("UI Elements")]
    [LabelText("进度条")]
    public Slider progressBar;

    [LabelText("进度文本")]
    public TMP_Text progressText;

    [Header("Process Text (Flow)")]
    [LabelText("流程显示文本")]
    public TMP_Text loadingProcessText;

    [LabelText("每次加载显示的步数")]
    public int stepsCount = 5; // 每次加载随机选出5句话来显示

    [LabelText("流程文案候选池 (本地化)")]
    // 使用 LocalizedString 替代 string，支持多语言
    public List<LocalizedString> processCandidates;

    // 运行时生成的当前流程文案列表
    private List<string> _activeProcessTexts = new List<string>();

    [Header("Tips")]
    [LabelText("提示文本")]
    public TMP_Text loadingTipText;

    [LabelText("提示文案候选池 (本地化)")]
    public List<LocalizedString> tipsCandidates;

    [Header("Settings")]
    [LabelText("最小加载时间")]
    public float minLoadTime = 1.0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // 1. 隐藏全局 UI
        if (UIManager.Instance != null)
            UIManager.Instance.gameObject.SetActive(false);

        // 2. 初始化文本 (随机化 + 本地化)
        InitializeRandomTexts();

        // 3. 开始加载流程
        if (AppManager.Instance != null && AppManager.Instance.CurrentRequest != null)
        {
            StartCoroutine(ProcessLoadSequence(AppManager.Instance.CurrentRequest));
        }
        else
        {
            Debug.LogError("[LoadTransition] 缺少加载请求！");
        }
    }

    /// <summary>
    /// 初始化：从池中随机抽取文案并解析本地化字符串
    /// </summary>
    private void InitializeRandomTexts()
    {
        // --- A. 随机提示 (Tips) ---
        if (loadingTipText != null && tipsCandidates != null && tipsCandidates.Count > 0)
        {
            // 随机选一个 LocalizedString 并立即获取文本
            var randomTip = tipsCandidates[Random.Range(0, tipsCandidates.Count)];
            // GetLocalizedString() 会根据当前设置的语言返回对应的字符串
            loadingTipText.text = randomTip.GetLocalizedString();
        }

        // --- B. 随机流程 (Process Flow) ---
        _activeProcessTexts.Clear();
        if (processCandidates != null && processCandidates.Count > 0)
        {
            // 1. 创建一个临时列表用于洗牌
            var shuffleList = new List<LocalizedString>(processCandidates);

            // 2. Fisher-Yates 洗牌算法 (或者简单的随机交换)
            for (int i = 0; i < shuffleList.Count; i++)
            {
                var temp = shuffleList[i];
                int randomIndex = Random.Range(i, shuffleList.Count);
                shuffleList[i] = shuffleList[randomIndex];
                shuffleList[randomIndex] = temp;
            }

            // 3. 取前 N 个作为本次的流程
            int count = Mathf.Min(stepsCount, shuffleList.Count);
            for (int i = 0; i < count; i++)
            {
                // 解析本地化字符串并存入运行时列表
                _activeProcessTexts.Add(shuffleList[i].GetLocalizedString());
            }

            // 4. 立即显示第一句
            if (loadingProcessText != null && _activeProcessTexts.Count > 0)
            {
                loadingProcessText.text = _activeProcessTexts[0];
            }
        }
    }

    private IEnumerator ProcessLoadSequence(AppManager.SceneLoadContext request)
    {
        float startTime = Time.time;
        UpdateProgressUI(0f);

        // 阶段 1: 内存清理
        yield return Resources.UnloadUnusedAssets();

     
        System.GC.Collect();
        yield return null;

        // 阶段 2: 预加载
        Task preloadTask = Task.CompletedTask;
        if (request.PreloadAddresses != null && request.PreloadAddresses.Count > 0)
        {
            var tasks = new List<Task>();
            foreach (var addr in request.PreloadAddresses)
                tasks.Add(AssetsManager.Instance.LoadAssetAsync<object>(addr));
            preloadTask = Task.WhenAll(tasks);
        }

        // 阶段 3: 加载场景
        AsyncOperation sceneOp = SceneManager.LoadSceneAsync(request.TargetSceneName);
        sceneOp.allowSceneActivation = false;

        bool isPreloadDone = false;
        while (!sceneOp.isDone)
        {
            if (!isPreloadDone && preloadTask.IsCompleted) isPreloadDone = true;

            float sceneProgress = Mathf.Clamp01(sceneOp.progress / 0.9f);
            float assetsProgress = isPreloadDone ? 1f : 0.5f;
            float timeProgress = Mathf.Clamp01((Time.time - startTime) / minLoadTime);

            float totalProgress = Mathf.Min(sceneProgress, assetsProgress);
            float displayProgress = Mathf.Min(totalProgress, timeProgress);

            UpdateProgressUI(displayProgress);

            if (sceneOp.progress >= 0.9f && isPreloadDone && timeProgress >= 1f)
            {
                UpdateProgressUI(1f);
                yield return new WaitForSeconds(0.2f);

                if (UIManager.Instance != null) UIManager.Instance.gameObject.SetActive(true);
                sceneOp.allowSceneActivation = true;
            }
            yield return null;
        }
    }

    private void UpdateProgressUI(float value)
    {
        if (progressBar != null) progressBar.value = value;
        if (progressText != null) progressText.text = $"{Mathf.FloorToInt(value * 100)}%";

        // 更新流程文案
        if (loadingProcessText != null && _activeProcessTexts.Count > 0)
        {
            // 将 0~1 的进度映射到 0 ~ activeList.Count 的索引
            int index = Mathf.FloorToInt(value * _activeProcessTexts.Count);
            index = Mathf.Clamp(index, 0, _activeProcessTexts.Count - 1);

            loadingProcessText.text = _activeProcessTexts[index];
        }
    }
}
