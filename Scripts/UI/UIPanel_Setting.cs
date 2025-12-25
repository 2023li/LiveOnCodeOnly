using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Moyo.Unity;
using UnityEngine.UI;
using Sirenix.OdinInspector;
using FMODUnity;
using Unity.VisualScripting;
public class UIPanel_Setting : PanelBase,IBackHandler
{

   

    [SerializeField, FoldoutGroup("导航栏")] private Toggle toggle_声音;
    [SerializeField, FoldoutGroup("导航栏")] private Toggle toggle_控制;
    [SerializeField, FoldoutGroup("导航栏")] private Toggle toggle_游戏;
    [SerializeField, FoldoutGroup("导航栏")] private Toggle toggle_图像;
    [SerializeField, FoldoutGroup("导航栏")] private Toggle toggle_辅助功能;

    [SerializeField, FoldoutGroup("导航栏")] private GameObject go_声音;
    [SerializeField, FoldoutGroup("导航栏")] private GameObject go_控制;
    [SerializeField, FoldoutGroup("导航栏")] private GameObject go_游戏;
    [SerializeField, FoldoutGroup("导航栏")] private GameObject go_图像;
    [SerializeField, FoldoutGroup("导航栏")] private GameObject go_辅助功能;

    public short Priority { get; set; } =  LOConstant.InputPriority.Priority_设置面板; 

    public bool TryHandleBack()
    {
       gameObject.SetActive(false);
        return true;
    }

    protected override void Awake()
    {
        base.Awake();
        BindToggleEvents();

        RefreshPanelState();

    }

    private void OnEnable()
    {
        if (InputManager.HasInstance)
        {
            InputManager.Instance.Register(this);
        }
    }

    private void OnDestroy()
    {
        if (InputManager.HasInstance)
        {
            InputManager.Instance.UnRegister(this);
        }
    }




    private void BindToggleEvents()
    {
        // 使用 Lambda 表达式直接绑定：isOn 为 true 时显示物体，false 时隐藏物体
        toggle_声音.onValueChanged.AddListener((isOn) => { go_声音.SetActive(isOn);AudioManager.Instance.PlayOneShot(AudioEventReference.Instance.UI_切换);}); 
        toggle_控制.onValueChanged.AddListener((isOn) => { go_控制.SetActive(isOn); AudioManager.Instance.PlayOneShot(AudioEventReference.Instance.UI_切换); });
        toggle_游戏.onValueChanged.AddListener((isOn) => { go_游戏.SetActive(isOn); AudioManager.Instance.PlayOneShot(AudioEventReference.Instance.UI_切换); });
        toggle_图像.onValueChanged.AddListener((isOn) => { go_图像.SetActive(isOn); AudioManager.Instance.PlayOneShot(AudioEventReference.Instance.UI_切换); });
        toggle_辅助功能.onValueChanged.AddListener((isOn) => { go_辅助功能.SetActive(isOn); AudioManager.Instance.PlayOneShot(AudioEventReference.Instance.UI_切换); });
    }

    private void RefreshPanelState()
    {
        // 强制根据当前Toggle的勾选状态刷新一次显隐，防止Inspector里手动设置乱了
        go_声音.SetActive(toggle_声音.isOn);
        go_控制.SetActive(toggle_控制.isOn);
        go_游戏.SetActive(toggle_游戏.isOn);
        go_图像.SetActive(toggle_图像.isOn);
        go_辅助功能.SetActive(toggle_辅助功能.isOn);

    }
}
