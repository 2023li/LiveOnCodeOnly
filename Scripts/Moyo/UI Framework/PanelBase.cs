using UnityEngine;
using DG.Tweening;

namespace Moyo.Unity
{
    public abstract class PanelBase : MonoBehaviour
    {
        

        protected Canvas canvas;
        
        protected virtual void Awake()
        {
            this.AutoBindFields();
            canvas = UIManager.Instance.GetMainCanvas();
        }

       

        /// <summary>
        /// 面板实例化后由UIManager仅调用一次
        /// 用于一次性初始化设置
        /// </summary>
        public virtual void OnPanelCreated(params object[] args) { }

        public virtual void Show(params object[] args)
        {
            gameObject.SetActive(true);
        }
        public virtual void Hide(params object[] args)
        {
            gameObject?.SetActive(false);
        }

        public virtual void Show()
        {
            gameObject.SetActive(true);
        }
        public virtual void Hide()
        {
            gameObject?.SetActive(false);
        }

    }
}
