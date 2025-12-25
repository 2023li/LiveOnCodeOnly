using System.Collections;
using System.Collections.Generic;
using Moyo.Unity;
using UnityEngine;
using UnityEngine.UI;

public class UIPanel_Pause : PanelBase, IBackHandler
{

    [SerializeField] private Button btn_Continue;
    [SerializeField] private Button btn_LoadGame;
    [SerializeField] private Button btn_Setting;
    [SerializeField] private Button btn_Save;
    [SerializeField] private Button Quit;


    protected override void Awake()
    {
        base.Awake();

        InputManager.Instance.Register(this);
    }


    public short Priority { get ; set; } = LOConstant.InputPriority.Priority_暂停面板;

    public bool TryHandleBack()
    {
        if (!UIManager.Instance.IsPanelShowing<UIPanel_Pause>())
        {
            return false;
        }
           


        UIManager.Instance.HidePanel<UIPanel_Pause>();

        return true;
    }






}
