using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace ABAssetLoader.Sample
{
    public class DownloadDialog : MonoBehaviour
    {
        [SerializeField] private Button _noButton;
        [SerializeField] private Button _yesButton;
        [SerializeField] private TMP_Text _sizeText;

        public async UniTask<bool> ShowAndAcceptDialog(long size, CancellationToken ct)
        {
            _sizeText.text = size.ToString();

            gameObject.SetActive(true);
            var onClicked = _noButton.OnClickAsObservable().Select(_ => false)
                .Merge(_yesButton.OnClickAsObservable().Select(_ => true))
                .Take(1);

            bool result;
            try
            {
                result = await onClicked.ToUniTask(cancellationToken: ct);
            }
            finally
            {
                gameObject.SetActive(false);
            }

            return result;
        }
    }
}