using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Sirenix.OdinInspector;

#if UNITY_EDITOR
using UnityEditor;
#endif





/// <summary>
/// 运行时：通过 Init() 加载并构建字典；
/// 编辑器未运行：GetSupplyDef() 可自动扫描 SupplyDef 并缓存；
/// 现在新增 CollectAllSupplyDefs()：一键把 SupplyDef 收集到 allSupply（默认扫描 Assets/GameData/Supplies，或全局扫描）。
/// </summary>
public class SupplyLib : ScriptableObject
{
    [SerializeField] public List<SupplyDef> allSupply = new List<SupplyDef>();
    public Dictionary<string, SupplyDef> dic_ID_SupplyDef;

    public static SupplyLib Ins;

#if UNITY_EDITOR
    private const string DefaultSuppliesFolder = "Assets/GameData/Supplies";
    private static bool s_EditorCacheReady = false;
#endif

    /// <summary>
    /// 运行时初始化：从你的资源系统加载 SupplyLib（Addressables/自研资源管理等）并构建字典。
    /// </summary>
    public static async Task Init()
    {
        if (Ins != null && Ins.dic_ID_SupplyDef != null && Ins.dic_ID_SupplyDef.Count > 0)
            return;

        // 按你的工程加载方式替换这里
        Ins = await AssetsManager.Instance.LoadAssetAsync<SupplyLib>(LOConstant.AssetsKey.Address_SupplyLib);
        if (Ins == null)
        {
            Debug.LogError("[SupplyLib] 运行时加载失败：未能获取 SupplyLib 资产。");
            return;
        }

        BuildDictionaryFromAllSupply();
    }

    /// <summary>从 allSupply 重建字典。</summary>
    private static void BuildDictionaryFromAllSupply()
    {
        if (Ins == null)
        {
            Debug.LogWarning("[SupplyLib] Ins 为空，无法构建字典。");
            return;
        }
        if (Ins.dic_ID_SupplyDef == null) Ins.dic_ID_SupplyDef = new Dictionary<string, SupplyDef>();
        else Ins.dic_ID_SupplyDef.Clear();

        if (Ins.allSupply == null) Ins.allSupply = new List<SupplyDef>();

        foreach (var item in Ins.allSupply)
        {
            if (item == null) continue;

            if (string.IsNullOrEmpty(item.Id))
            {
                Debug.LogWarning("[SupplyLib] 存在 Id 为空的 SupplyDef。");
                continue;
            }

            if (!Ins.dic_ID_SupplyDef.TryAdd(item.Id, item))
            {
                Debug.LogWarning($"[SupplyLib] 重复的 SupplyDef Id：{item.Id}");
            }
        }
    }

   

    /// <summary>
    /// 通用入口：编辑器/运行时均可使用。
    /// 运行时：依赖 Init()；编辑器未播放：自动扫描项目构建缓存。
    /// 用法：var def = SupplyLib.GetSupplyDef("food_lv1");
    /// </summary>
    public static SupplyDef GetSupplyDef(string id, bool editorFallback = true)
    {
#if UNITY_EDITOR
        // 在编辑器且未播放：自动扫描一次资产作为兜底（不要求先 Init）
        if (!Application.isPlaying && editorFallback)
        {
            EnsureEditorCache(); // 内部复用 CollectAllSupplyDefs()
            if (Ins != null && Ins.dic_ID_SupplyDef != null &&
                Ins.dic_ID_SupplyDef.TryGetValue(id, out var defFromEditor))
                return defFromEditor;

            Debug.LogWarning($"[Editor][SupplyLib] 未找到 SupplyDef，Id={id}");
            return null;
        }
#endif
        // 运行时/播放模式：依赖 Init()
        if (Ins == null || Ins.dic_ID_SupplyDef == null)
        {
            Debug.LogWarning("[SupplyLib] 运行时访问：请先调用 SupplyLib.Init() 再使用 GetSupplyDef。");
            return null;
        }
        if (Ins.dic_ID_SupplyDef.TryGetValue(id, out var def))
            return def;

        Debug.LogWarning($"[SupplyLib] 不存在 id 为 {id} 的 SupplyDef。");
        return null;
    }
    
