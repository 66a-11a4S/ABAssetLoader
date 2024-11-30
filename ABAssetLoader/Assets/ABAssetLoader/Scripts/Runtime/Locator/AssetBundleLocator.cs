using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using ABAssetLoader.Version;

namespace ABAssetLoader.Locator
{
    // ファイル名と versionManifest から、ファイルが remote / persistent / streaming のどこに所属するか解決して取得する
    internal class AssetBundleLocator
    {
        private Dictionary<string, AbstractFileLocatorFactory.AbstractFileLocator> _locatorTable = new();

        public async UniTask BuildLocationMap(CancellationToken ct)
        {
            var persistent = await VersionManifestAccessor.GetOrDefault(LocationType.Persistent, ct) ??
                             VersionManifest.CreateEmpty();
            var streaming = await VersionManifestAccessor.Get(LocationType.Streaming, ct);
            var allLocalBundlePath = streaming.BundleVersions.Values.Select(x => x.FilePath)
                .Concat(persistent.BundleVersions.Values.Select(x => x.FilePath))
                .Distinct();

            var table = new Dictionary<string, AbstractFileLocatorFactory.AbstractFileLocator>();
            foreach (var path in allLocalBundlePath)
            {
                var streamingVersion = streaming.BundleVersions.GetValueOrDefault(path);
                var persistentVersion = persistent.BundleVersions.GetValueOrDefault(path);

                // アセット更新で persistent に追加された
                if (streamingVersion == null)
                {
                    table.Add(path, AbstractFileLocatorFactory.CreateLocator(LocationType.Persistent, path));
                    continue;
                }

                // バイナリ更新で streaming に追加された
                if (persistentVersion == null)
                {
                    table.Add(path, AbstractFileLocatorFactory.CreateLocator(LocationType.Streaming, path));
                    continue;
                }

                // どっちにもある場合は更新時間が新しい方を優先する
                if (persistentVersion.LastWriteTime <= streamingVersion.LastWriteTime)
                    table.Add(path, AbstractFileLocatorFactory.CreateLocator(LocationType.Streaming, path));
                else
                    table.Add(path, AbstractFileLocatorFactory.CreateLocator(LocationType.Persistent, path));
            }

            _locatorTable = table;
        }

        public string GetUri(string bundleName)
        {
            if (_locatorTable.TryGetValue(bundleName, out var result))
                return result.PathForWebRequest;

            UnityEngine.Debug.LogWarning($"LatestInfo for {bundleName} is not found");
            return null;
        }

        public void Clear() => _locatorTable.Clear();
    }
}