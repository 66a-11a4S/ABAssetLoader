using UnityEngine;

namespace ABAssetLoader.Sample
{
    public class LoadingMarker : MonoBehaviour
    {
        [SerializeField] private float _animationSpeed = 0.25f;
        private void Update()
        {
            transform.Rotate(axis: Vector3.up, angle: _animationSpeed);
        }
    }
}