using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ABAssetLoader.AssetLoader
{
    // ファイルスキーマ(file://)つきのパスを使って WebRequest 経由で読み込む
    internal class WebRequestBundleLoadRequest
    {
        private readonly string _uri;
        private UniTaskCompletionSource<AssetBundle> _promise;

        public WebRequestBundleLoadRequest(string uri) => _uri = uri;

        public async UniTask<AssetBundle> Load(CancellationToken ct)
        {
            if (_promise != null)
                return await _promise.Task;

            // mock load delay
            if (ABAssetLoaderSetting.EmulateLoadingBundleDelayMilliseconds.HasValue)
                await UniTask.Delay(millisecondsDelay: ABAssetLoaderSetting.EmulateLoadingBundleDelayMilliseconds.Value,
                    cancellationToken: ct);

            _promise = new UniTaskCompletionSource<AssetBundle>();
            using var webRequest = UnityWebRequestAssetBundle.GetAssetBundle(_uri, 0);
            await webRequest.SendWebRequest().WithCancellation(ct);
            var result = DownloadHandlerAssetBundle.GetContent(webRequest);
            _promise.TrySetResult(result);
            return result;
        }
    }
}