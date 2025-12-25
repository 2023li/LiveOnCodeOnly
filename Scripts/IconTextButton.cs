using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Lean.Pool;


public class IconTextButton : MonoBehaviour
{
    [Header("UI 引用")]
    public TMP_Text text;
    public Image iconImage;
    public Button btn;

    /// <summary>
    /// 设置按钮显示内容
    /// </summary>
    public void SetContent(string label, Sprite icon = null)
    {
        if (text != null)
            text.text = label;

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.gameObject.SetActive(icon != null);
        }
    }

    /// <summary>
    /// 设置点击回调（会清空旧的监听）
    /// </summary>
    public void SetOnClick(System.Action onClick)
    {
        if (btn == null) return;

        btn.onClick.RemoveAllListeners();
        if (onClick != null)
            btn.onClick.AddListener(() => onClick());
    }

    /// <summary>
    /// 设置按钮可交互状态
    /// </summary>
    /// <param name="interactable">是否可交互</param>
    public void SetInteractable(bool interactable)
    {
        if (btn == null) return;

        btn.interactable = interactable;
    }
}
