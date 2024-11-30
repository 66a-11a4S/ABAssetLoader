using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace ABAssetLoader.AssetLoader
{
    // assetPath -> assetBundlePath への変換テーブル
    public class AssetBundleContentsTable
    {
        private readonly Dictionary<string, string> _relations;

        public AssetBundleContentsTable(IEnumerable<(string assetName, string bundleName)> relations)
        {
            _relations = relations.ToDictionary(x => x.assetName, x => x.bundleName);
        }

        public string GetBundleName(string assetName)
        {
            // NOTE: AssetBundle.GetAllAssetNames() が返すアセット名が小文字なので relations のキーは全て小文字
            return _relations[assetName.ToLower()];
        }

        public static string Serialize(AssetBundleContentsTable table)
        {
            // NOTE: 配列 は JsonUtility ではシリアライズできない.
            return JsonConvert.SerializeObject(table._relations.Select(kvp => (kvp.Key, kvp.Value)).ToArray());
        }

        public static AssetBundleContentsTable Deserialize(string rawText)
        {
            try
            {
                var relations = JsonConvert.DeserializeObject<(string, string)[]>(rawText);
                return new AssetBundleContentsTable(relations);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError(ex.Message);
                return new AssetBundleContentsTable(Array.Empty<(string, string)>());
            }
        }
    }
}