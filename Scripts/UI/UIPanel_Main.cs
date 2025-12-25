using System.Collections;
using System.Collections.Generic;
using Moyo.Unity;
using UnityEngine;
using UnityEngine.UI;


public class UIPanel_Main : PanelBase
{

    [AutoBind] public Button btn_Continue;
    [AutoBind] public Button btn_NewGame;
    [AutoBind] public Button btn_LoadGame;
    [AutoBind] public Button btn_Setting;
    [AutoBind] public Button btn_Credits;
    [AutoBind] public Button btn_Quit;


    private void Reset()
    {
        this.AutoBindFields();
    }


    void Start()
    {
        btn_NewGame.onClick.AddListener(()=>
        {
            AppManager.Instance.LoadScene(LOConstant.SceneName.Game);
        });
    }

  
}
