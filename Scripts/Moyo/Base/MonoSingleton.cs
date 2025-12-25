using UnityEngine;

namespace Moyo.Unity
{
    /// <summary>
    /// MonoBehaviour单例基类
    /// </summary>
    /// <typeparam name="T">单例类型</typeparam>
    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
    {
        private static T _instance;
        private static readonly object _lock = new object();
        private static bool _isApplicationQuitting = false;

        public static T Instance
        {
            get
            {
                if (_isApplicationQuitting)
                {
                    // 正在退出：不要再创建
                    Debug.LogWarning($"[MonoSingleton] 应用程序正在退出，无法获取 {typeof(T)} 实例");
                    return null;
                }

                if (_instance != null) return _instance;

                lock (_lock)
                {
                    if (_instance != null) return _instance;

                    // 先找场景里已有的
                    _instance = FindObjectOfType<T>();
                    if (_instance != null) return _instance;

                    // 仅在运行态允许自动创建；编辑器/退出阶段不建
                    if (!Application.isPlaying) return null;

                    var singletonObject = new GameObject($"{typeof(T).Name} (Singleton)");
                    _instance = singletonObject.AddComponent<T>();

                    if (_instance.IsDontDestroyOnLoad)
                        DontDestroyOnLoad(singletonObject);

                    return _instance;
                }
            }
        }

        /// <summary>是否在场景切换时不销毁（默认为true）</summary>
        protected virtual bool IsDontDestroyOnLoad => true;

        /// <summary>是否自动初始化（默认为true）</summary>
        protected virtual bool AutoInitialize => true;

        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"[MonoSingleton] 检测到重复的 {typeof(T)} 实例，销毁新实例");
                Destroy(gameObject);
                return;
            }

            _instance = (T)this;

            if (IsDontDestroyOnLoad && transform.parent == null)
                DontDestroyOnLoad(gameObject);

            if (AutoInitialize)
                Initialize();
        }

        protected virtual void OnApplicationQuit()
        {
            _isApplicationQuitting = true;   // 🔒 退出标记：阻止后续任何自动创建
        }

        protected virtual void OnDestroy()
        {
            // ⚠️ 不要把 _isApplicationQuitting 复位！
            if (_instance == this)
                _instance = null;
        }

        /// <summary>初始化方法（子类可重写）</summary>
        protected virtual void Initialize() { }

        /// <summary>销毁单例</summary>
        public static void DestroyInstance()
        {
            if (_instance != null)
            {
                Destroy(_instance.gameObject);
                _instance = null;
                // 这里不要动 _isApplicationQuitting；否则外部访问 Instance 可能又把它建回来
            }
        }

        /// <summary>单例是否存在</summary>
        public static bool HasInstance => _instance != null && !_isApplicationQuitting;
    }
}
