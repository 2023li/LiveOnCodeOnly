using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 建筑预放器
/// </summary>

public class BuildingPlaceholder : MonoBehaviour
{


    // 建议用 List 也可以，数组同样能显示
    [LabelText("场景预制建筑")]
    public List<BuildingPos> preBuildings;

    [Button]
    public void CreatePrefabricatedBuildings()
    {
        BuildingBuilder builder = BuildingBuilder.Instance;
        if (builder == null)
        {
            Debug.LogWarning("不存在建造器，无法建造");
            return;
        }

        foreach (var item in preBuildings)
        {
            if (item == null || item.pointTransform == null || item.archetype == null)
            {
                Debug.LogWarning("条目不完整，已跳过");
                continue;
            }

            if (builder.TryCreateBuildingAtWorld(item.pointTransform.position, item.archetype, out BuildingInstance building))
            {
                Debug.Log($"建筑创建成功：{item.archetype.DisplayName}");
            }
            else
            {
                Debug.LogWarning($"建筑创建失败：{item.archetype.DisplayName}");
            }
        }
    }

    [System.Serializable] // <- 关键：让 Unity 能序列化
    public class BuildingPos
    {
        public Transform pointTransform;
        public BuildingArchetype archetype;
    }

#if UNITY_EDITOR
    static GUIStyle _labelStyle;

    void OnDrawGizmos() // 若只想选中时显示，改成 OnDrawGizmosSelected()
    {
        if (preBuildings == null) return;

        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _labelStyle.normal.textColor = Color.white;
        }

        foreach (var item in preBuildings)
        {
            if (item == null || item.pointTransform == null) continue;

            var pos = item.pointTransform.position;

            // 一个小球标记点位，方便看
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(pos, 0.15f);

            // 在点位上方一点显示文字
            var name = (item.archetype != null && !string.IsNullOrEmpty(item.archetype.DisplayName))
                ? item.archetype.DisplayName
                : "(未指定)";

            Handles.Label(pos + Vector3.up * 0.25f, name, _labelStyle);
        }
    }
#endif
}
