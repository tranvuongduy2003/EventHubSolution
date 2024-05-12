using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using EventHubSolution.ViewModels.General;
using Microsoft.Extensions.Options;

namespace EventHubSolution.BackendServer.Services
{
    public class AzureBlobService
    {
        private readonly AzureBlobStorage _azureBlobStorage;
        private readonly BlobContainerClient _filesContainer;

        public AzureBlobService(IOptions<AzureBlobStorage> azureBlobStorage)
        {
            _azureBlobStorage = azureBlobStorage.Value;

            var credential = new StorageSharedKeyCredential(_azureBlobStorage.StorageAccount, _azureBlobStorage.Key);
            var blobUri = $"https://{_azureBlobStorage.StorageAccount}.blob.core.windows.net/{_azureBlobStorage.ContainerName}";
            var blobServiceClient = new BlobServiceClient(new Uri(blobUri), credential);
            _filesContainer = blobServiceClient.GetBlobContainerClient(_azureBlobStorage.ContainerName);
        }

        public async Task<string> GetUriByFileNameAsync(string fileContainer, string fileName, string? storedPolicyName = null)
        {
            var blobClient = _filesContainer.GetBlobClient($"{fileContainer}/{fileName}");

            // Check if BlobContainerClient object has been authorized with Shared Key
            if (blobClient != null && blobClient.CanGenerateSasUri)
            {
                // Create a SAS token that's valid for one day
                BlobSasBuilder sasBuilder = new BlobSasBuilder()
                {
                    BlobContainerName = _azureBlobStorage.ContainerName,
                    BlobName = blobClient.Name,
                    Resource = "b"
                };

                if (storedPolicyName == null)
                {
                    sasBuilder.ExpiresOn = DateTimeOffset.UtcNow.AddDays(1);
                    sasBuilder.SetPermissions(BlobContainerSasPermissions.Read);
                    sasBuilder.Protocol.HasFlag(SasProtocol.HttpsAndHttp);
                }
                else
                {
                    sasBuilder.Identifier = storedPolicyName;
                }

                Uri sasURI = blobClient.GenerateSasUri(sasBuilder);

                return sasURI.ToString();
            }
            else
            {
                // Client object is not authorized via Shared Key
                return null;
            }
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

        public async Task<BlobResponseVm> UploadAsync(IFormFile blob, string fileContainer)
        {
            BlobResponseVm response = new();
            BlobClient client = _filesContainer.GetBlobClient($"{fileContainer}/{blob.FileName}");

            if (await client.ExistsAsync())
            {
                response.Status = $"File {blob.FileName} Uploaded Successfully";
                response.Error = false;
                response.Blob.Uri = client.Uri.ToString();
                response.Blob.Name = blob.FileName;
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
                response.Blob.Uri = client.Uri.ToString();
                response.Blob.Name = blob.FileName;
                response.Blob.ContentType = blob.ContentType;
                response.Blob.Size = blob.Length;
            }

            return response;
        }

        public async Task<BlobVm?> DownloadAsync(string fileContainer, string blobFilename)
        {
            BlobClient file = _filesContainer.GetBlobClient($"{fileContainer}/{blobFilename}");

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

        public async Task<BlobResponseVm> DeleteAsync(string fileContainer, string blobFilename)
        {
            BlobClient file = _filesContainer.GetBlobClient($"{fileContainer}/{blobFilename}");

            await file.DeleteIfExistsAsync();

            return new BlobResponseVm { Error = false, Status = $"File: {blobFilename} has been successfully deleted" };
        }
    }
}
