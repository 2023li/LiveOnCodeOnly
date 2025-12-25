using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class BuildingView : MonoBehaviour
{




    [LabelText("每个等级对应的模型")]
    [SerializeField] private List<StructKV<int, GameObject>> levelsModel;

    [SerializeField]private Canvas buildingCanvas;

    private UINode nodeUI;

    private BuildingInstance self;
    internal void Init(BuildingInstance buildingInstance)
    {
        self = buildingInstance;
        self.OnStateChanged += Handle_BuildingStateChange;

        foreach (StructKV<int, GameObject> item in levelsModel)
        {
            item.Value2.gameObject.SetActive(item.Value1 == self.Self_LevelIndex);
        }
        //建筑UI系统
        buildingCanvas = transform.parent.Find("建筑实体uiCanvas").GetComponent<Canvas>();

        if (buildingCanvas == null)
        {
            Debug.LogError("buildingCanvas NULL");
        }

        buildingCanvas.worldCamera = InputManager.Instance.RealCamera;
        nodeUI = buildingCanvas.GetComponentInChildren<UINode>();


        nodeUI.BuidBuildingInstance(self);

        Debug.Log("初始化完成");
    }


    private void OnDisable()
    {
        self.OnStateChanged -= Handle_BuildingStateChange;
    }

    private void Handle_BuildingStateChange(BuildingInstance instance, BuildingStateValueType type)
    {
        switch (type)
        {
            case BuildingStateValueType.LevelIndex:

                foreach (StructKV<int, GameObject> item in levelsModel)
                {

                    item.Value2.gameObject.SetActive(item.Value1 == instance.Self_LevelIndex);

                }

                break;
            case BuildingStateValueType.CurrentExp:
                break;
            case BuildingStateValueType.ExpToNext:
                break;
            case BuildingStateValueType.MaxPopulation:
                break;
            case BuildingStateValueType.CurrentPopulation:
                break;
            case BuildingStateValueType.CurrentWorkers:
                break;
            case BuildingStateValueType.MaxStorageCapacity:
                break;
            case BuildingStateValueType.TransportationAbility:
                break;
            case BuildingStateValueType.TransportationResistance:
                break;
            case BuildingStateValueType.就业吸引力:
                break;
            case BuildingStateValueType.产品列表:



                break;
            default:
                break;
        }
    }

    private void Handle_OnEditSupplyNetwork(SupplyEnum supply)
    {
        //这里要处理一下 区分一下逻辑看是否要显示该建筑的节点 以及如何显示

        //现在由节点自行处理了
    }

}
