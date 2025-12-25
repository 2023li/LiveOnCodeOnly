using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
public class UIItem_TransportPanel : MonoBehaviour
{

    [SerializeField, LabelText("btn_打开物流面板")]
    private Button btn_打开物流面板;

    [SerializeField, LabelText("btn_关闭物流面板")]
    private Button btn_关闭物流面板;

    [SerializeField, LabelText("panel_物资分类选择面板")]
    private GameObject panel;

    [SerializeField, LabelText("物资枚举按钮父对象")]
    private RectTransform content_物资类型父对象;

    [SerializeField, LabelText("资源标签预制体"), AssetsOnly]
    private IconTextButton prefab_资源标签预制体;

  

    // Start is called before the first frame update
    void Awake()
    {
        btn_打开物流面板.onClick.AddListener(() =>
        {
            ShowPanel();

            ConnectionManager.Instance.EnterEditorMode();
        });

        btn_关闭物流面板.onClick.AddListener(() =>
        {
            HidePanel();
            ConnectionManager.Instance.ExitEditorMode();
        });
    }


    private void Start()
    {
        panel.gameObject.SetActive(false);
    }

    private void ShowPanel()
    {
        // 打开整体面板（以及内部 panel，如果需要的话）
        gameObject.SetActive(true);
        if (panel != null)
            panel.SetActive(true);

        // 先清空旧的子项，避免重复生成
        for (int i = content_物资类型父对象.childCount - 1; i >= 0; i--)
        {
            Destroy(content_物资类型父对象.GetChild(i).gameObject);
        }

        // 生成 prefab_资源标签预制体 
        foreach (SupplyDef supplyDef in GameContext.Instance.ResourceNetwork.CurrentProducibleMaterialEnums)
        {
            if (supplyDef == null) continue;

            // 实例化并设置父对象为 content_物资类型父对象
            IconTextButton btnItem = Instantiate(prefab_资源标签预制体, content_物资类型父对象);

            // 初始化 图标 + 名称
            btnItem.SetContent(supplyDef.DisplayName, supplyDef.Icon); // LevelDisplayName & Icon :contentReference[oaicite:1]{index=1}

            // 为了避免闭包问题，拷贝一份局部变量
            var def = supplyDef;

            // 设置点击事件 —— 点击时打印 supplyDef.LevelDisplayName
            btnItem.SetOnClick(() =>
            {
                ConnectionManager.Instance.OnSelect(def);
            });
        }
    }

    private void HidePanel()
    {
        gameObject.SetActive(false);
    }
}
