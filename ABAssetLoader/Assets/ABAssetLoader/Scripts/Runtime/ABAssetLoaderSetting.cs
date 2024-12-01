using UnityEngine;

namespace ABAssetLoader
{
    public static class ABAssetLoaderSetting
    {
        public static string RemotePackageName = string.Empty;
        public static int? EmulateLoadingBundleDelayMilliseconds = null;
        public static int? EmulateLoadingAssetDelayMilliseconds = null;
        public static int? EmulateDownloadDelayMilliseconds = null;
        public static int DownloadTimeoutSeconds = 300;

        public const string RootBundleName = "AssetBundles";
        public const string ContentsTableName = "ContentsTable.json";
        public const string VersionManifestFileName = "VersionManifest.json";
        public const string BundleBasePath = "AssetBundles";
        public const string CacheBasePath = "PersistentCache";

        public static string PersistentDataPath => $"{Application.persistentDataPath}/{CacheBasePath}";
        public static string PersistentAssetBundleBasePath => $"{PersistentDataPath}/{BundleBasePath}";
        public static string StreamingAssetBundleBasePath => $"{Application.streamingAssetsPath}/{BundleBasePath}";
    }
}