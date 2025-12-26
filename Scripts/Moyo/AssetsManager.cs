using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders; // 用于 SceneInstance
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sirenix.OdinInspector;
using Moyo.Unity;

public class AssetsManager : MonoSingleton<AssetsManager>
{
    [System.Serializable]
    public class LoadOptions
    {
        public bool AutoRelease = true;
        [LabelText("超时延迟")]
        public int Timeout = 0;
        public int Priority = 0;
    }

    // 仅管理【共享资源】（如预制体本身、Texture、配置表）的句柄
    private readonly Dictionary<string, AsyncOperationHandle> assetHandles = new();
    private readonly Dictionary<string, int> referenceCount = new();

    protected override void Initialize()
    {
        assetHandles.Clear();
        referenceCount.Clear();
    }

    #region 核心资源加载 (Shared Assets)

    /// <summary>
    /// 加载共享资源 (引用计数管理)
    /// 适用于：UI面板预制体、通用贴图、音频、配置表等
    /// </summary>
    public async Task<T> LoadAssetAsync<T>(string address, LoadOptions options = null) where T : class
    {
        // 1. 检查缓存
        if (assetHandles.TryGetValue(address, out var existing))
        {
            referenceCount[address]++;
            // 如果还在加载中，等待其完成
            if (!existing.IsDone)
            {
                try
                {
                    await existing.Task;
                }
                catch
                {
                    // 这里不需要处理异常，因为下面会通过 Status 检查
                }
            }

            if (existing.Status == AsyncOperationStatus.Succeeded)
            {
                Debug.Log($"[AssetsManager] 使用缓存资源: {address}");
                return existing.Result as T;
            }
            else
            {
                // 如果缓存的句柄是失败的，清理掉并重新加载
                Debug.LogWarning($"[AssetsManager] 缓存的资源加载失败，尝试重新加载: {address}");
                ReleaseAsset(address); // 这会减少一次计数并可能移除句柄
            }
        }

        // 2. 新增加载
        var op = Addressables.LoadAssetAsync<T>(address);
        assetHandles[address] = op;

        // 初始化计数 (注意：如果上面ReleaseAsset清理了，这里就是1；如果是全新的，也是1)
        if (!referenceCount.ContainsKey(address)) referenceCount[address] = 0;
        referenceCount[address]++;

        // 3. 等待结果并处理异常
        try
        {
            var timeout = options?.Timeout ?? 0;
            if (timeout > 0)
            {
                var completed = await Task.WhenAny(op.Task, Task.Delay(timeout));
                if (completed != op.Task)
                {
                    throw new System.TimeoutException($"加载资源超时: {address}");
                }
            }

            await op.Task;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AssetsManager] 加载异常: {address}, Info: {e.Message}");
            // 异常回滚：清理刚才增加的计数和句柄
            HandleLoadFailure(address, op);
            return null;
        }

        // 4. 校验最终状态
        if (op.Status == AsyncOperationStatus.Succeeded)
        {
            return op.Result;
        }
        else
        {
            Debug.LogError($"[AssetsManager] 加载失败 Status={op.Status}: {address}");
            HandleLoadFailure(address, op);
            return null;
        }
    }

    /// <summary>
    /// 释放共享资源
    /// </summary>
    public void ReleaseAsset(string address)
    {
        if (string.IsNullOrEmpty(address)) return;
        if (!referenceCount.ContainsKey(address)) return;

        referenceCount[address]--;

        if (referenceCount[address] <= 0)
        {
            if (assetHandles.TryGetValue(address, out var handle))
            {
                Addressables.Release(handle);
                assetHandles.Remove(address);
                Debug.Log($"[AssetsManager] 释放资源: {address}");
            }
            referenceCount.Remove(address);
        }
    }

    private void HandleLoadFailure(string address, AsyncOperationHandle op)
    {
        if (referenceCount.ContainsKey(address))
        {
            referenceCount[address]--;
            if (referenceCount[address] <= 0)
            {
                referenceCount.Remove(address);
                assetHandles.Remove(address);
            }
        }
        // 释放这个失败的句柄
        Addressables.Release(op);
    }

    #endregion

    #region 实例管理 (Instancing) - 无引用计数

    /// <summary>
    /// 实例化对象 (不走引用计数管理)
    /// 注意：调用者必须负责管理该对象的生命周期，或者调用 ReleaseInstance
    /// </summary>
    public async Task<GameObject> InstantiateAsync(string address, Vector3 position = default, Quaternion rotation = default, Transform parent = null)
    {
        // 直接透传给 Addressables，不记录 Handle，也不增加 ReferenceCount
        // Addressables 内部会追踪这个 Instance 的句柄
        var op = Addressables.InstantiateAsync(address, position, rotation, parent);

        try
        {
            return await op.Task;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AssetsManager] 实例化失败: {address}, Error: {e}");
            return null;
        }
    }

    /// <summary>
    /// 销毁由 InstantiateAsync 创建的实例
    /// </summary>
    public void ReleaseInstance(GameObject instance)
    {
        if (instance == null) return;

        // Addressables 会自动查找该 GameObject 关联的 Handle 并释放
        // 同时也支持释放由 Addressables 加载的 Asset 实例化出来的普通 GO (如果释放了AssetHandle)
        // 但最稳妥的是释放 Addressables.InstantiateAsync 出来的对象
        bool success = Addressables.ReleaseInstance(instance);
        if (!success)
        {
            // 如果 Addressables 无法释放（比如不是通过 Addressables 实例化的），兜底销毁
            Debug.LogWarning($"[AssetsManager] Addressables 无法释放实例 (可能是普通对象)，执行 Destroy: {instance.name}");
            Destroy(instance);
        }
    }

    #endregion

    #region 场景管理 (Scene)

    /// <summary>
    /// 异步加载 Addressable 场景
    /// </summary>
    public async Task<SceneInstance> LoadSceneAsync(string address, LoadSceneMode mode = LoadSceneMode.Single, bool activateOnLoad = true)
    {
        var op = Addressables.LoadSceneAsync(address, mode, activateOnLoad);
        try
        {
            return await op.Task;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AssetsManager] 场景加载失败: {address}, Error: {e}");
            throw;
        }
    }

    #endregion

    #region 工具方法

    public async Task PreloadGroup(string groupLabel)
    {
        var size = await Addressables.GetDownloadSizeAsync(groupLabel).Task;
        if (size > 0)
        {
            await Addressables.DownloadDependenciesAsync(groupLabel).Task;
        }
    }

    public async Task<bool> CheckForUpdates()
    {
        var list = await Addressables.CheckForCatalogUpdates().Task;
        if (list.Count > 0)
        {
            await Addressables.UpdateCatalogs(list).Task;
            return true;
        }
        return false;
    }

    #endregion
}
