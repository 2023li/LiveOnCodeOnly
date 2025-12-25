using System;
using System.Reflection;
using UnityEngine;

namespace Moyo.Unity
{
    /// <summary>
    /// 普通单例基类（非MonoBehaviour）- 安全版本
    /// </summary>
    /// <typeparam name="T">单例类型</typeparam>
    public abstract class Singleton<T> where T : class
    {
        private static T _instance;
        private static readonly object _lock = new object();
        private static bool _isInitialized = false;

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            CreateInstance();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 创建单例实例（使用反射调用受保护的无参构造函数）
        /// </summary>
        private static void CreateInstance()
        {
            if (_isInitialized)
                return;

            Type type = typeof(T);

            // 检查是否已经有实例存在（通过反射等方式）
            var existingInstance = GetExistingInstance();
            if (existingInstance != null)
            {
                _instance = existingInstance;
                _isInitialized = true;
                return;
            }

            // 使用反射获取受保护的构造函数
            ConstructorInfo constructor = null;

            try
            {
                // 尝试获取受保护的无参构造函数
                constructor = type.GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    null, Type.EmptyTypes, null);

                if (constructor == null || constructor.IsPublic)
                {
                    throw new InvalidOperationException($"单例类 {type.Name} 必须有一个受保护的无参构造函数");
                }

                _instance = (T)constructor.Invoke(null);
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"创建单例 {type.Name} 失败: {ex.Message}", ex);
            }

           
        }

        /// <summary>
        /// 检查是否已存在实例（用于防止反射攻击）
        /// </summary>
        private static T GetExistingInstance()
        {
            // 这里可以添加额外的检查逻辑
            // 例如检查静态字段或属性等
            return _instance;
        }

        /// <summary>
        /// 受保护的构造函数，防止外部实例化
        /// </summary>
        protected Singleton()
        {
            // 防止通过反射创建多个实例
            if (_instance != null)
            {
                throw new InvalidOperationException($"单例类 {typeof(T).Name} 只能有一个实例");
            }

            // 设置实例引用
            _instance = this as T;

            // 初始化逻辑
            Initialize();
           
        }

        /// <summary>
        /// 初始化方法（子类可重写）
        /// </summary>
        protected virtual void Initialize() { Debug.Log($"单例类 {typeof(T).Name}  InitializeByData"); }

        public virtual void Awake() { }
        /// <summary>
        /// 销毁单例
        /// </summary>
        public static void DestroyInstance()
        {
            if (_instance is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _instance = null;
            _isInitialized = false;
        }

        /// <summary>
        /// 单例是否存在
        /// </summary>
        public static bool HasInstance => _instance != null;
    }
}
