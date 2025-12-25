using System.Collections;
using System.Collections.Generic;
using Moyo.Unity;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 场景转移脚本
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
    public float minLoadTime = 0.7f;

    public string[] loadingTips = {
            "提示1111...",
            "提示222...",
            "提示3333...",
            "提示444..."
        };

    private AsyncOperation loadingOperation;
    private float loadingStartTime;
    private bool isLoadingComplete;

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
        UIManager.Instance.gameObject.SetActive(false);

        // 随机显示一条加载提示
        if (loadingTipText != null && loadingTips.Length > 0)
        {
            loadingTipText.text = loadingTips[Random.Range(0, loadingTips.Length)];
        }

        if (AppManager.Instance)
        {
            // 启动加载流程
            StartLoading(AppManager.Instance.TargetSceneName);
        }
    }


    public void StartLoading(string sceneName)
    {
        

        loadingStartTime = Time.time;
        isLoadingComplete = false;


        StartCoroutine(LoadSceneAsync(sceneName));
    }

    private IEnumerator LoadSceneAsync(string sceneName)
    {
        var preloadList = AppManager.Instance?.NeedPreloadGameObject;
        var preloadTasks = new List<System.Threading.Tasks.Task>();

        if (preloadList != null && preloadList.Count > 0)
        {
            foreach (var addr in preloadList)
                preloadTasks.Add(AssetsManager.Instance.LoadAssetAsync<object>(addr));
        }

        loadingOperation = SceneManager.LoadSceneAsync(sceneName);
        loadingOperation.allowSceneActivation = false;

        loadingStartTime = Time.time;
        isLoadingComplete = false;

        // 进度合成：预加载与场景加载都未完成时持续等待
        while ((Time.time - loadingStartTime) < minLoadTime
               || loadingOperation.progress < 0.9f
               || preloadTasks.Exists(t => !t.IsCompleted))
        {
            float sceneProgress = Mathf.Clamp01(loadingOperation.progress / 0.9f); // 0~1
            float preloadProgress = 1f;

            if (preloadTasks.Count > 0)
            {
                int done = 0;
                for (int i = 0; i < preloadTasks.Count; i++)
                    if (preloadTasks[i].IsCompleted) done++;

                preloadProgress = (float)done / preloadTasks.Count; // 0~1
            }

            float timeProgress = Mathf.Clamp01((Time.time - loadingStartTime) / minLoadTime);

            // 进度合成策略：时间限制也要满足；场景与预加载按权重合成
            float combined = 0.5f * sceneProgress + 0.5f * preloadProgress;
            float display = Mathf.Min(timeProgress, combined);

            UpdateProgressUI(display);
            yield return null;
        }

        UpdateProgressUI(1f);
        yield return new WaitForSeconds(0.5f);


        UIManager.Instance.gameObject.SetActive(true);
        isLoadingComplete = true;
        loadingOperation.allowSceneActivation = true;

        while (!loadingOperation.isDone)
            yield return null;
    }

    private void UpdateProgressUI(float progress)
    {
        if (progressBar != null)
        {
            progressBar.value = progress;
        }

        if (progressText != null)
        {
            progressText.text = $"{(progress * 100):0}%";
        }
    }

    // 可选：允许用户点击屏幕提前进入场景（在满足最小时间后）
    private void Update()
    {
        if (!isLoadingComplete && Time.time - loadingStartTime >= minLoadTime && Input.GetMouseButtonDown(0))
        {
            isLoadingComplete = true;
            loadingOperation.allowSceneActivation = true;
        }
    }
}

