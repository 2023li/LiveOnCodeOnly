#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class SupplyEnumGenerator   // ← 不再继承 AssetPostprocessor
{
    // 你要监听的相对路径
    private const string SuppliesFolder = "Assets/GameData/Supplies";

    // 自动生成的枚举文件路径（可按需要调整）
    private const string EnumFilePath = "Assets/Scripts/GameItem/Supply/SupplyEnum_Auto.cs";

    [MenuItem("Tools/SupplyLib/Generate SupplyEnum (手动)")]
    public static void GenerateEnum()
    {
        // 找到所有 SupplyDef
        var guids = AssetDatabase.FindAssets("t:SupplyDef", new[] { SuppliesFolder });

        var names = guids
            .Select(g => AssetDatabase.GUIDToAssetPath(g))
            .Select(p => AssetDatabase.LoadAssetAtPath<SupplyDef>(p))
            .Where(d => d != null)
            .Select(d => GetEnumNameFromDef(d))
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct()
            .ToList();

        if (names.Count == 0)
        {
            Debug.LogWarning("[SupplyEnumGenerator] 未找到任何 SupplyDef，生成空的枚举。");
        }

        var sb = new StringBuilder();
        sb.AppendLine("// 自动生成，请勿手动修改");
        sb.AppendLine("// 生成时间: " + System.DateTime.Now);
        sb.AppendLine();
        sb.AppendLine("public enum SupplyEnum");
        sb.AppendLine("{");

        foreach (string n in names)
        {
            sb.AppendLine($"    {n},");
        }

        sb.AppendLine("}");

        var dir = Path.GetDirectoryName(EnumFilePath);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(EnumFilePath, sb.ToString(), Encoding.UTF8);
        AssetDatabase.Refresh();

        Debug.Log($"[SupplyEnumGenerator] 已生成 SupplyEnum，共 {names.Count} 个条目。");
    }

    private static string GetEnumNameFromDef(SupplyDef def)
    {
        if (!string.IsNullOrEmpty(def.Id))
            return def.Id;

        var name = def.name;
        name = name.Replace(" ", "_");
        return name;
    }
}
#endif
