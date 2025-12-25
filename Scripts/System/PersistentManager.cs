using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Moyo;
using Moyo.Unity;
using System;
using System.IO;
using System.Linq;
using Sirenix.OdinInspector;

// 确保引用你的命名空间
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
    private const string AppDataFileName = "AppData.load";
    private const string GameSaveDirName = "GameSaves";

    // 数据文件包含完整的游戏状态
    private const string GameDataKey = "GameData";
    private const string GameFileExtension = ".logd";

    // [新] 元数据文件仅包含列表显示所需的信息
    private const string MetaDataKey = "MetaData";
    private const string MetaFileExtension = ".meta";

    private string GameSaveRootPath => Path.Combine(Application.persistentDataPath, GameSaveDirName);

    #region AppData (全局设置)
    // ... (保持原有的 SaveAppData 和 LoadAppData 不变) ...
    public void SaveAppData()
    {
        if (appData == null) return;
        ES3.Save("appData", appData, AppDataFileName);
    }

    public void LoadAppData()
    {
        if (ES3.FileExists(AppDataFileName))
        {
            appData = ES3.Load<AppSaveData>("appData", AppDataFileName);
        }
        else
        {
            appData = AppSaveData.GetDef();
            SaveAppData();
        }
    }
    #endregion

    #region GameData (游戏存档)

    // CollectCurrentGameSaveData 方法保持你修复后的样子，这里略去不写
    private GameSaveData CollectCurrentGameSaveData()
    {
        if (currentGameData == null) { currentGameData = GameSaveData.CreateNew(); }

        //收集建筑数据
        List<BuildingInstance.BuildingSaveData> buildingSaveDatas = new();
        //遍历所有的激活的建筑
        foreach (var building in BuildingInstance.ActiveInstances)
        {
            buildingSaveDatas.Add(building.Save());
        }
        currentGameData.allBuildingData = buildingSaveDatas;

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
        // 假设你已经修复了 CollectCurrentGameSaveData 中的赋值问题
        SaveGame(CollectCurrentGameSaveData());
    }

    public void SaveGame(GameSaveData data)
    {
        if (data == null)
        {
            Debug.LogError("试图保存空的 GameSaveData！");
            return;
        }

        if (!Directory.Exists(GameSaveRootPath))
        {
            Directory.CreateDirectory(GameSaveRootPath);
        }

        // 1. 处理 ID 和 时间
        if (string.IsNullOrEmpty(data.saveid))
        {
            data.saveid = System.Guid.NewGuid().ToString();
        }
        data.lastSaveDate = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // 2. 保存完整数据 (.logd)
        string contentFileName = $"{data.saveid}{GameFileExtension}";
        string contentPath = Path.Combine(GameSaveDirName, contentFileName);
        ES3.Save(GameDataKey, data, contentPath);

        // 3. [新] 保存元数据 (.meta)
        // 提取元数据
        SaveMetadata meta = new SaveMetadata
        {
            saveid = data.saveid,
            saveName = data.saveName,
            lastSaveDate = data.lastSaveDate,
            versionNumber = data.versionNumber
        };

        string metaFileName = $"{data.saveid}{MetaFileExtension}";
        string metaPath = Path.Combine(GameSaveDirName, metaFileName);
        ES3.Save(MetaDataKey, meta, metaPath);

        currentGameData = data;
        Debug.Log($"[PersistentManager] Game saved: {contentPath} & {metaPath}");
    }

    #region 加载
    public void LoadGame(string saveid)
    {
        // 1. 读取存档文件到内存 (currentGameData)
        var data = LoadGameData(saveid);
        if (data == null)
        {
            Debug.LogError($"[PersistentManager] 无法加载存档: {saveid}");
            return;
        }

        Debug.Log($"[PersistentManager] 开始加载游戏: {data.saveName} ({data.saveid})");

        // 2. 切换到游戏场景 (假设场景名为 "GameMain" 或你定义的常量)
        // 注意：这里借用 AppManager 来开启协程，因为 PersistentManager 可能不是 MonoBehaviour
        AppManager.Instance.LoadScene("GameMain", true); // true 表示使用过渡页

        // 3. 开启协程等待场景加载完成，然后恢复数据
        AppManager.Instance.StartCoroutine(RestoreGameRoutine());
    }
    /// <summary>
    /// 等待场景加载并恢复数据的协程
    /// </summary>
    private IEnumerator RestoreGameRoutine()
    {
        // 等待直到当前场景变为游戏场景
        // 注意：如果你使用了过渡场景，这里需要确保逻辑是正确的。
        // AppManager.LoadScene 通常是异步的，我们需要等待目标场景激活。
        yield return new WaitUntil(() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "GameMain");

        // 等待一帧，确保场景内所有 Monobehaviour 的 Awake/Start 已执行
        yield return null;

        // 4. 执行数据恢复
        RestoreGameState();
    }
    /// <summary>
    /// 将内存中的 currentGameData 应用到当前游戏世界
    /// </summary>
    private void RestoreGameState()
    {
        if (currentGameData == null)
        {
            Debug.LogError("[PersistentManager] 数据恢复失败：currentGameData 为空");
            return;
        }

        Debug.Log("[PersistentManager] 正在重建游戏世界...");



        //需要立刻马上重建建筑
        ReconstructBuildings();

        // --- A. 恢复子系统数据 ---
        RecoverGameContext();


        // --- C. 触发全局事件 ---
        // 通知 UI 刷新，或者关闭遮罩
        AppStateEvent.Tiggle(AppState.游戏加载完成);

        Debug.Log("[PersistentManager] 游戏加载完成！");


    }

    /// <summary>
    /// 根据存档重建场景中的建筑
    /// </summary>
    private void ReconstructBuildings()
    {
        // 1. 清理当前场景可能残留的建筑 (如果是重新开始或覆盖加载)
        //BuildingManager.Instance.ClearAll(); // 如果你有这个方法的话

        BuildingInstance.ClearAll();

        if (currentGameData.allBuildingData == null) return;

        foreach (BuildingInstance.BuildingSaveData bData in currentGameData.allBuildingData)
        {
            BuildingBuilder.Instance.TryCreateBuildingByData(bData,out BuildingInstance ins);
            
        }
    }

    private void RecoverGameContext()
    {
        // 1. 恢复回合与时间
        if (TurnSystem.Instance != null && currentGameData.turnSystemSaveData != null)
            TurnSystem.Instance.Load(currentGameData.turnSystemSaveData);

        // 2. 恢复资源网络 (库存、上限等)
        if (GameContext.Instance.ResourceNetwork != null && currentGameData.resourceNetworkSaveData != null)
            GameContext.Instance.ResourceNetwork.Load(currentGameData.resourceNetworkSaveData);

        // 3. 恢复科技树
        if (GameContext.Instance.TechTree != null && currentGameData.techTreeSaveData != null)
            GameContext.Instance.TechTree.Load(currentGameData.techTreeSaveData);

        // 4. 恢复人力资源
        if (GameContext.Instance.HumanResourcesNetwork != null && currentGameData.humanResourcesNetworkSaveData != null)
            GameContext.Instance.HumanResourcesNetwork.Load(currentGameData.humanResourcesNetworkSaveData);

        // 5. 恢复连接管理器
        if (ConnectionManager.Instance != null && currentGameData.connectionManagerSaveData != null)
            ConnectionManager.Instance.Load(currentGameData.connectionManagerSaveData);

    }



    #endregion


    /// <summary>
    /// 获取所有存档列表（高性能版）
    /// 优先读取 .meta 小文件。
    /// </summary>
    public List<GameSaveData> GetAllSaves()
    {
        List<GameSaveData> saves = new List<GameSaveData>();

        if (!Directory.Exists(GameSaveRootPath))
        {
            Directory.CreateDirectory(GameSaveRootPath);
            return saves;
        }

        // 1. 获取所有 .logd 文件（主数据文件），以此为基准
        string[] dataFiles = Directory.GetFiles(GameSaveRootPath, "*" + GameFileExtension);

        foreach (var fullDataPath in dataFiles)
        {
            try
            {
                string fileName = Path.GetFileNameWithoutExtension(fullDataPath); // 获取 saveid
                string saveid = fileName; // 文件名即ID

                // 构建对应的 .meta 文件路径
                string metaRelativePath = Path.Combine(GameSaveDirName, saveid + MetaFileExtension);

                GameSaveData headerData = null;

                // 2. 检查是否存在对应的 .meta 文件
                if (ES3.FileExists(metaRelativePath))
                {
                    // [快路径] 只加载极小的元数据文件
                    SaveMetadata meta = ES3.Load<SaveMetadata>(MetaDataKey, metaRelativePath);

                    // 将元数据转换为 GameSaveData (仅填充头部信息)
                    headerData = new GameSaveData
                    {
                        saveid = meta.saveid,
                        saveName = meta.saveName,
                        lastSaveDate = meta.lastSaveDate,
                        versionNumber = meta.versionNumber
                        // 注意：其他字段如 allBuildingData 此时为 null
                    };
                }
                else
                {
                    // [慢路径/兼容路径] 只有数据文件，没有元数据（通常是旧版本的存档）
                    // 此时我们不得不加载完整文件，但顺便生成一个 .meta 方便下次快速读取
                    Debug.LogWarning($"[PersistentManager] 存档 {saveid} 缺少元数据，正在执行自动修复...");

                    string dataRelativePath = Path.Combine(GameSaveDirName, saveid + GameFileExtension);
                    headerData = ES3.Load<GameSaveData>(GameDataKey, dataRelativePath);

                    if (headerData != null)
                    {
                        // 立即补救：生成 .meta 文件
                        SaveMetadata newMeta = new SaveMetadata
                        {
                            saveid = headerData.saveid,
                            saveName = headerData.saveName,
                            lastSaveDate = headerData.lastSaveDate,
                            versionNumber = headerData.versionNumber
                        };
                        ES3.Save(MetaDataKey, newMeta, metaRelativePath);
                    }
                }

                if (headerData != null)
                {
                    saves.Add(headerData);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PersistentManager] 读取存档列表失败: {fullDataPath}, Error: {e.Message}");
            }
        }

        return saves.OrderByDescending(x => x.lastSaveDate).ToList();
    }

    /// <summary>
    /// 读取实际游戏数据
    /// </summary>
    public GameSaveData LoadGameData(string saveid)
    {
        string fileName = $"{saveid}{GameFileExtension}";
        string relativePath = Path.Combine(GameSaveDirName, fileName);

        if (ES3.FileExists(relativePath))
        {
            try
            {
                // 这里仍然加载完整的 .logd 文件
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

    public void DeleteGame(string saveid)
    {
        // 删除数据文件
        string dataFile = Path.Combine(GameSaveDirName, $"{saveid}{GameFileExtension}");
        if (ES3.FileExists(dataFile))
        {
            ES3.DeleteFile(dataFile);
        }

        // 删除元数据文件
        string metaFile = Path.Combine(GameSaveDirName, $"{saveid}{MetaFileExtension}");
        if (ES3.FileExists(metaFile))
        {
            ES3.DeleteFile(metaFile);
        }

        Debug.Log($"[PersistentManager] Deleted save and metadata: {saveid}");
    }

    // LoadGame() 方法仍然需要你根据之前的建议去实现具体的场景恢复逻辑
    public void LoadGame()
    {
        // 建议流程：
        // 1. StartCoroutine(LoadGameSequence());
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
public class SaveMetadata
{
    public string saveid;
    public string saveName;
    public string lastSaveDate;
    public string versionNumber;
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
