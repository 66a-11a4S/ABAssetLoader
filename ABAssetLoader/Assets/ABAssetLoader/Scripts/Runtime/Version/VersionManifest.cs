using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace ABAssetLoader.Version
{
    // アセットのパスと更新情報の関連をまとめたもの
    public class VersionManifest
    {
        public IReadOnlyDictionary<string, BundleVersion> BundleVersions => _bundles;
        private readonly Dictionary<string, BundleVersion> _bundles;

        public VersionManifest(IEnumerable<BundleVersion> list)
        {
            _bundles = list.ToDictionary(x => x.FilePath);
        }

        public void Set(string path, BundleVersion version) => _bundles[path] = version;

        public void Remove(string path) => _bundles.Remove(path);

        public static string Serialize(VersionManifest manifest)
        {
            // NOTE: 配列 は JsonUtility ではシリアライズできない.
            return JsonConvert.SerializeObject(manifest._bundles.Values.ToArray());
        }

        public static VersionManifest Deserialize(string rawText)
        {
            try
            {
                var versions = JsonConvert.DeserializeObject<BundleVersion[]>(rawText);
                return new VersionManifest(versions);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError(ex.Message);
                return CreateEmpty();
            }
        }

        public static VersionManifest CreateEmpty() => new(Array.Empty<BundleVersion>());
    }
}