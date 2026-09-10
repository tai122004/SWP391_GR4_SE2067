

using RWPM.Common.Exceptions;
using System.IO;
using System.IO.Pipes;

namespace RWPM.Infrastructure.Storage
{
    public class LocalFileSystemStorage : IFileStorageService
    {
        private readonly string _baseRelativePath = ApplicationDefinition.STORAGE;
        private string _basePath => Path.Combine(AppContext.BaseDirectory, _baseRelativePath);

        public LocalFileSystemStorage()
        {
            if (!Directory.Exists(_basePath))
            {
                Directory.CreateDirectory(_basePath);
            }
        }

        public async Task SaveFileAsync(string relativePath, Stream content)
        {
            using var fileStream = File.Create(relativePath);
            await content.CopyToAsync(fileStream);
        }

        public Task<Stream> ReadFileAsync(string relativePath)
        {
            throw new NotImplementedException();

            //await Task.Delay(100);
            //return File.Exists(relativePath) ? File.OpenRead(relativePath) : throw new FileNotFoundException();
        }

        public Task DeleteFileAsync(string relativePath, bool onlyDeleteIfAble = true)
        {
            var fullPath = GetFullFilePath(relativePath);
            if (File.Exists(fullPath))
            {
                if (onlyDeleteIfAble)
                {
                    try
                    {
                        File.Delete(fullPath);
                    }
                    catch { }
                }
                else
                {
                    File.Delete(fullPath);
                }
            }
            
            return Task.CompletedTask;
        }

        public bool Exists(string relativePath)
        {
            return File.Exists(Path.Combine(AppContext.BaseDirectory, relativePath));
        }

        /// <summary>
        /// Upload file from user
        /// </summary>
        /// <param name="uploadFile"></param>
        /// <param name="subDirectory"></param>
        /// <param name="limitFileSize"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ModelValidationException"></exception>
        public async Task<string> SaveRandomFileAsync(IFormFile uploadFile, string subDirectory, int limitFileSize = 20)
        {
            if(limitFileSize <= 0)
                limitFileSize = 20;

            //I. Validate the input file
            if (uploadFile == null || uploadFile.Length == 0)
                throw new ModelValidationException("File is empty or null.");

            if (uploadFile.Length > limitFileSize * 1024 * 1024) // upload file > 20MB
                throw new ModelValidationException("Storage_MaximumUploadFileSize", $"{limitFileSize}MB");

            string fileName = GetSaveableFileName(Path.GetExtension(uploadFile.FileName), subDirectory, out var saveDirectory, out var saveDirectoryRelativePath);

            var filePath = Path.Combine(saveDirectory, fileName);

            // Save the file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await uploadFile.CopyToAsync(stream);
            }

            return Path.Combine(saveDirectoryRelativePath, fileName);
        }

        public string GetFullFilePath(string relativeFilePath)
        {
            return Path.Combine(AppContext.BaseDirectory, relativeFilePath);
        }

        public async Task<string> SaveRandomFileAsync(FileStream fileStream, string subDirectory)
        {
            if (fileStream.CanSeek)
                fileStream.Position = 0;

            string fileName = GetSaveableFileName(Path.GetExtension(fileStream.Name), subDirectory, out var saveDirectory, out var saveDirectoryRelativePath);
            var filePath = Path.Combine(saveDirectory, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await fileStream.CopyToAsync(stream);
            }

            return Path.Combine(saveDirectoryRelativePath, fileName);
        }

        private string GetSaveableFileName(string extension, string? subDirectory, out string saveDirectory, out string saveDirectoryRelativePath)
        {
            saveDirectory = _basePath;
            saveDirectoryRelativePath = _baseRelativePath;
            if (!string.IsNullOrEmpty(subDirectory))
            {
                saveDirectory = Path.Combine(saveDirectory, subDirectory);
                saveDirectoryRelativePath = Path.Combine(saveDirectoryRelativePath, subDirectory);
            }

            if (!Directory.Exists(saveDirectory))
                Directory.CreateDirectory(saveDirectory);

            string fileName;
            do
            {
                fileName = DateTime.Now.ToString("yyyyMMddHHmmssfff") + extension;
            }
            while (File.Exists(Path.Combine(saveDirectory, fileName)));

            return fileName;
        }

        public string GetSaveablePath(string extension, string subDirectory)
        {
            var fileName = GetSaveableFileName(extension, subDirectory, out var saveDirectory, out _);
            return Path.Combine(saveDirectory, fileName); ;
        }
    }
}
