using EFCore.BulkExtensions;

namespace RWPM.Common
{
    public static class FileHelper
    {
        public static void DeleteIfExists(string filePath)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                }
                catch
                {

                }
            }
        }
    }
}
