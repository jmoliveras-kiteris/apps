using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.IO.Compression;
using Wedding.Web.Models;
using Wedding.Web.Services;

namespace Wedding.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        private readonly BlobStorageService _blobService;

        public HomeController(BlobStorageService blobService)
        {
            _blobService = blobService;
        }

        public async Task<IActionResult> Index()
        {
            var media = await _blobService.ListMediaAsync();
            return View(media);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [Route("Denied")]
        public IActionResult Denied()
        {
            return View();
        }

        public async Task<IActionResult> Download(string blobName)
        {
            var (stream, contentType) =
                await _blobService.DownloadAsync(blobName);

            var fileName = blobName.Split('/').Last();

            return File(stream, contentType, fileName);
        }

        public async Task<IActionResult> DownloadAll()
        {
            var files = await _blobService.GetAllMediaStreamsAsync();
            var zipStream = new MemoryStream();

            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                foreach (var file in files)
                {
                    var entry = archive.CreateEntry(file.Name.Split('/').Last());

                    using var entryStream = entry.Open();

                    await file.Content.CopyToAsync(entryStream);
                }
            }

            zipStream.Position = 0;

            return File(
                zipStream,
                "application/zip",
                "NuestraBoda_Recuerdos.zip");
        }
    }
}
