using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using ABAssetLoader.Locator;
using ABAssetLoader.Version;
using UnityEngine.Networking;

namespace ABAssetLoader.Download
{
    public static class AssetBundleDownloader
    {
        public static async UniTask<long> CalculateDownloadSize(CancellationToken ct)
        {
            var remoteManifest = await VersionManifestAccessor.Get(LocationType.Remote, ct);
            var localManifest = await VersionManifestAccessor.GetOrDefault(LocationType.Persistent, ct) ??
                                     await VersionManifestAccessor.Get(LocationType.Streaming, ct);
            var updatedBundles = CalculateUpdatedBundles(localManifest, remoteManifest);
            return updatedBundles.Select(version => version.ByteSize).Sum();
        }

        public static async UniTask DownloadUpdated(CancellationToken ct,
            Action<BundleVersion> onDownloaded = null)
        {
            var remoteManifest = await VersionManifestAccessor.Get(LocationType.Remote, ct);
            var localManifest = await VersionManifestAccessor.GetOrDefault(LocationType.Persistent, ct) ??
                                     await VersionManifestAccessor.Get(LocationType.Streaming, ct);
            var updatedTargets = CalculateUpdatedBundles(localManifest, remoteManifest);

            try
            {
                await DownloadBundles(updatedTargets, localManifest, onDownloaded, ct);
                await Download(CreateUri(LocationType.Remote, AssetLoaderSetting.RootBundleName),
                    filePath: AssetLoaderSetting.RootBundleName,
                    downloadTo: AssetLoaderSetting.PersistentAssetBundleBasePath,
                    ct);
                await Download(CreateUri(LocationType.Remote, AssetLoaderSetting.RootBundleName + ".manifest"),
                    filePath: AssetLoaderSetting.RootBundleName + ".manifest",
                    downloadTo: AssetLoaderSetting.PersistentAssetBundleBasePath,
                    ct);
                await Download(CreateUri(LocationType.Remote, AssetLoaderSetting.ContentsTableName),
                    filePath: AssetLoaderSetting.ContentsTableName,
                    downloadTo: AssetLoaderSetting.PersistentAssetBundleBasePath,
                    ct);
            }
            finally
            {
                // 全ての DL が終了すれば persistentManifest と remoteManifest の中身が同じになる.
                // 途中で終了した場合、そこまでの persistentManifest を保存する
                VersionManifestAccessor.SavePersistentManifest(localManifest);
            }
        }

        private static IReadOnlyCollection<BundleVersion> CalculateUpdatedBundles(
            VersionManifest localManifest, VersionManifest remoteManifest)
        {
            var updatedTargets = new Dictionary<string, BundleVersion>();

            // remote にあって local にないなら新規追加
            // remote にあって local にあるとき persistent.LastWriteTime < remote.LastWriteTime なら更新された
            // 他のケースはアセットが削除されたケースだが、個別削除は非対応 (キャッシュ削除 -> 一括DL で対応)
            foreach (var remoteVersion in remoteManifest.BundleVersions.Values)
            {
                // remote にあって local にあるとき
                if (localManifest.BundleVersions.TryGetValue(remoteVersion.FilePath, out var localVersion))
                {
                    // 更新されていたら追加
                    if (remoteVersion.IsNewerThan(localVersion))
                        updatedTargets[localVersion.FilePath] = remoteVersion;
                }
                else
                {
                    // local になければ新規追加
                    updatedTargets[remoteVersion.FilePath] = remoteVersion;
                }
            }

            return updatedTargets.Values;
        }

        private static async UniTask DownloadBundles(IEnumerable<BundleVersion> downloadTargets,
            VersionManifest localManifest, Action<BundleVersion> onDownloaded, CancellationToken ct)
        {
            foreach (var target in downloadTargets)
            {
                if (ct.IsCancellationRequested)
                    break;

                // mock download delay
                if (AssetLoaderSetting.EmulateDownloadDelayMilliseconds.HasValue)
                    await UniTask.Delay(millisecondsDelay: AssetLoaderSetting.EmulateDownloadDelayMilliseconds.Value, cancellationToken: ct);

                // cdn から再ダウンロードする
                var uri = CreateUri(LocationType.Remote, target.FilePath);
                await Download(uri, target.FilePath, downloadTo: AssetLoaderSetting.PersistentAssetBundleBasePath, ct);

                // 取得したら versionManifest を dl 後のものに更新する
                localManifest.Set(target.FilePath, target);

                onDownloaded?.Invoke(target);
            }
        }

        private static async UniTask Download(Uri uri, string filePath, string downloadTo, CancellationToken ct,
            int maxRetryCount = 1)
        {
            UnityEngine.Debug.Log($"Downloading : {uri} to {downloadTo}");

            for (var retryCount = 1; retryCount <= maxRetryCount; retryCount++)
            {
                var isSucceed = await DownloadImpl(uri, filePath, downloadTo, ct);
                if (isSucceed)
                    return;

                UnityEngine.Debug.LogFormat("Retry({0}):{1}", retryCount, uri);
            }

            throw new InvalidOperationException($"Download Failed :{uri}");

            static async UniTask<bool>DownloadImpl(Uri uri, string filePath, string downloadDirectory,
                CancellationToken ct)
            {
                var directory = Path.GetDirectoryName(downloadDirectory);
                if (string.IsNullOrEmpty(directory))
                    throw new InvalidOperationException();

                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var x = uri.ToString();
                using var webRequest = UnityWebRequest.Get(x);

                var path = $"{downloadDirectory}/{filePath}";
                var downloadHandler = new DownloadHandlerFile(path);
                downloadHandler.removeFileOnAbort = true;
                webRequest.downloadHandler = downloadHandler;
                webRequest.timeout = AssetLoaderSetting.DownloadTimeoutSeconds;

#if UNITY_IOS
                // アプリを削除しても再度 DL すればよいデータなので iCloud にバックアップされないようにする
                UnityEngine.iOS.Device.SetNoBackupFlag(path);
#endif

                await webRequest.SendWebRequest().WithCancellation(ct);
                var success = webRequest.result == UnityWebRequest.Result.Success;
                if (!success)
                    UnityEngine.Debug.LogWarning($"Downloading file was failed : {webRequest.error}");

                return success;
            }
        }

        private static Uri CreateUri(LocationType locationType, string fileName)
        {
            var path = AbstractFileLocatorFactory.CreateLocator(locationType, fileName).PathForWebRequest;
            return new Uri(path);
        }
    }
}