using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using ABAssetLoader.Locator;
using UnityEngine;
using UnityEngine.Networking;

namespace ABAssetLoader.Version
{
    internal static class VersionManifestAccessor
    {
        public static async UniTask<VersionManifest> Get(LocationType locationType, CancellationToken ct)
        {
            var path = AbstractFileLocatorFactory.CreateLocator(locationType,
                fileName: AssetLoaderSetting.VersionManifestFileName).PathForWebRequest;
            return await LoadVersionManifest(path, ct, Debug.Log, Debug.LogWarning);
        }

        public static async UniTask<VersionManifest> GetOrDefault(LocationType locationType, CancellationToken ct)
        {
            var path = AbstractFileLocatorFactory.CreateLocator(locationType,
                fileName: AssetLoaderSetting.VersionManifestFileName).PathForWebRequest;
            try
            {
                return await LoadVersionManifest(path, ct, _ => { }, _ => { });
            }
            catch (UnityWebRequestException ex)
            {
                if (ex.ResponseCode == 404)
                    return null;

                throw;
            }
        }

        private static async UniTask<VersionManifest> LoadVersionManifest(string path, CancellationToken ct,
            Action<string> log, Action<string> logWarning)
        {
            using var request = UnityWebRequest.Get(path);
            try
            {
                await request.SendWebRequest().WithCancellation(ct);
            }
            catch (UnityWebRequestException ex)
            {
                logWarning($"invalid data for {path}: " + ex);
                throw;
            }

            if (request.error == null)
            {
                var rawTextData = System.Text.Encoding.UTF8.GetString(request.downloadHandler.data);
                var result = VersionManifest.Deserialize(rawTextData);
                log("loaded data:" + path);
                return result;
            }

            logWarning($"invalid data for {path} : " + request.error);
            return null;
        }

        public static void SavePersistentManifest(VersionManifest manifest)
        {
            var directory = AssetLoaderSetting.PersistentAssetBundleBasePath;
            var path = $"{directory}/{AssetLoaderSetting.VersionManifestFileName}";
            var text = VersionManifest.Serialize(manifest);
#if UNITY_IOS
            // アプリを削除しても再度 DL すればよいデータなので iCloud にバックアップされないようにする
            UnityEngine.iOS.Device.SetNoBackupFlag(path);
#endif
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, text);
        }
    }
}