using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEngine.Tilemaps; // 需安装 2D Tilemap Extras

public class HexTileGenerator : OdinEditorWindow
{
    [MenuItem("Tools/Hex Tile Generator (All-in-One)")]
    private static void OpenWindow()
    {
        GetWindow<HexTileGenerator>().Show();
    }

    // ============================================================
    // 全局设置
    // ============================================================
    [Title("全局配置")]
    [FolderPath, LabelText("Sprite 保存路径")]
    public string spriteFolderPath = "Assets/Art/HexSprites";

    [LabelText("文件名前缀")]
    public string filePrefix = "hex256_"; // 建议使用 hex_ 这种短前缀

    // ============================================================
    // 分页：步骤 1 - 生成贴图
    // ============================================================
    [TabGroup("步骤 1: 生成贴图")]
    [Title("源素材 (单边)")]
    [InfoBox("请拖入单边 PNG。'1' 代表该方向有边框（即无邻居）。\n对应二进制顺序：右上、右、右下、左下、左、左上。")]

    [LabelText("右上 (100000)"), PreviewField(50, ObjectFieldAlignment.Left)]
    public Texture2D texTR;
    [LabelText("右侧 (010000)"), PreviewField(50, ObjectFieldAlignment.Left)]
    public Texture2D texR;
    [LabelText("右下 (001000)"), PreviewField(50, ObjectFieldAlignment.Left)]
    public Texture2D texBR;
    [LabelText("左下 (000100)"), PreviewField(50, ObjectFieldAlignment.Left)]
    public Texture2D texBL;
    [LabelText("左侧 (000010)"), PreviewField(50, ObjectFieldAlignment.Left)]
    public Texture2D texL;
    [LabelText("左上 (000001)"), PreviewField(50, ObjectFieldAlignment.Left)]
    public Texture2D texTL;

    [TabGroup("步骤 1: 生成贴图")]
    [Button("1. 生成所有 64 张组合图片", ButtonSizes.Large), GUIColor(0, 1, 0)]
    public void GenerateAllImages()
    {
        Texture2D[] sources = { texTR, texR, texBR, texBL, texL, texTL };
        if (!CheckTextures(sources)) return;

        if (!Directory.Exists(spriteFolderPath)) Directory.CreateDirectory(spriteFolderPath);

        int width = sources[0].width;
        int height = sources[0].height;

        // 遍历 0 - 63
        for (int i = 0; i < 64; i++)
        {
            string binaryCode = IntToBinaryString(i); // 例如 "101010"
            string fileName = $"{filePrefix}{binaryCode}.png";

            // 创建画布
            Texture2D finalTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] finalPixels = new Color[width * height];
            for (int k = 0; k < finalPixels.Length; k++) finalPixels[k] = Color.clear;

            // 遍历 6 个方向位
            // binaryCode[0] 是最高位 (右上), 对应 sources[0]
            for (int dirIndex = 0; dirIndex < 6; dirIndex++)
            {
                if (binaryCode[dirIndex] == '1')
                {
                    BlendPixels(ref finalPixels, sources[dirIndex]);
                }
            }

            // 保存
            finalTex.SetPixels(finalPixels);
            finalTex.Apply();
            byte[] bytes = finalTex.EncodeToPNG();
            string fullPath = Path.Combine(spriteFolderPath, fileName);
            File.WriteAllBytes(fullPath, bytes);
            DestroyImmediate(finalTex);
        }

