using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using ABAssetLoader.Locator;
using UnityEngine;

namespace ABAssetLoader.AssetLoader
{
    internal class AssetBundleAssetLoader
    {
        private readonly Dictionary<string, string[]> _bundleDependencies = new();
        private readonly Dictionary<string, AssetBundle> _loadedBundles = new();
        private readonly Dictionary<string, int> _bundleReferenceCounts = new();
        private readonly Dictionary<string, WebRequestBundleLoadRequest> _bundleLoadingRequest = new();

        private readonly AssetBundleLocator _locator = new();
        private AssetBundleContentsTable _assetBundleContentsTable;
        private CancellationTokenSource _cts = new();

        public async UniTask BuildDependency(bool unloadAllObjects, CancellationToken ct)
        {
            UnloadAll(unloadAllObjects);

            await _locator.BuildLocationMap(ct);
            await BuildBundleDependency(ct);

            // Persistent に見つからない時は Streaming の ContentTable を参照する
            _assetBundleContentsTable =
                await AssetBundleContentsTableAccessor.GetOrDefault(LocationType.Persistent, ct) ??
                await AssetBundleContentsTableAccessor.Get(LocationType.Streaming, ct);
        }

        public async UniTask<LoadedAssetHandle> LoadAssetAsync<T>(string assetPath,
            CancellationToken ct = default) where T : Object
        {
            var bundleName = _assetBundleContentsTable.GetBundleName(assetPath);
            using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, ct);
            var bundle = await LoadBundle(bundleName, linkedTokenSource.Token);

            // mock load delay
            if (ABAssetLoaderSetting.EmulateLoadingAssetDelayMilliseconds.HasValue)
                await UniTask.Delay(millisecondsDelay: ABAssetLoaderSetting.EmulateLoadingAssetDelayMilliseconds.Value,
                    cancellationToken: ct);

            var asset = await bundle.LoadAssetAsync<T>(assetPath).WithCancellation(linkedTokenSource.Token);
            return new LoadedAssetHandle(assetPath, asset);
        }

        // 発行した handle を使って Unload する.
        // Load のように assetPath 指定で Unload させると、参照カウントが一致しなくなる可能性があるため必ず handle を経由する
        public void UnloadAsset(LoadedAssetHandle handle)
        {
            var bundleName = _assetBundleContentsTable.GetBundleName(handle.Key);
            UnloadBundle(bundleName);
        }

        public void UnloadAll(bool unloadAllObjects)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            _loadedBundles.Clear();
            _bundleReferenceCounts.Clear();
            _bundleLoadingRequest.Clear();
            _bundleDependencies.Clear();
            AssetBundle.UnloadAllAssetBundles(unloadAllObjects);

            _locator.Clear();
        }

        private async UniTask BuildBundleDependency(CancellationToken ct)
        {
            AssetBundle rootBundle;
            try
            {
                rootBundle = await AssetBundle.LoadFromFileAsync($"{ABAssetLoaderSetting.StreamingAssetBundleBasePath}/{ABAssetLoaderSetting.RootBundleName}")
                    .WithCancellation(ct);
            }
            catch (UnityWebRequestException ex)
            {
                if (ex.ResponseCode != 404)
                    throw;

                // Persistent に見つからない時は Streaming の RootBundle を参照する
                rootBundle = await AssetBundle.LoadFromFileAsync(
                        $"{ABAssetLoaderSetting.PersistentAssetBundleBasePath}/{ABAssetLoaderSetting.RootBundleName}")
                    .WithCancellation(ct);
            }

            var rootManifest = rootBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
            foreach (var bundleName in rootManifest.GetAllAssetBundles())
            {
                // manifest に書かれた自身の子のバンドル名を渡す.
                // そのバンドルが依存するバンドル名を取得できる
                var dependencies = rootManifest.GetAllDependencies(bundleName);
                _bundleDependencies[bundleName] = dependencies;
                Debug.Log($"{bundleName} has dependency - {string.Join('\n', dependencies)}");
            }
        }

        private async UniTask<AssetBundle> LoadBundle(string bundleName, CancellationToken ct)
        {
            var dependencies = _bundleDependencies.GetValueOrDefault(bundleName);

            // 読み込むバンドルが依存するバンドルがあれば、先にロードしておく
            if (dependencies != null)
                await dependencies.Select(LoadImpl);

            return await LoadImpl(bundleName);

            async UniTask<AssetBundle> LoadImpl(string name)
            {
                _bundleReferenceCounts.TryAdd(name, 0);
                _bundleReferenceCounts[name]++;

                if (_loadedBundles.TryGetValue(name, out var loadedBundle) && loadedBundle != null)
                    return loadedBundle;

                var uri = _locator.GetUri(name);
                if (_bundleLoadingRequest.TryGetValue(uri, out var request))
                    return await request.Load(ct);

                _bundleLoadingRequest[uri] = new WebRequestBundleLoadRequest(uri);
                AssetBundle bundle;
                try
                {
                    bundle = await _bundleLoadingRequest[uri].Load(ct);
                }
                catch (UnityWebRequestException)
                {
                    _bundleReferenceCounts[name]--;
                    Debug.LogError($"Failed to load AssetBundle. : {name}");
                    throw;
                }
                finally
                {
                    _bundleLoadingRequest.Remove(uri);
                }

                if (ct.IsCancellationRequested && bundle != null)
                {
                    bundle.Unload(unloadAllLoadedObjects: true);
                    return null;
                }

                _loadedBundles.Add(name, bundle);
                return bundle;
            }
        }

        private void UnloadBundle(string bundleName)
        {
            UnloadImpl(bundleName);
            return;

            void UnloadImpl(string name)
            {
                _bundleReferenceCounts[name]--;
                if (_bundleReferenceCounts[name] <= 0)
                {
                    var bundle = _loadedBundles[name];
                    if (bundle == null)
                    {
                        Debug.LogWarning($"Detected attempting to unload invalid AssetBundle. : {name}");
                        return;
                    }

                    bundle.Unload(unloadAllLoadedObjects: true);
                    _loadedBundles.Remove(name);
                }

                // 依存するバンドルがあれば一緒に Unload しておく
                var dependencies = _bundleDependencies.GetValueOrDefault(name);
                if (dependencies != null)
                {
                    foreach (var dependency in dependencies)
                    {
                        UnloadImpl(name: dependency);
                    }
                }
            }
        }
    }
}