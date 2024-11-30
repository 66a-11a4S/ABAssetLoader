using System.Collections.Generic;
using System.IO;
using ABAssetLoader.AssetLoader;
using ABAssetLoader.Version;
using UnityEditor;
using UnityEngine;

namespace ABAssetLoader.Editor
{
    public static class PackageAttachmentBuilder
    {
        public static void ExportAssetBundleContentsTable(string buildOutputPath, HashSet<string> contentsNameTable,
            AssetBundleManifest rootManifest)
        {
            var rawTable = new List<(string, string)>();
            var allBundleNames = rootManifest.GetAllAssetBundles();
            foreach (var bundleName in allBundleNames)
            {
                if (!contentsNameTable.Contains(bundleName))
                    continue;

                var bundle = AssetBundle.LoadFromFile($"{buildOutputPath}/{bundleName}");
                var contentNames = bundle.GetAllAssetNames();
                foreach (var contentName in contentNames)
                {
                    rawTable.Add((contentName, bundleName));
                }
            }

            // root bundle と manifest は手動で追加する
            rawTable.Add((AssetLoaderSetting.RootBundleName, AssetLoaderSetting.RootBundleName));
            rawTable.Add(($"{AssetLoaderSetting.RootBundleName}.manifest", AssetLoaderSetting.RootBundleName));

            var filePath = $"{buildOutputPath}/{AssetLoaderSetting.ContentsTableName}";
            if (File.Exists(filePath))
                File.Delete(filePath);

            var table = new AssetBundleContentsTable(rawTable);
            AssetDatabase.StartAssetEditing();
            try
            {
                using var fs = File.Create(filePath);
                using var sw = new StreamWriter(fs);
                sw.Write(AssetBundleContentsTable.Serialize(table));
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        public static void ExportVersionManifest(string buildOutputPath, HashSet<string> contentsNameTable,
            AssetBundleManifest rootManifest)
        {
            var filePath = $"{buildOutputPath}/{AssetLoaderSetting.VersionManifestFileName}";
            var versionManifest = AssetVersionManifestMaker.Create(buildOutputPath, contentsNameTable, rootManifest);

            if (File.Exists(filePath))
                File.Delete(filePath);

            AssetDatabase.StartAssetEditing();
            try
            {
                using var fs = File.Create(filePath);
                using var sw = new StreamWriter(fs);
                sw.Write(VersionManifest.Serialize(versionManifest));
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }
    }
}