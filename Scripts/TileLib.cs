using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum GameTileEnum
{
    Tile_默认 = 0,
    Tile_浅绿色 = 1,
    Tile_中绿色 = 2,
    Tile_深绿色 = 3,
    Tile_深红色 = 4,
    Tile_红色 = 5,
    Tile_黄色 = 6,
    Tile_边框 = 7,
}

public class TileLib : ScriptableObject
{
    [SerializeField] private List<StructKV<GameTileEnum, TileBase>> AllTiles;

    private Dictionary<GameTileEnum, TileBase> dic_AllTiles;
    private static TileLib ins;
    private static bool loggedInitFailure = false;

    /// <summary>
    /// 获取对应的 Tile；若资源仍在加载或加载失败则返回 null。
    /// </summary>
    public static TileBase GetTile(GameTileEnum e)
    {

        if (ins == null || ins.dic_AllTiles == null)
        {
            if (!loggedInitFailure)
            {
                Debug.LogWarning("[TileLib] TileLib 尚未加载完成或初始化失败，返回 null");
                loggedInitFailure = true;
            }

            return null;
        }

        return ins.dic_AllTiles.TryGetValue(e, out var tile) ? tile : null;
    }

    /// <summary>
    /// 确保静态 TileLib 实例加载完成；若异步仍在进行则直接返回，等待下一次访问。
    /// </summary>
    public static async Task Init()
    {
        
        
         ins = await AssetsManager.Instance.LoadAssetAsync<TileLib>("TileLib");
           
         BuildTileDictionary();

        Debug.Log("TileLib初始化完毕");
    }

    private static void BuildTileDictionary()
    {
        if (ins == null) return;

        if (ins.dic_AllTiles == null)
        {
            ins.dic_AllTiles = new Dictionary<GameTileEnum, TileBase>(ins.AllTiles?.Count ?? 0);
        }
        else
        {
            ins.dic_AllTiles.Clear();
        }

        if (ins.AllTiles == null) return;

        foreach (var item in ins.AllTiles)
        {
            // 使用索引器可避免重复键抛异常，后写覆盖前写
            ins.dic_AllTiles[item.Value1] = item.Value2;
        }
    }
}
