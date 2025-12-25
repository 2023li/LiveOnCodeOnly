using UnityEngine;
using UnityEngine.UI;



/*
 * 这个脚本用于动画驱动的 UI开关组件
 */

[RequireComponent(typeof(Toggle))] // 自动添加Toggle组件依赖
public class ToggleAnimatorLink : MonoBehaviour
{
    private Animator targetAnimator; // 拖入你的Animator
    public string boolParamName = "ToggleIsOn"; // 对应你在Animator里设置的参数名

    private Toggle toggle;


    private void Awake()
    {
        targetAnimator = GetComponent<Animator>();
    }
    void Start()
    {
        toggle = GetComponent<Toggle>();

        // 初始化动画状态
        UpdateAnimator(toggle.isOn);

        // 监听开关值的改变
        toggle.onValueChanged.AddListener(UpdateAnimator);
    }

    // 当Toggle值改变时调用此函数
    void UpdateAnimator(bool isOn)
    {
        if (targetAnimator != null)
        {
            targetAnimator.SetBool(boolParamName, isOn);
        }
    }

    // 记得在销毁时移除监听，是个好习惯
    void OnDestroy()
    {
        if (toggle != null)
            toggle.onValueChanged.RemoveListener(UpdateAnimator);
    }
}
