using EventHubSolution.BackendServer.Data;
using EventHubSolution.BackendServer.Data.Entities;

namespace EventHubSolution.BackendServer.Services
{
    public class FileStorageService : IFileStorageService
    {
        private readonly ApplicationDbContext _db;
        private readonly AzureBlobService _fileService;

        public FileStorageService(ApplicationDbContext db, AzureBlobService fileService)
        {
            _db = db;
            _fileService = fileService;
        }
        public async Task<FileStorage> SaveFileToFileStorage(IFormFile image)
        {
            var imageFile = await _fileService.UploadAsync(image);

            var fileStorage = await _db.FileStorages.FindAsync(imageFile.Blob.Name);

            if (fileStorage == null)
            {
                fileStorage = new FileStorage()
                {
                    Id = imageFile.Blob.Name,
                    FileName = imageFile.Blob.Name,
                    FilePath = imageFile.Blob.Uri,
                    FileSize = imageFile.Blob.Size,
                    FileType = imageFile.Blob.ContentType,
                };

                _db.FileStorages.Add(fileStorage);
                await _db.SaveChangesAsync();
            }

            return fileStorage;
        }

        public async Task<FileStorage> GetFileFromFileStorage(string fileName)
        {
            var fileStorage = await _db.FileStorages.FindAsync(fileName);

            return fileStorage;
        }
        public async Task<FileStorage> DeleteFileFromFileStorage(string fileName)
        {
            var fileStorage = await _db.FileStorages.FindAsync(fileName);

            if (fileStorage != null)
            {
                await _fileService.DeleteAsync(fileName);

                _db.FileStorages.Remove(fileStorage);
                var result = await _db.SaveChangesAsync();
            }

            return fileStorage;
        }
    }
}
