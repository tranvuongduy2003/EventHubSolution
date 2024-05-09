using EventHubSolution.ViewModels.Contents;

namespace EventHubSolution.BackendServer.Services
{
    public interface IFileStorageService
    {
        Task<FileStorageVm> SaveFileToFileStorageAsync(IFormFile image, string container);

        Task<IEnumerable<FileStorageVm>> GetListFileStoragesAsync();

        Task<FileStorageVm> GetFileByFileNameAsync(string fileName);

        Task<FileStorageVm> GetFileByFileIdAsync(string id);

        Task<FileStorageVm> DeleteFileByFileNameAsync(string fileName);

        Task<FileStorageVm> DeleteFileByIdAsync(string id);
    }
}
