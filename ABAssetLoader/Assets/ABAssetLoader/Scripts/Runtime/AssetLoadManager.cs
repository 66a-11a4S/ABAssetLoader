using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using ABAssetLoader.AssetLoader;
using ABAssetLoader.Download;
using ABAssetLoader.Version;

namespace ABAssetLoader
{
    // アセットの動的ロード機能の API
    public class AssetLoadManager
    {
        private readonly AssetBundleAssetLoader _assetLoader = new();

        public async UniTask Setup(CancellationToken ct = default)
        {
            await _assetLoader.BuildDependency(unloadAllObjects: true, ct);
        }

        public async UniTask DownloadPackage(bool unloadAllObjects = true,
            Action<BundleVersion> onDownloaded = null,
            CancellationToken ct = default)
        {
            await AssetBundleDownloader.DownloadUpdated(ct, onDownloaded);
            await _assetLoader.BuildDependency(unloadAllObjects, ct);
        }

        public UniTask<LoadedAssetHandle> LoadAssetAsync<T>(string assetPath, CancellationToken ct = default)
            where T : UnityEngine.Object
        {
            return _assetLoader.LoadAssetAsync<T>(assetPath, ct);
        }

        public void UnloadAsset(LoadedAssetHandle handle) => _assetLoader.UnloadAsset(handle);

        public void UnloadAll(bool unloadAllObjects) => _assetLoader.UnloadAll(unloadAllObjects);

        public void DeleteBundleCache(bool unloadAllObjects)
        {
            UnloadAll(unloadAllObjects);
            var path = AssetLoaderSetting.PersistentDataPath;
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
    }
}