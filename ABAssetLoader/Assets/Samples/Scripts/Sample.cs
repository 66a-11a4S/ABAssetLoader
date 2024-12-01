using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using ABAssetLoader.Download;
using ABAssetLoader.Version;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace ABAssetLoader.Sample
{
    public class Sample : MonoBehaviour
    {
        [SerializeField] private Button _playMovieButton;
        [SerializeField] private Button _playAudioButton;
        [SerializeField] private Button _titleUIButton;
        [SerializeField] private Button _homeUIButton;
        [SerializeField] private Button _deleteCacheButton;
        [SerializeField] private Button _downloadUpdatedButton;
        [SerializeField] private Button _unloadBundleButton;
        [SerializeField] private Button _loadTitleFromHandleButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private TMP_InputField _versionInputField;
        [SerializeField] private DownloadDialog _downloadDialog;
        [SerializeField] private GameObject _downloadProgressRoot;
        [SerializeField] private TMP_Text _downloadedSizeText;
        [SerializeField] private TMP_Text _downloadSizeText;

        [SerializeField] private VideoPlayer _videoPlayer;
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private Transform _uiPlaceholder;

        private readonly AssetLoadManager _assetLoader = new();
        private CancellationTokenSource _cts;
        private LoadedAssetHandle _handle;

        private async UniTaskVoid Awake()
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);

            _playMovieButton.OnClickAsObservable()
                .Select(_ =>
                    _assetLoader.LoadAssetAsync<VideoClip>("Assets/Samples/SampleAssets/Movie1.mov", _cts.Token).ToObservable())
                .Switch()
                .OnErrorRetry((OperationCanceledException _) => { })
                .Subscribe(x => PlayMovie(x.Asset as VideoClip))
                .AddTo(this);

            _playAudioButton.OnClickAsObservable()
                .Select(_ =>
                    _assetLoader.LoadAssetAsync<AudioClip>("Assets/Samples/SampleAssets/ExternalAssets/Audio1.wav", _cts.Token).ToObservable())
                .Switch()
                .OnErrorRetry((OperationCanceledException _) => { })
                .OnErrorRetry((KeyNotFoundException _) => Debug.LogError("Audio データが存在しません。先に v100 以上のバージョンを Download してください."))
                .Subscribe(x => PlayAudio(x.Asset as AudioClip))
                .AddTo(this);

            _titleUIButton.OnClickAsObservable()
                .Select(_ =>
                    _assetLoader.LoadAssetAsync<GameObject>("Assets/Samples/SampleAssets/Title.prefab", _cts.Token).ToObservable())
                .Switch()
                .OnErrorRetry((OperationCanceledException _) => { })
                .Subscribe(x =>
                {
                    SwitchUI(x.Asset as GameObject);
                    _handle = x;
                })
                .AddTo(this);

            _homeUIButton.OnClickAsObservable()
                .Select(_ =>
                    _assetLoader.LoadAssetAsync<GameObject>("Assets/Samples/SampleAssets/Home.prefab", _cts.Token).ToObservable())
                .Switch()
                .OnErrorRetry((OperationCanceledException _) => { })
                .OnErrorRetry((KeyNotFoundException _) => Debug.LogError("Home データが存在しません。先に v100 以上のバージョンを Download してください."))
                .Subscribe(x => SwitchUI(x.Asset as GameObject))
                .AddTo(this);

            _downloadUpdatedButton.OnClickAsObservable()
                .Select(_ => DownloadPackage(_cts.Token).ToObservable())
                .Switch()
                .OnErrorRetry((OperationCanceledException _) => { })
                .Subscribe()
                .AddTo(this);

            _unloadBundleButton.OnClickAsObservable()
                .Subscribe(_ =>
                {
                    _assetLoader.UnloadAsset(_handle);
                    _handle = null;
                })
                .AddTo(this);

            _loadTitleFromHandleButton.OnClickAsObservable()
                .Subscribe(_ => SwitchUI(_handle.Asset as GameObject))
                .AddTo(this);

            _deleteCacheButton.OnClickAsObservable()
                .Do(_ => _assetLoader.DeleteBundleCache(unloadAllObjects: true))
                .Select(_ => _assetLoader.Setup(_cts.Token).ToObservable())
                .Switch()
                .OnErrorRetry((OperationCanceledException _) => { })
                .Subscribe()
                .AddTo(this);

            _cancelButton.OnClickAsObservable()
                .Subscribe(_ => Cancel())
                .AddTo(this);

            _versionInputField.onValueChanged.AsObservable()
                .StartWith(_versionInputField.text)
                .Subscribe(text => ABAssetLoaderSetting.RemotePackageName = text)
                .AddTo(this);

            await _assetLoader.Setup(destroyCancellationToken);
        }

        private async UniTask DownloadPackage(CancellationToken ct)
        {
            var size = await AssetBundleDownloader.CalculateDownloadSize(ct);
            var accepted = await _downloadDialog.ShowAndAcceptDialog(size, ct);
            if (!accepted)
                return;

            _downloadSizeText.text = size.ToString();
            _downloadedSizeText.text = "0";
            _downloadProgressRoot.SetActive(true);
            long downloadedSize = 0;
            try
            {
                await _assetLoader.DownloadPackage(unloadAllObjects: true, OnDownloaded, ct);
            }
            finally
            {
                _downloadProgressRoot.SetActive(false);
            }

            return;

            void OnDownloaded(BundleVersion version)
            {
                downloadedSize += version.ByteSize;
                _downloadedSizeText.text = downloadedSize.ToString();
            }
        }

        private void PlayMovie(VideoClip video)
        {
            _videoPlayer.Stop();
            _videoPlayer.clip = video;
            _videoPlayer.Play();
        }

        private void PlayAudio(AudioClip audio)
        {
            _audioSource.Stop();
            _audioSource.clip = audio;
            _audioSource.Play();
        }

        private void SwitchUI(GameObject prefab)
        {
            foreach (Transform child in _uiPlaceholder)
            {
                Destroy(child.gameObject);
            }

            var instance = Instantiate(prefab, _uiPlaceholder);
            instance.SetActive(true);
        }

        private void Cancel()
        {
            _cts.Cancel();
            _cts.Dispose();
            UnityEngine.Debug.Log("Canceled");
            _cts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
        }
    }
}