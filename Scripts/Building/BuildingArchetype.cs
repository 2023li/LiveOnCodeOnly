using UnityEngine;
using System.Collections.Generic;
using System;
using Sirenix.OdinInspector;


[Flags]
public enum WarehouseProperties
{
    物资,
    粮食,
    
}

public enum BuildingClassify
{
    基础,
    市政,
    工业类,
    农业类,

    其他,
}


[CreateAssetMenu(fileName = "BA_", menuName = "Game/BuildingArchetype")]
public class BuildingArchetype : ScriptableObject
{
    [LabelText("建筑唯一ID")]
    public string Id;                // "residence", "warehouse", "garden"

    [LabelText("建筑名称")]
    public string DisplayName;       // "居民房"

    [LabelText("建筑尺寸")]
    public int Size;

    [LabelText("建筑分类")]
    public BuildingClassify classification = BuildingClassify.基础;



    [AssetsOnly,LabelText("建筑预制体"),Required("必须赋值",InfoMessageType.Error)]
    public BuildingInstance BuildingPrefab;

    [LabelText("建筑图标")]
    public Sprite BuildingIcon;




    [AssetsOnly,LabelText("建筑信息UI预制体(简短)")]
    public BuildingBriefPanelBase UIPanelPrefab_Brief;

    [AssetsOnly, LabelText("建筑信息UI预制体(详情)")]
    public BuildingDetailedPanelBase UIPanelPrefab_Detailed;



    [LabelText("简介"), MultiLineProperty(3)]
    public string Introduction;


    //是否在建造面板显示
    [LabelText("显示在建造面板的条件")]
    [SerializeReference] 
    [Tooltip("留空则始终显示")]
    public List<Condition> ShowInBuildPanel;
    //允许建造
    [LabelText("允许建造的条件")]
    [SerializeReference] 
    [Tooltip("留空则始终允许建造")]
    public List<Condition> AllowConstruction;

    [LabelText("建筑的固有Rule")]
    [SerializeReference]
    public List<Rule> BaseRules;

    [ListDrawerSettings(ShowFoldout = true, ShowIndexLabels = true, DraggableItems = true)]
    public List<BuildingLevelDef> LevelsList = new List<BuildingLevelDef>();
   

}

[Serializable]
public class BuildingLevelDef
{
    [LabelText("建筑名称")]
    public string LevelDisplayName;       // "居民房"

    // —— 基础属性（根据建筑不同使用其子集）——
    [SerializeField,HorizontalGroup("人口"),LabelText("基础最大人口")]
    public int BaseMaxPopulation;   // 人口上限基础值（居民类）
  
    [SerializeField,LabelText("仓库容量"),Tooltip("提供的全局容量增量")]
    public int BaseStorageCapacity;     // 仓库容量（仓库类）
   
 
    [LabelText("升级所需经验")]
    public int ExpToNext = -1;      // 升级需要经验；-1 表示最高级

    [SerializeField, LabelText("基础最大岗位")]
    public int BaseMaxJobsPosition;

   


    [LabelText("转运物资耐久度消耗")]
    public int BaseTransportationResistance = 3;


    [LabelText("链接运输范围")]
    public float BaseTransportRadius = 10f;

    [LabelText("资源分发范围")]
    public float BaseDistributeRadius = 5f;


    [LabelText("基础最大转运流量")]
    public float BaseMaxTraffic = 0f;

    [LabelText("基础岗位吸引力")]
    public float BaseAttractivenessPerJob = 0f;


   // [LabelText("等级表现配置")]
   // public BuildingLevelViewConfig ViewConfig = new();


    [LabelText("允许升级的条件")]
    [SerializeReference]
    public List<Condition> ConditionsForAllowingUpgrades = new List<Condition>
    {
        //这里需要增加一个默认的条件：建筑经验值大于ExpToNext
    };


    // 规则：回合末拉取资源、人口增减、经验与升级等
    [LabelText("规则")]
     [ListDrawerSettings(
        ShowFoldout = true,
        ShowIndexLabels = true,
        DraggableItems = true,
        ListElementLabelName = nameof(Rule.GetRuleName)
        )]
    [SerializeReference, HideReferenceObjectPicker]
    public List<Rule> Rules = new();

  

}

