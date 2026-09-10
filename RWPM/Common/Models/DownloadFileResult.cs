namespace RWPM.Common.Models
{
    public class DownloadFileResult : IDisposable
    {
        public readonly MemoryStream MemoryStream;
        public readonly string ContentType;
        public readonly string DownloadFileName;

        public DownloadFileResult(MemoryStream memoryStream, string contentType, string downloadFileName)
        {
            MemoryStream = memoryStream;
            ContentType = contentType;
            DownloadFileName = downloadFileName;
        }

        public void Dispose()
        {
            MemoryStream?.Dispose();
        }
    }
}
