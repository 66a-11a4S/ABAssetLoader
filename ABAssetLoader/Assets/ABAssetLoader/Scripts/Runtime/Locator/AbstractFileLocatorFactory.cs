using System;
using UnityEngine;

namespace ABAssetLoader.Locator
{
    internal static class AbstractFileLocatorFactory
    {
        public static AbstractFileLocator CreateLocator(LocationType locationType, string fileName)
        {
            return locationType switch
            {
                LocationType.Persistent => new PersistentFileLocator(fileName),
                LocationType.Streaming => new StreamingFileLocator(fileName),
                LocationType.Remote => new RemoteFileLocator(fileName),
                LocationType.None => throw new InvalidOperationException(),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public abstract class AbstractFileLocator
        {
            public abstract string PathForWebRequest { get; }
            protected abstract string BasePath { get; }

            protected AbstractFileLocator(string fileName) => FileName = fileName;
            protected virtual string Path => $"{BasePath}/{ABAssetLoaderSetting.BundleBasePath}/{FileName}";
            protected string FileName { get; }
        }

        private class StreamingFileLocator : AbstractFileLocator
        {
            protected override string BasePath => Application.streamingAssetsPath;
            public override string PathForWebRequest
            {
                get
                {
                    // Android の StreamingAssetsPath はスキーマが元々ついているので file:// をつけない
                    if (Application.platform == RuntimePlatform.Android)
                        return Path.Replace("\\", "/");

                    return "file://" + Path.Replace("\\", "/");
                }
            }

            public StreamingFileLocator(string fileName) : base(fileName)
            {
            }
        }

        private class PersistentFileLocator : AbstractFileLocator
        {
            protected override string BasePath => ABAssetLoaderSetting.PersistentDataPath;
            public override string PathForWebRequest => "file://" + Path.Replace("\\", "/");

            public PersistentFileLocator(string fileName) : base(fileName)
            {
            }
        }

        private class RemoteFileLocator : AbstractFileLocator
        {
            // TODO: アセットサーバのホスティング
            protected override string BasePath =>
                $"{Application.streamingAssetsPath}/MockCdnHost/{ABAssetLoaderSetting.RemotePackageName}";
            protected override string Path => $"{BasePath}/{FileName}";

            public override string PathForWebRequest
            {
                get
                {
                    // Android の StreamingAssetsPath はスキーマが元々ついているので file:// をつけない
                    if (Application.platform == RuntimePlatform.Android)
                        return Path.Replace("\\", "/");

                    return "file://" + Path.Replace("\\", "/");
                }
            }

            public RemoteFileLocator(string fileName) : base(fileName)
            {
            }
        }
    }
}