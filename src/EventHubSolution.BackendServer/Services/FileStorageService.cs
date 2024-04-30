using AutoMapper;
using EventHubSolution.BackendServer.Data;
using EventHubSolution.BackendServer.Data.Entities;
using EventHubSolution.BackendServer.Services.Interfaces;
using EventHubSolution.ViewModels.Constants;
using EventHubSolution.ViewModels.Contents;
using Microsoft.EntityFrameworkCore;

namespace EventHubSolution.BackendServer.Services
{
    public class FileStorageService : IFileStorageService
    {
        private readonly ApplicationDbContext _db;
        private readonly AzureBlobService _fileService;
        private readonly IMapper _mapper;
        private readonly ICacheService _cacheService;

        public FileStorageService(ApplicationDbContext db, AzureBlobService fileService, IMapper mapper, ICacheService cacheService)
        {
            _db = db;
            _fileService = fileService;
            _mapper = mapper;
            _cacheService = cacheService;
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
                    FileContainer = container,
                };

                var addedFileStorage = _db.FileStorages.Add(fileStorage);

                // Set expiry time
                var expiryTime = DateTimeOffset.Now.AddMinutes(45);
                _cacheService.SetData<FileStorage>($"{CacheKey.FILE}{addedFileStorage.Entity.Id}", addedFileStorage.Entity, expiryTime);

                await _db.SaveChangesAsync();

                fileStorageVm = _mapper.Map<FileStorageVm>(fileStorage);
                fileStorageVm.FilePath = imageFile.Blob.Uri;
            }

            return fileStorageVm;
        }

        public async Task<IEnumerable<FileStorageVm>> GetListFileStoragesAsync()
        {
            // Check cache data
            var fileStorages = new List<FileStorage>();
            var cacheFileStorages = _cacheService.GetData<IEnumerable<FileStorage>>(CacheKey.FILES);
            if (cacheFileStorages != null && cacheFileStorages.Count() > 0)
                fileStorages = cacheFileStorages.ToList();
            else
                fileStorages = await _db.FileStorages.ToListAsync();
            // Set expiry time
            var expiryTime = DateTimeOffset.Now.AddMinutes(45);
            _cacheService.SetData<IEnumerable<FileStorage>>(CacheKey.FILES, fileStorages, expiryTime);

            var fileStorageVms = await Task.WhenAll(fileStorages.AsParallel().Select(async file =>
            {
                var fileStorageVm = _mapper.Map<FileStorageVm>(file);

                fileStorageVm.FilePath = string.IsNullOrEmpty(fileStorageVm.FilePath) ? await _fileService.GetUriByFileNameAsync(file.FileContainer, file.FileName) : fileStorageVm.FilePath;

                return fileStorageVm;
            }));

            return fileStorageVms;
        }

        public async Task<FileStorageVm> GetFileByFileNameAsync(string fileName)
        {
            // Check cache data
            FileStorage fileStorage = null;
            var cacheFileStorage = _cacheService.GetData<FileStorage>($"{CacheKey.FILE}{fileName}");
            if (cacheFileStorage != null)
                fileStorage = cacheFileStorage;
            else
                fileStorage = await _db.FileStorages.FindAsync(fileName);
            // Set expiry time
            var expiryTime = DateTimeOffset.Now.AddMinutes(45);
            _cacheService.SetData<FileStorage>($"{CacheKey.FILE}{fileName}", fileStorage, expiryTime);

            if (fileStorage == null)
                return null;

            var fileStorageVm = _mapper.Map<FileStorageVm>(fileStorage);
            fileStorageVm.FilePath = string.IsNullOrEmpty(fileStorageVm.FilePath) ? await _fileService.GetUriByFileNameAsync(fileStorage.FileContainer, fileStorage.FileName) : fileStorageVm.FilePath;

            return fileStorageVm;
        }

        public async Task<FileStorageVm> GetFileByFileIdAsync(string id)
        {
            // Check cache data
            FileStorage fileStorage = null;
            var cacheFileStorage = _cacheService.GetData<FileStorage>($"{CacheKey.FILE}{id}");
            if (cacheFileStorage != null)
                fileStorage = cacheFileStorage;
            else
                fileStorage = await _db.FileStorages.FindAsync(id);
            // Set expiry time
            var expiryTime = DateTimeOffset.Now.AddMinutes(45);
            _cacheService.SetData<FileStorage>($"{CacheKey.FILE}{id}", fileStorage, expiryTime);

            if (fileStorage == null)
                return null;

            var fileStorageVm = _mapper.Map<FileStorageVm>(fileStorage);
            fileStorageVm.FilePath = string.IsNullOrEmpty(fileStorageVm.FilePath) ? await _fileService.GetUriByFileNameAsync(fileStorage.FileContainer, fileStorage.FileName) : fileStorageVm.FilePath;

            return fileStorageVm;
        }

        public async Task<FileStorageVm> DeleteFileFromFileStorageAsync(string fileName)
        {
            var fileStorage = await _db.FileStorages.FindAsync(fileName);
            var fileStorageVm = new FileStorageVm();

            if (fileStorage != null)
            {
                fileStorageVm = _mapper.Map<FileStorageVm>(fileStorage);
                fileStorageVm.FilePath = string.IsNullOrEmpty(fileStorageVm.FilePath) ? await _fileService.GetUriByFileNameAsync(fileStorage.FileContainer, fileStorage.FileName) : fileStorageVm.FilePath;

                await _fileService.DeleteAsync(fileStorage.FileContainer, fileStorage.FileName);

                _db.FileStorages.Remove(fileStorage);

                _cacheService.RemoveData($"{CacheKey.FILE}{fileStorage.Id}");
                _cacheService.RemoveData($"{CacheKey.FILE}{fileStorage.FileName}");

                await _db.SaveChangesAsync();
            }

            return fileStorageVm;
        }
    }
}
