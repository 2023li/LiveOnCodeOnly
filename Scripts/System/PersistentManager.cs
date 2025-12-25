using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Moyo;
using Moyo.Unity;
using System;
using System.IO;
using System.Linq; // 用于方便的列表排序
using Sirenix.OdinInspector;
using static TechTreeManager;

public class PersistentManager : Singleton<PersistentManager>
{
    protected PersistentManager() { }

    // --- 数据引用 ---
    [ShowInInspector]
    public AppSaveData appData;
    [ShowInInspector]
    public GameSaveData currentGameData;

    // --- 路径与常量定义 ---
    private const string AppDataFileName = "AppData.load"; // 全局设置文件名
    private const string GameSaveDirName = "GameSaves";   // 游戏存档文件夹名
    private const string GameDataKey = "GameData";        // ES3文件内部的Key
    private const string GameFileExtension = ".logd";

    // 获取游戏存档的根目录路径 (PersistentDataPath/GameSaves)
    private string GameSaveRootPath => Path.Combine(Application.persistentDataPath, GameSaveDirName);

    #region AppData (全局设置)

    public void SaveAppData()
    {
        if (appData == null)
        {
            Debug.LogWarning("AppData为空");
            return;
        }

        // 直接保存在根目录下
        ES3.Save("appData", appData, AppDataFileName);
        Debug.Log($"[PersistentManager] AppData saved.");
    }

    public void LoadAppData()
    {
        if (ES3.FileExists(AppDataFileName))
        {
            appData = ES3.Load<AppSaveData>("appData", AppDataFileName);
            Debug.Log("已加载App数据");
        }
        else
        {
            appData = AppSaveData.GetDef();
            Debug.Log("已创建默认数据");
            SaveAppData();
        }
    }

    #endregion

    #region GameData (游戏存档)

    /// <summary>
    /// 收集游戏数据
    /// </summary>
    /// <returns></returns>
    private GameSaveData CollectCurrentGameSaveData()
    {
        if (currentGameData == null){ currentGameData = GameSaveData.CreateNew(); }

        //收集建筑数据
        List<BuildingInstance.BuildingSaveData> buildingSaveDatas = new();
        //遍历所有的激活的建筑
        foreach (var building in BuildingInstance.ActiveInstances)
        {
            buildingSaveDatas.Add(building.Save());
        }

        //游戏上下文数据
        currentGameData.turnSystemSaveData = TurnSystem.Instance.Save();
        currentGameData.humanResourcesNetworkSaveData = GameContext.Instance.HumanResourcesNetwork.Save();
        currentGameData.techTreeSaveData = GameContext.Instance.TechTree.Save();
        currentGameData.resourceNetworkSaveData = GameContext.Instance.ResourceNetwork.Save();
        currentGameData.connectionManagerSaveData = ConnectionManager.Instance.Save();


        return currentGameData;
    }


    public void SaveGame()
    {
        SaveGame(CollectCurrentGameSaveData());
    }
    /// <summary>
    /// 保存游戏数据
    /// 路径: PersistentDataPath/GameSaves/{saveid}.es3
    /// </summary>
    public void SaveGame(GameSaveData data)
    {
        if (data == null)
        {
            Debug.LogError("试图保存空的 GameSaveData！");
            return;
        }

        // --- 修复 1: 确保文件夹存在 (解决 DirectoryNotFoundException) ---
        if (!Directory.Exists(GameSaveRootPath))
        {
            Directory.CreateDirectory(GameSaveRootPath);
        }
        //检测ID
        if (string.IsNullOrEmpty(data.saveid))
        {
            data.saveid = System.Guid.NewGuid().ToString();
            Debug.Log($"[PersistentManager] 为存档生成了新的 ID: {data.saveid}");
        }

        data.lastSaveDate = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // --- 修复 2: 移除多余的点 ---
        // 原代码: $"{data.saveid}.{GameFileExtension}" -> "ID..logd" (两个点)
        // 修改后: 既然 GameFileExtension 包含了点，这里就不要加点了
        string fileName = $"{data.saveid}{GameFileExtension}";
        string relativePath = Path.Combine(GameSaveDirName, fileName);

        ES3.Save(GameDataKey, data, relativePath);

        currentGameData = data;
        Debug.Log($"[PersistentManager] Game saved: {relativePath} (Name: {data.saveName})");
    }