    public static SupplyDef GetSupplyDef(SupplyEnum e)
    {
        return GetSupplyDef(e.ToString());
    }





#if UNITY_EDITOR
    /// <summary>
    /// （新增）自动收集所有 SupplyDef 到 allSupply。
    /// 默认仅扫描目录 Assets/GameData/Supplies；传入 globalScan=true 时改为全局扫描。
    /// 如 rebuildDictionary=true，会按 Id 重建字典。
    /// 返回收集到的数量。
    /// </summary>
    public static int CollectAllSupplyDefs(bool globalScan = false, bool rebuildDictionary = true)
    {
        EnsureInsForEditor();

        if (Ins.allSupply == null) Ins.allSupply = new List<SupplyDef>();
        if (Ins.dic_ID_SupplyDef == null) Ins.dic_ID_SupplyDef = new Dictionary<string, SupplyDef>();

        Ins.allSupply.Clear();
        Ins.dic_ID_SupplyDef.Clear();

        // 目录存在且非全局 -> 仅扫描默认目录；否则切换至全局扫描
        string[] guids;
        if (!globalScan && !AssetDatabase.IsValidFolder(DefaultSuppliesFolder))
        {
            Debug.LogWarning($"[SupplyLib] 未找到目录 {DefaultSuppliesFolder}，改为全局扫描。");
            globalScan = true;
        }

        if (globalScan)
        {
            guids = AssetDatabase.FindAssets("eventArg:SupplyDef");
        }
        else
        {
            guids = AssetDatabase.FindAssets("eventArg:SupplyDef", new[] { DefaultSuppliesFolder });
        }

        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                if (i % 25 == 0)
                    EditorUtility.DisplayProgressBar("Collect SupplyDef", $"扫描 {i}/{guids.Length}", (float)i / guids.Length);

                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var def = AssetDatabase.LoadAssetAtPath<SupplyDef>(path);
                if (def == null) continue;

                Ins.allSupply.Add(def);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (rebuildDictionary)
            BuildDictionaryFromAllSupply();

        s_EditorCacheReady = true;
        Debug.Log($"[SupplyLib] 收集完成：{Ins.allSupply.Count} 个 SupplyDef"
            + (globalScan ? "（全局扫描）" : $"（目录：{DefaultSuppliesFolder}）"));

        return Ins.allSupply.Count;
    }

    /// <summary>
    /// 仅编辑器：构建缓存（复用 CollectAllSupplyDefs，默认优先扫描固定目录）。
    /// </summary>
    private static void EnsureEditorCache(bool forceRefresh = false)
    {
        if (!forceRefresh && s_EditorCacheReady &&
            Ins != null && Ins.dic_ID_SupplyDef != null && Ins.dic_ID_SupplyDef.Count > 0)
            return;

        // 默认先按指定目录扫描；目录缺失时自动回退到全局扫描
        CollectAllSupplyDefs(globalScan: false, rebuildDictionary: true);
    }

    /// <summary>确保 Ins 在编辑器下可用：优先加载现有 SupplyLib 资产，没有则创建一个临时实例。</summary>
    private static void EnsureInsForEditor()
    {
        if (Ins != null) return;

        var guidsLib = AssetDatabase.FindAssets("eventArg:SupplyLib");
        if (guidsLib != null && guidsLib.Length > 0)
        {
            string libPath = AssetDatabase.GUIDToAssetPath(guidsLib[0]);
            Ins = AssetDatabase.LoadAssetAtPath<SupplyLib>(libPath);
        }
        if (Ins == null) Ins = CreateInstance<SupplyLib>();
    }

    /// <summary>菜单：只扫默认目录。</summary>
    [MenuItem("Tools/SupplyLib/Collect Supplies (Default Folder)")]
    [Button("手机默认路径下的SupplyDef")]
    public static void MenuCollectDefaultFolder()
    {
        CollectAllSupplyDefs(globalScan: false, rebuildDictionary: true);
    }

    /// <summary>菜单：全局扫描。</summary>
    [MenuItem("Tools/SupplyLib/Collect Supplies (Global)")]
    public static void MenuCollectGlobal()
    {
        CollectAllSupplyDefs(globalScan: true, rebuildDictionary: true);
    }

    /// <summary>菜单：手动刷新编辑器缓存。</summary>
    [MenuItem("Tools/SupplyLib/Refresh Editor Cache")]
    public static void RefreshEditorCacheMenu()
    {
        s_EditorCacheReady = false;
        EnsureEditorCache(true);
        Debug.Log("[SupplyLib] Editor 缓存已刷新。");
    }
#endif
}
