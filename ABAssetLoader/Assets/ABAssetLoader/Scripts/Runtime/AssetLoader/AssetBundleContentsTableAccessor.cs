using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using ABAssetLoader.Locator;
using UnityEngine;
using UnityEngine.Networking;

namespace ABAssetLoader.AssetLoader
{
    internal static class AssetBundleContentsTableAccessor
    {
        public static async UniTask<AssetBundleContentsTable> Get(LocationType locationType, CancellationToken ct)
        {
            var path = AbstractFileLocatorFactory.CreateLocator(locationType,
                fileName: AssetLoaderSetting.ContentsTableName).PathForWebRequest;
            return await Load(path, ct, Debug.Log, Debug.LogWarning);
        }

        public static async UniTask<AssetBundleContentsTable> GetOrDefault(LocationType locationType,
            CancellationToken ct)
        {
            var path = AbstractFileLocatorFactory.CreateLocator(locationType,
                fileName: AssetLoaderSetting.ContentsTableName).PathForWebRequest;
            try
            {
                return await Load(path, ct, _ => {}, _ => {});
            }
            catch (UnityWebRequestException ex)
            {
                if (ex.ResponseCode == 404)
                    return null;

                throw;
            }
        }

        private static async UniTask<AssetBundleContentsTable> Load(string path, CancellationToken ct,
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
                var result = AssetBundleContentsTable.Deserialize(rawTextData);
                log("loaded data :" + path);
                return result;
            }

            logWarning($"invalid data for {path} : " + request.error);
            return null;
        }
    }
}