using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class BuildingBrief_Common : BuildingBriefPanelBase
{
    public Image buildingIcon;
    public TMP_Text buildingName;

    protected override void ShowInfo(BuildingInstance building)
    {
        if (building == null)
        {
            return;
        }
        if (building.Def.BuildingIcon != null)
        {
            buildingIcon.sprite = building.Def.BuildingIcon;
        }
        buildingName.text = building.Def.DisplayName+" lv"+building.Self_LevelIndex;
    }
}
