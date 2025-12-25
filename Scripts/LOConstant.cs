using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using System.Threading.Tasks.Sources;

public static class LOConstant
{

    public static class AssetsKey
    {
        public const string Address_SupplyLib = "SupplyLib";
    }

    public static class SceneName
    {
        public const string Boot = "Boot";
        public const string Start = "Start";
        public const string Game = "Game";
        public const string Transition = "Transition";

    }
    public static class InputPriority
    {
        //越高越先触发
        public const int Priority_设置面板 = 17;
        //
        public const int Priority_BuildingBuilder = 10;
        public const int Priority_暂停面板 = 16;
        public const int Priority_UI控制器 = 15;





        public const int Priority_相机监听鼠标滚轮 = 20;
    }

    public static class Layer
    {
        public const string LayerStr_Building = "SelfBuilding";
        public const int LayerIndex_Building = 14;
    }

    

}
