using EventHubSolution.BackendServer.Data.Entities;

namespace EventHubSolution.BackendServer.Services
{
    public interface IFileStorageService
    {
        Task<FileStorage> SaveFileToFileStorage(IFormFile image);

        Task<FileStorage> GetFileFromFileStorage(string fileName);

        Task<FileStorage> DeleteFileFromFileStorage(string fileName);
    }
}