        AssetDatabase.Refresh();
        Debug.Log($"<color=green>成功生成 64 张图片至: {spriteFolderPath}</color>");
    }

    // ============================================================
    // 分页：步骤 2 - 生成 RuleTile
    // ============================================================
    [TabGroup("步骤 2: 生成 RuleTile")]
    [FolderPath, LabelText("Tile 保存路径")]
    public string tileSavePath = "Assets/Art/Tiles";

    [TabGroup("步骤 2: 生成 RuleTile")]
    [LabelText("Tile 资产名称")]
    public string targetTileName = "HexRuleTile_Generated";

    [TabGroup("步骤 2: 生成 RuleTile")]
    [Button("2. 检测图片并生成 RuleTile", ButtonSizes.Large), GUIColor(0.6f, 0.8f, 1f)]
    public void GenerateRuleTileAsset()
    {
        if (string.IsNullOrEmpty(tileSavePath) || string.IsNullOrEmpty(spriteFolderPath))
        {
            Debug.LogError("请确保路径设置正确！");
            return;
        }

        // 1. 确保目录存在
        if (!Directory.Exists(tileSavePath)) Directory.CreateDirectory(tileSavePath);

        // 2. 创建或加载 RuleTile
        string tilePath = $"{tileSavePath}/{targetTileName}.asset";
        HexagonalRuleTile tile = AssetDatabase.LoadAssetAtPath<HexagonalRuleTile>(tilePath);

        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<HexagonalRuleTile>();
            AssetDatabase.CreateAsset(tile, tilePath);
        }

        // 3. 清除旧规则
        tile.m_TilingRules.Clear();

        // 4. 遍历 0-63 创建规则
        int missingCount = 0;
        for (int i = 0; i < 64; i++)
        {
            string binaryCode = IntToBinaryString(i);
            string fileName = $"{filePrefix}{binaryCode}";

            // 加载 Sprite
            string spriteAssetPath = $"{spriteFolderPath}/{fileName}.png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spriteAssetPath);

            if (sprite == null)
            {
                missingCount++;
                continue; // 缺失图片则跳过该规则
            }

            RuleTile.TilingRule rule = new RuleTile.TilingRule();
            rule.m_Sprites = new Sprite[] { sprite };
            rule.m_Output = RuleTile.TilingRuleOutput.OutputSprite.Single;

            // 设置邻居规则 (关键逻辑修正)
            // 顺序: 0:右上, 1:右, 2:右下, 3:左下, 4:左, 5:左上
            rule.m_Neighbors = new List<int>(6);

            for (int bitIndex = 0; bitIndex < 6; bitIndex++)
            {
                char bitChar = binaryCode[bitIndex];

                // 【修正 2】使用 .Add() 依次添加，不要使用下标访问
                if (bitChar == '1')
                {
                    // 1 = 有边框 = 被阻挡 = NotThis
                    rule.m_Neighbors.Add(RuleTile.TilingRule.Neighbor.NotThis);
                }
                else
                {
                    // 0 = 无边框 = 通透 = This
                    rule.m_Neighbors.Add(RuleTile.TilingRule.Neighbor.This);
                }
            }

            tile.m_TilingRules.Add(rule);
        }

        // 5. 设置默认 Sprite (通常设为无边的，即全连通 000000)
        string defaultSpritePath = $"{spriteFolderPath}/{filePrefix}000000.png";
        Sprite defaultSprite = AssetDatabase.LoadAssetAtPath<Sprite>(defaultSpritePath);
        if (defaultSprite != null) tile.m_DefaultSprite = defaultSprite;

        EditorUtility.SetDirty(tile);
        AssetDatabase.SaveAssets();

        if (missingCount > 0)
            Debug.LogWarning($"RuleTile 生成完毕，但有 {missingCount} 个规则因缺失图片而被跳过。");
        else
            Debug.Log($"<color=cyan>RuleTile 完美生成！共 {tile.m_TilingRules.Count} 条规则。</color>");
    }

    // ============================================================
    // 辅助方法
    // ============================================================

    private string IntToBinaryString(int number)
    {
        return System.Convert.ToString(number, 2).PadLeft(6, '0');
    }

    private bool CheckTextures(Texture2D[] textures)
    {
        foreach (var t in textures)
        {
            if (t == null)
            {
                Debug.LogError("请填满所有6个方向的源贴图！");
                return false;
            }
            try
            {
                t.GetPixels(); // 测试是否开启 Read/Write
            }
            catch (UnityException)
            {
                Debug.LogError($"贴图 {t.name} 未开启 Read/Write Enabled，请在 Import Settings 中开启。");
                return false;
            }
        }
        return true;
    }

    // 像素混合逻辑 (Alpha Blending)
    private void BlendPixels(ref Color[] basePixels, Texture2D overlayTex)
    {
        Color[] overlayPixels = overlayTex.GetPixels();
        for (int i = 0; i < basePixels.Length; i++)
        {
            Color src = overlayPixels[i];
            Color dst = basePixels[i];

            if (src.a <= 0.01f) continue;

            float outAlpha = src.a + dst.a * (1f - src.a);
            if (outAlpha > 0)
            {
                Color outColor = (src * src.a + dst * dst.a * (1f - src.a)) / outAlpha;
                outColor.a = outAlpha;
                basePixels[i] = outColor;
            }
        }
    }
}
