using EventHubSolution.ViewModels.Systems;

namespace EventHubSolution.BackendServer.Services
{
    public interface IAzureBlobService
    {
        Task<List<BlobVm>> ListAsync();

        Task<BlobResponseVm> UploadAsync(IFormFile blob);

        Task<BlobVm?> DownloadAsync(string blobFilename);

        Task<BlobResponseVm> DeleteAsync(string blobFilename);
    }
}
