using System.Collections.Generic;
using System.IO;
using System.Linq;
using ABAssetLoader.Version;
using UnityEngine;

namespace ABAssetLoader.Editor
{
    public static class AssetVersionManifestMaker
    {
        public static VersionManifest Create(
            string assetBundlesRootDirectory,
            HashSet<string> contentsNameTable,
            AssetBundleManifest rootBundleManifest)
        {
            var assetBundles = rootBundleManifest.GetAllAssetBundles();
            var versionList = assetBundles
                .Where(contentsNameTable.Contains)
                .Select(bundleName =>
                {
                    var bundleFilePath = $"{assetBundlesRootDirectory}/{bundleName}";
                    return CreateVersion(bundleName, bundleFilePath, rootBundleManifest);
                })
                .ToArray();
            return new VersionManifest(versionList);
        }

        private static BundleVersion CreateVersion(string bundleName, string bundleFullPath,
            AssetBundleManifest rootBundleManifest)
        {
            var assetBundleHash = rootBundleManifest.GetAssetBundleHash(bundleName);
            var fileInfo = new FileInfo(bundleFullPath);
            return new BundleVersion(bundleName,
                hash: assetBundleHash.ToString(),
                byteSize: fileInfo.Length,
                fileInfo.LastWriteTime.Ticks);
        }
    }
}