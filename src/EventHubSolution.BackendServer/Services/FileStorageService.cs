using AutoMapper;
using EventHubSolution.BackendServer.Data;
using EventHubSolution.BackendServer.Data.Entities;
using EventHubSolution.ViewModels.Contents;

namespace EventHubSolution.BackendServer.Services
{
    public class FileStorageService : IFileStorageService
    {
        private readonly ApplicationDbContext _db;
        private readonly AzureBlobService _fileService;
        private readonly IMapper _mapper;

        public FileStorageService(ApplicationDbContext db, AzureBlobService fileService, IMapper mapper)
        {
            _db = db;
            _fileService = fileService;
            _mapper = mapper;
        }



        public async Task<FileStorageVm> SaveFileToFileStorageAsync(IFormFile image, string container)
        {
            var imageFile = await _fileService.UploadAsync(image, container);

            var fileStorage = await _db.FileStorages.FindAsync(imageFile.Blob.Name);

            var fileStorageVm = new FileStorageVm();

            if (fileStorage == null)
            {
                fileStorage = new FileStorage()
                {
                    Id = imageFile.Blob.Name,
                    FileName = imageFile.Blob.Name,
                    FileSize = imageFile.Blob.Size,
                    FileType = imageFile.Blob.ContentType,
                };

                _db.FileStorages.Add(fileStorage);
                await _db.SaveChangesAsync();

                fileStorageVm = _mapper.Map<FileStorageVm>(fileStorage);
                fileStorageVm.FilePath = imageFile.Blob.Uri;
            }

            return fileStorageVm;
        }

        public async Task<IEnumerable<FileStorageVm>> GetListFileStoragesAsync()
        {
            var fileStorageVms = await Task.WhenAll(_db.FileStorages.AsParallel().Select(async file =>
            {
                var fileStorageVm = _mapper.Map<FileStorageVm>(file);

                fileStorageVm.FilePath = await _fileService.GetUriByFileNameAsync(file.FileContainer, file.FileName);

                return fileStorageVm;
            }));

            return fileStorageVms;
        }

        public async Task<FileStorageVm> GetFileByFileNameAsync(string fileName)
        {
            var fileStorage = await _db.FileStorages.FindAsync(fileName);

            if (fileStorage == null)
                return null;

            var fileStorageVm = _mapper.Map<FileStorageVm>(fileStorage);
            fileStorageVm.FilePath = await _fileService.GetUriByFileNameAsync(fileStorage.FileContainer, fileStorage.FileName);

            return fileStorageVm;
        }

        public async Task<FileStorageVm> GetFileByFileIdAsync(string id)
        {
            var fileStorage = await _db.FileStorages.FindAsync(id);

            if (fileStorage == null)
                return null;

            var fileStorageVm = _mapper.Map<FileStorageVm>(fileStorage);
            fileStorageVm.FilePath = await _fileService.GetUriByFileNameAsync(fileStorage.FileContainer, fileStorage.FileName);

            return fileStorageVm;
        }

        public async Task<FileStorageVm> DeleteFileFromFileStorageAsync(string fileName)
        {
            var fileStorage = await _db.FileStorages.FindAsync(fileName);
            var fileStorageVm = new FileStorageVm();

            if (fileStorage != null)
            {
                fileStorageVm = _mapper.Map<FileStorageVm>(fileStorage);
                fileStorageVm.FilePath = await _fileService.GetUriByFileNameAsync(fileStorage.FileContainer, fileStorage.FileName);

                await _fileService.DeleteAsync(fileStorage.FileContainer, fileStorage.FileName);

                _db.FileStorages.Remove(fileStorage);
                await _db.SaveChangesAsync();
            }

            return fileStorageVm;
        }
    }
}
