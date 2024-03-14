using Azure.Storage;
using Azure.Storage.Blobs;
using EventHubSolution.ViewModels.Systems;

namespace EventHubSolution.BackendServer.Services
{
    public class AzureBlobService
    {
        private readonly IConfiguration _configuration;
        private readonly string _storageAccount;
        private readonly string _key;
        private readonly string _containerName;
        private readonly BlobContainerClient _filesContainer;

        public AzureBlobService(IConfiguration configuration)
        {
            _configuration = configuration;
            _storageAccount = _configuration.GetValue<string>("AzureBlobStorage:StorageAccount");
            _key = _configuration.GetValue<string>("AzureBlobStorage:Key");
            _containerName = _configuration.GetValue<string>("AzureBlobStorage:ContainerName");

            var credential = new StorageSharedKeyCredential(_storageAccount, _key);
            var blobUri = $"https://{_storageAccount}.blob.core.windows.net";
            var blobServiceClient = new BlobServiceClient(new Uri(blobUri), credential);
            _filesContainer = blobServiceClient.GetBlobContainerClient(_containerName);
        }

        public async Task<List<BlobVm>> ListAsync()
        {
            List<BlobVm> files = new List<BlobVm>();

            await foreach (var file in _filesContainer.GetBlobsAsync())
            {
                string uri = _filesContainer.Uri.ToString();
                var name = file.Name;
                var fullUri = $"{uri}/{name}";

                files.Add(new BlobVm
                {
                    Uri = fullUri,
                    Name = name,
                    ContentType = file.Properties.ContentType
                });
            }

            return files;
        }

        public async Task<BlobResponseVm> UploadAsync(IFormFile blob)
        {
            BlobResponseVm response = new();
            BlobClient client = _filesContainer.GetBlobClient(blob.FileName);

            if (await client.ExistsAsync())
            {
                response.Status = $"File {blob.FileName} Uploaded Successfully";
                response.Error = false;
                response.Blob.Uri = client.Uri.AbsoluteUri;
                response.Blob.Name = client.Name;
                response.Blob.ContentType = blob.ContentType;
                response.Blob.Size = blob.Length;
            }
            else
            {
                await using (Stream? data = blob.OpenReadStream())
                {
                    await client.UploadAsync(data);
                }

                response.Status = $"File {blob.FileName} Uploaded Successfully";
                response.Error = false;
                response.Blob.Uri = client.Uri.AbsoluteUri;
                response.Blob.Name = client.Name;
                response.Blob.ContentType = blob.ContentType;
                response.Blob.Size = blob.Length;
            }

            return response;
        }

        public async Task<BlobVm?> DownloadAsync(string blobFilename)
        {
            BlobClient file = _filesContainer.GetBlobClient(blobFilename);

            if (await file.ExistsAsync())
            {
                var data = await file.OpenReadAsync();
                Stream blobContent = data;

                var content = await file.DownloadContentAsync();

                string name = blobFilename;
                string contentType = content.Value.Details.ContentType;

                return new BlobVm { Content = blobContent, Name = name, ContentType = contentType };
            }

            return null;
        }

        public async Task<BlobResponseVm> DeleteAsync(string blobFilename)
        {
            BlobClient file = _filesContainer.GetBlobClient(blobFilename);

            await file.DeleteAsync();

            return new BlobResponseVm { Error = false, Status = $"File: {blobFilename} has been successfully deleted" };
        }
    }
}
