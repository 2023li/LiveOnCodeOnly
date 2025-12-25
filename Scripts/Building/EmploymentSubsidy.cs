using UnityEngine;
using Sirenix.OdinInspector;

// 可挂到任何可就业建筑实例：补贴按“每岗位/每回合”
[DisallowMultipleComponent]
[AddComponentMenu("LifeOn/Employment/Employment Subsidy")]
public class EmploymentSubsidy : MonoBehaviour
{
    [LabelText("政府补贴（每岗位/每回合）"), Min(0)]
    public float SubsidyPerJob = 0f;
}
