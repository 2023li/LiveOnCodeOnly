using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Moyo.Unity;
using UnityEngine.SceneManagement;
using Sirenix.OdinInspector;
using static BuildingConfirmPanel;
using System;
using static LOConstant;


public enum AppLanguage
{
    简体中文,
    English,

}

public class AppManager : MonoSingleton<AppManager>
{
    // 存储当前正在进行的加载请求数据
    public class SceneLoadContext
    {
        public string TargetSceneName;
        public List<string> PreloadAddresses;
        public Action OnComplete; // 核心：加载完成后的回调
        public bool UseTransition = true;
    }
    public SceneLoadContext CurrentRequest { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }
    // Start is called before the first frame update
    void Start()
    {
        PersistentManager.Instance.LoadAppData();

        uiController = new LOUIController();
    }

    // Update is called once per frame
    void Update()
    {

    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }
    #region 切换场景
    [Button]
    public void LoadStartScene()
    {
        SceneLoadContext sc = new SceneLoadContext()
        {
            TargetSceneName = LOConstant.SceneName.Start,
            PreloadAddresses = new List<string>()
            {
                "UIPanel_Main"
            },
            OnComplete = async () => {await UIManager.Instance.ShowPanel<UIPanel_Main>(UIManager.UILayer.Main); }
        };
        LoadScene(sc);
    }
    [Button]
    public void LoadGameScene()
    {
        // LoadScene("Game", true, "UIPanel_GameMain");
        SceneLoadContext sc = new SceneLoadContext()
        {
            TargetSceneName = LOConstant.SceneName.Game,
            PreloadAddresses = new List<string>()
            {
                "UIPanel_GameMain"
            },
            OnComplete = async () =>
            {
                await UIManager.Instance.ShowPanel<UIPanel_GameMain>(UIManager.UILayer.Main);

                GameContext.Instance.Init();

                AppStateEvent.Tiggle(AppState.游戏场景加载完成);

            }
        };
        LoadScene(sc);
    }
    #endregion



    LOUIController uiController;

    public List<string> NeedPreloadGameObject;

    public string TargetSceneName { get; set; }
    /// <summary>
    /// 加载场景的统一入口
    /// </summary>
    /// <param name="sceneName">目标场景名</param>
    /// <param name="onComplete">加载完成后的回调逻辑</param>
    /// <param name="transition">是否使用过渡页</param>
    /// <param name="preloadAssets">需要预加载的资源地址</param>
    public void LoadScene(string sceneName, Action onComplete = null, bool transition = true, params string[] preloadAssets)
    {
        // 1. 构建请求上下文
        CurrentRequest = new SceneLoadContext
        {
            TargetSceneName = sceneName,
            OnComplete = onComplete,
            UseTransition = transition,
            PreloadAddresses = new List<string>(preloadAssets ?? Array.Empty<string>())
        };

        LoadScene(CurrentRequest);
    }
    public void LoadScene(SceneLoadContext sceneLoadContext)
    {
        CurrentRequest = sceneLoadContext;
        // 2. 执行加载
        if (CurrentRequest.UseTransition)
        {
            // 进入过渡场景
            SceneManager.LoadScene(LOConstant.SceneName.Transition);
        }
        else
        {
            // 直接加载（通常用于测试）
            SceneManager.LoadScene(CurrentRequest.TargetSceneName);
            // 注意：直接加载时，Unity的sceneLoaded事件也会触发，所以HandleSceneLoaded会被调用
        }
    }

    // Unity场景加载完成时的系统回调
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 如果加载的是过渡场景，不执行回调
        if (scene.name == LOConstant.SceneName.Transition) return;

        // 如果当前没有请求，或者加载的场景不是目标场景（防御性编程），跳过
        if (CurrentRequest == null || scene.name != CurrentRequest.TargetSceneName) return;

        Debug.Log($"[AppManager] 场景 {scene.name} 加载完毕，执行回调。");

        // 1. 执行回调
        CurrentRequest.OnComplete?.Invoke();

        // 2. 清理请求，防止重复触发
        CurrentRequest = null;
    }
}





public enum AppState
{

    游戏加载完成,

   游戏场景加载完成,

    开始游戏,

    游戏进行中,

    结束游戏


}
public struct AppStateEvent
{
    private static AppStateEvent eventArg;

    public AppState State;
    
    public static void Tiggle(AppState e)
    {

        eventArg.State = e;


        MoyoEventManager.TriggerEvent<AppStateEvent>(eventArg);
    }
}
