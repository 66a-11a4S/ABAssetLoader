namespace ABAssetLoader
{
    public class LoadedAssetHandle
    {
        public UnityEngine.Object Asset { get; private set; }

        public string Key { get; }

        public LoadedAssetHandle(string key, UnityEngine.Object asset)
        {
            Key = key;
            Asset = asset;
        }
    }
}