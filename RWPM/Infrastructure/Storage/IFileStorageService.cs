using Microsoft.AspNetCore.Http;

namespace RWPM.Infrastructure.Storage
{
    public interface IFileStorageService
    {
        Task<string> SaveRandomFileAsync(IFormFile uploadFile, string subDirectory, int limitFileSize = 20);
        Task<string> SaveRandomFileAsync(FileStream fileStream, string subDirectory);
        Task<Stream> ReadFileAsync(string relativePath);
        Task DeleteFileAsync(string relativePath, bool onlyDeleteIfAble = true);
        bool Exists(string relativePath);
        string GetFullFilePath(string relativeFilePath);
        string GetSaveablePath(string extension, string subDirectory);
    }
}
