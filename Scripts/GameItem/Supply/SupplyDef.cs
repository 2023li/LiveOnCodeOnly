

using System;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 物资种类
/// </summary>
public enum SupplyCategory
{
    独特,
    一级食物,
    二级食物,
}





[CreateAssetMenu(fileName = "SD_", menuName = "Game/SupplyDef")]
public class SupplyDef : ScriptableObject
{
    [ReadOnly]
    [LabelText("物资ID")]
    public string Id;

    [LabelText("名称")]
    public string DisplayName; // "食物"

    [LabelText("本质")]
    public SupplyCategory Category;

    [LabelText("图标")]
    public Sprite Icon;



    [LabelText("仓库显示设置"),BoxGroup("库存属性")]
    public DisplayOption DisplaySetting = DisplayOption.常规;

    [LabelText("占用库存"), BoxGroup("库存属性")]
    public int OccupationUnit = 1;

    [LabelText("损耗率"),BoxGroup("库存属性")]
    [Tooltip("一般来说控制在5%以内，实际物资损耗还会受到仓库影响,最小单位为0.005f")]
    [Range(0, 0.05f)]
    [OnValueChanged(nameof(OnBaseLossRateChanged))]
    public float BaseLossRate;




    [LabelText("耐久度"),BoxGroup("运输属性")]
    [Tooltip("能够传播的跳板数量")]
    public int BaseDurability = 5;

    [LabelText("库存流量占用"),BoxGroup("运输属性")]
    public int BaseTrafficOccupancy = 1;

    [LabelText("运输路线材质"), BoxGroup("运输属性")]
    public Material lineMat;

    




   

    private void OnBaseLossRateChanged()
    {
        BaseLossRate = Mathf.Round(BaseLossRate / 0.005f) * 0.005f;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // ScriptableObject 的名字就是文件名（不带 .asset）
        string fileName = name;

        // 不再去掉 SD_ 前缀，直接使用完整名字
        if (Id != fileName)
        {
            Id = fileName;
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif




    public enum DisplayOption
    {
        常规,
        不显示,
        宝藏,
        
    }



    public static SupplyDef GetSupplyDef(SupplyEnum supply)
    {
        return SupplyLib.GetSupplyDef(supply);
    }
    public static SupplyDef GetSupplyDef(string supplyid)
    {
        return SupplyLib.GetSupplyDef(supplyid);
    }

}

[Serializable]
public struct SupplyAmount
{
    public SupplyDef Resource;
    public int Amount;
}
