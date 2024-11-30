using System;

namespace ABAssetLoader.Version
{
    [Serializable]
    public class BundleVersion
    {
        public string FilePath { get; set; }
        public string Hash { get; set; }
        public long ByteSize { get; set; }
        public long LastWriteTime { get; set; }

        public BundleVersion(string filePath, string hash, long byteSize, long lastWriteTime)
        {
            FilePath = filePath;
            Hash = hash;
            ByteSize = byteSize;
            LastWriteTime = lastWriteTime;
        }

        public bool IsNewerThan(BundleVersion otherVersion) =>
            otherVersion.LastWriteTime < LastWriteTime && otherVersion.Hash != Hash;
    }
}