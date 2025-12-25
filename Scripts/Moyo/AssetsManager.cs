using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
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
        public int Timeout = 0; // 0 or below disables the timeout
        public int Priority = 0;
    }

    private readonly Dictionary<string, AsyncOperationHandle> assetHandles = new();
    private readonly Dictionary<string, int> referenceCount = new();

    protected override void Initialize()
    {
        assetHandles.Clear();
        referenceCount.Clear();
    }


    // 异步加载资源
    public async Task<T> LoadAssetAsync<T>(string address, LoadOptions options = null) where T : class
    {
        if (assetHandles.TryGetValue(address, out var existing))
        {
            referenceCount[address]++;
            if (!existing.IsDone)
                await existing.Task; // 确保完成后再取 Result


            Debug.Log("使用了已加载的资源");
            return existing.Result as T; // 若第一次用 object 预热，这里 as T 仍可能 null
        }

        var op = Addressables.LoadAssetAsync<T>(address);
        assetHandles[address] = op;
        referenceCount[address] = 1;

        var timeout = options?.Timeout ?? 0;
        if (timeout > 0)
        {
            var completed = await Task.WhenAny(op.Task, Task.Delay(timeout));
            if (completed != op.Task)
            {
                Debug.LogError($"加载资源超时: {address}");
                Addressables.Release(op);
                assetHandles.Remove(address);
                referenceCount.Remove(address);
                return null;
            }
        }

        await op.Task;

        if (op.Status == AsyncOperationStatus.Succeeded)
            return op.Result;

        Debug.LogError(op.OperationException != null
            ? $"加载资源失败: {address}。错误: {op.OperationException}"
            : $"加载资源失败: {address}，状态: {op.Status}");

        if (assetHandles.ContainsKey(address))
        {
            assetHandles.Remove(address);
            referenceCount.Remove(address);
        }
        Addressables.Release(op);
        return null;
    }

    public async Task<GameObject> InstantiateAsync(string address, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        // 不再覆盖 assetHandles，实例化本身返回的句柄无需缓存（或用单独字典）
        var op = Addressables.InstantiateAsync(address, position, rotation, parent);

        if (!referenceCount.ContainsKey(address))
            referenceCount[address] = 0;
        referenceCount[address]++;

        return await op.Task;
    }

    // 释放资源
    public void ReleaseAsset(string address)
    {
        if (!referenceCount.ContainsKey(address)) return;

        referenceCount[address]--;
        if (referenceCount[address] <= 0)
        {
            if (assetHandles.TryGetValue(address, out var h))
            {
                Addressables.Release(h);
                assetHandles.Remove(address);
            }
            referenceCount.Remove(address);
        }
    }

    // 检查资源更新
    public async Task<bool> CheckForUpdates()
    {
        var catalogUpdates = await Addressables.CheckForCatalogUpdates().Task;

        if (catalogUpdates.Count > 0)
        {
            await Addressables.UpdateCatalogs(catalogUpdates).Task;

            // CRIWARE资源需要特殊处理:cite[9]
#if CRIWARE
            CriWare.Assets.CriAddressables.ModifyLocators();
#endif

            return true;
        }
        return false;
    }

    // 预加载资源组
    public async Task PreloadGroup(string groupLabel)
    {
        var size = await Addressables.GetDownloadSizeAsync(groupLabel).Task;
        if (size > 0)
        {
            var downloadOperation = Addressables.DownloadDependenciesAsync(groupLabel);
            await downloadOperation.Task;
        }
    }
}
