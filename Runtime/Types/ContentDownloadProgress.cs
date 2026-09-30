namespace UContent
{
    public readonly struct ContentDownloadProgress
    {
        public long DownloadedBytes { get; }
        public long TotalBytes { get; }
        public bool IsDone { get; }

        public float Progress => TotalBytes <= 0 ? (IsDone ? 1f : 0f) : (float)DownloadedBytes / TotalBytes;
        public double DownloadedMegabytes => DownloadedBytes / 1024d / 1024d;
        public double TotalMegabytes => TotalBytes / 1024d / 1024d;

        public ContentDownloadProgress(long downloadedBytes, long totalBytes, bool isDone)
        {
            DownloadedBytes = downloadedBytes;
            TotalBytes = totalBytes;
            IsDone = isDone;
        }
    }
}