    public void LoadGame()
    {





    }


    /// <summary>
    /// 读取指定 ID 的游戏数据
    /// </summary>
    /// <param name="saveid">存档的唯一ID (对应文件名)</param>
    public GameSaveData LoadGameData(string saveid)
    {
        // 同样去掉多余的点
        string fileName = $"{saveid}{GameFileExtension}";
        string relativePath = Path.Combine(GameSaveDirName, fileName);

        if (ES3.FileExists(relativePath))
        {
            try
            {
                GameSaveData data = ES3.Load<GameSaveData>(GameDataKey, relativePath);
                currentGameData = data;
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[PersistentManager] Load failed for ID {saveid}: {e.Message}");
                return null;
            }
        }
        else
        {
            Debug.LogWarning($"[PersistentManager] Save file not found: {relativePath}");
            return null;
        }
    }

    


    /// <summary>
    /// 删除指定存档
    /// </summary>
    public void DeleteGame(string saveid)
    {
        // --- 修复 4: 移除文件名中的空格和多余的点 ---
        // 原代码: $"{saveid}. {GameFileExtension}" -> "ID. .logd" (有点和空格)
        string fileName = $"{saveid}{GameFileExtension}";
        string relativePath = Path.Combine(GameSaveDirName, fileName);

        if (ES3.FileExists(relativePath))
        {
            ES3.DeleteFile(relativePath);
            Debug.Log($"[PersistentManager] Deleted save: {saveid}");
        }
    }

    /// <summary>
    /// 获取所有有效的存档数据列表
    /// (用于在 UI 上显示存档列表，按时间倒序排列)
    /// </summary>
    public List<GameSaveData> GetAllSaves()
    {
        List<GameSaveData> saves = new List<GameSaveData>();

        if (!Directory.Exists(GameSaveRootPath))
        {
            Directory.CreateDirectory(GameSaveRootPath);
            return saves;
        }

        // --- 修复 3: 搜索正确的后缀名 ---
        // 原代码: "*.es3" -> 找不到 .logd 文件
        string searchPattern = "*" + GameFileExtension;
        string[] files = Directory.GetFiles(GameSaveRootPath, searchPattern);

        foreach (var fullPath in files)
        {
            try
            {
                string fileName = Path.GetFileName(fullPath);
                string relativePath = Path.Combine(GameSaveDirName, fileName);

                // 注意：这里加载整个对象可能较慢，如果只显示UI列表，建议只加载 Header
                var data = ES3.Load<GameSaveData>(GameDataKey, relativePath);
                if (data != null)
                {
                    saves.Add(data);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PersistentManager] 跳过损坏的存档文件: {fullPath}, Error: {e.Message}");
            }
        }

        return saves.OrderByDescending(x => x.lastSaveDate).ToList();
    }

    #endregion
}

[Serializable]
public class AppSaveData
{
    public bool firstStartup;
    public bool firstGame;
    public AppLanguage language;
    public AudioManager.AudioSaveData audioSaveData;

    public static AppSaveData GetDef()
    {
        return new AppSaveData
        {
            firstStartup = true,
            firstGame = true,
            language = AppLanguage.简体中文,
            audioSaveData = AudioManager.AudioSaveData.GetDef()

        };
    }
}

[Serializable]
public class GameSaveData
{
    // --- 元数据 ---
    public string saveid;       // 唯一ID，对应文件名 (GUID)
    public string saveName;     // 玩家给存档起的名字 (显示用)
    public string lastSaveDate; // 保存时间
    public string versionNumber;

    // --- 游戏内容数据 ---
    public ResourceNetworkSaveData resourceNetworkSaveData;
    public HumanResourcesNetworkSaveData humanResourcesNetworkSaveData;
    public TechSystemSaveData techTreeSaveData;
    public TurnSystemSaveData turnSystemSaveData;
    public ConnectionManagerSaveData connectionManagerSaveData;
    public List<BuildingInstance.BuildingSaveData> allBuildingData;


    // 创建新游戏的工厂方法
    public static GameSaveData CreateNew()
    {
        GameSaveData tData = new GameSaveData();
        tData.saveid = System.Guid.NewGuid().ToString(); // 初始化时就生成ID
        return tData;
    }
}
