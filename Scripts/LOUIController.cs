using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moyo.Unity;
using UnityEngine;

public class LOUIController:IBackHandler
{

    public LOUIController()
    {
        InputManager.Instance.Register(this);
    }

    public short Priority { get; set; } = LOConstant.InputPriority.Priority_UI控制器;

    public bool TryHandleBack()
    {
        _ = UIManager.Instance.ShowPanel<UIPanel_Pause>(UIManager.UILayer.Main);
        return true;
    }

   
}
