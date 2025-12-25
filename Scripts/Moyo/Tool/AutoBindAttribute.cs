using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Moyo.Unity
{

    /// <summary>
    /// 自动绑定特性，用于自动查找并绑定同名对象的组件引用
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class AutoBindAttribute : Attribute
    {
        public string Name { get; private set; }
        public bool Required { get; private set; }
        public int MaxDepth { get; private set; }

        /// <summary>
        /// 自动绑定特性构造函数
        /// </summary>
        /// <param name="name">可选，指定要查找的对象名称，默认为字段名称</param>
        /// <param name="required">可选，是否必须找到对象，默认为true</param>
        /// <param name="maxDepth">可选，最大查找深度，默认为5</param>
        public AutoBindAttribute(string name = null, bool required = true, int maxDepth = 5)
        {
            Name = name;
            Required = required;
            MaxDepth = maxDepth;
        }
    }



    public static class MonoBehaviourAutoBindExtensions
    {
        /// <summary>
        /// 自动绑定所有带有 AutoBind 特性的字段（MonoBehaviour 扩展方法）
        /// </summary>
        /// <param name="monoBehaviour">目标 MonoBehaviour 实例</param>
        public static void AutoBindFields(this MonoBehaviour monoBehaviour)
        {
            if (monoBehaviour == null) throw new ArgumentNullException(nameof(monoBehaviour));

            Type type = monoBehaviour.GetType();
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var field in fields)
            {
                var autoBindAttr = field.GetCustomAttribute<AutoBindAttribute>();
                if (autoBindAttr == null) continue;

                string objectName = string.IsNullOrEmpty(autoBindAttr.Name) ? field.Name : autoBindAttr.Name;
                var foundObjects = FindObjectsRecursive(monoBehaviour.transform, objectName, autoBindAttr.MaxDepth);

                if (foundObjects.Count == 0)
                {
                    if (autoBindAttr.Required)
                    {
                        Debug.LogError($"自动绑定失败：在 {monoBehaviour.gameObject.name} 上，字段 '{field.Name}' 找不到名为 '{objectName}' 的对象", monoBehaviour.gameObject);
                    }
                    continue;
                }

                if (foundObjects.Count > 1)
                {
                    Debug.LogWarning($"自动绑定警告：在 {monoBehaviour.gameObject.name} 上，字段 '{field.Name}' 找到 {foundObjects.Count} 个名为 '{objectName}' 的对象。将使用第一个。", monoBehaviour.gameObject);
                }

                Component component = foundObjects[0].GetComponent(field.FieldType);
                if (component == null)
                {
                    if (autoBindAttr.Required)
                    {
                        Debug.LogError($"自动绑定失败：在 {monoBehaviour.gameObject.name} 上，对象 '{objectName}' 没有字段 '{field.Name}' 所需的 '{field.FieldType.Name}' 类型组件", monoBehaviour.gameObject);
                    }
                    continue;
                }

                field.SetValue(monoBehaviour, component);
            }
        }

        /// <summary>
        /// 递归查找指定名称的对象
        /// </summary>
        private static List<GameObject> FindObjectsRecursive(Transform parent, string name, int maxDepth, int currentDepth = 0)
        {
            List<GameObject> results = new List<GameObject>();

            if (currentDepth > maxDepth)
            {
                Debug.LogWarning($"AutoBind 警告：在 {parent.name} 下搜索 '{name}' 时达到最大深度 {maxDepth}");
                return results;
            }

            foreach (Transform child in parent)
            {
                if (child.name == name)
                {
                    results.Add(child.gameObject);
                }

                if (child.childCount > 0)
                {
                    results.AddRange(FindObjectsRecursive(child, name, maxDepth, currentDepth + 1));
                }
            }

            return results;
        }
    }
}
