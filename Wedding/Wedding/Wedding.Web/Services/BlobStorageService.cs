using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Wedding.Web.Models;

namespace Wedding.Web.Services
{
    public class BlobStorageService
    {
        private readonly BlobContainerClient _container;

        public BlobStorageService(IConfiguration configuration)
        {
            var connectionString =
                configuration["AzureStorage:ConnectionString"];

            var blobServiceClient =
                new BlobServiceClient(connectionString);

            _container =
                blobServiceClient.GetBlobContainerClient("media");
        }

        public async Task UploadAsync(IFormFile file, string folder)
        {
            var fileName =
                $"{folder}/{Guid.NewGuid()}_{file.FileName}";

            var blob = _container.GetBlobClient(fileName);

            await blob.UploadAsync(
                file.OpenReadStream(),
                overwrite: true);
        }

        public async Task<List<MediaViewModel>> ListMediaAsync()
        {
            var result = new List<MediaViewModel>();

            await foreach (var blob in _container.GetBlobsAsync())
            {
                var blobClient = _container.GetBlobClient(blob.Name);

                var sas = blobClient.GenerateSasUri(
                    BlobSasPermissions.Read,
                    DateTimeOffset.UtcNow.AddHours(6));

                result.Add(new MediaViewModel
                {
                    Url = sas.ToString(),
                    BlobName = blob.Name,
                    MediaType = blob.Name.StartsWith("videos/")
                     ? "video"
                     : "photo"
                });
            }

            return result
                .OrderByDescending(x => x.Url)
                .ToList();
        }

        public async Task<(Stream Stream, string ContentType)> DownloadAsync(string blobName)
        {
            var blob = _container.GetBlobClient(blobName);

            var response = await blob.DownloadAsync();

            return (
                response.Value.Content,
                response.Value.Details.ContentType
            );
        }


        public async Task<List<(string Name, Stream Content)>> GetAllMediaStreamsAsync()
        {
            var files = new List<(string, Stream)>();

            await foreach (var blob in _container.GetBlobsAsync())
            {
                var blobClient = _container.GetBlobClient(blob.Name);
                var stream = new MemoryStream();

                await blobClient.DownloadToAsync(stream);
                stream.Position = 0;

                files.Add((blob.Name, stream));
            }

            return files;
        }
    }
}
