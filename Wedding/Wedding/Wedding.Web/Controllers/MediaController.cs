using Microsoft.AspNetCore.Mvc;
using Wedding.Web.Services;

namespace Wedding.Web.Controllers
{
    public class MediaController : Controller
    {
        private readonly BlobStorageService _blobService;

        public MediaController(BlobStorageService blobService)
        {
            _blobService = blobService;
        }

        [HttpGet]
        public IActionResult Upload()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Upload(List<IFormFile> files)
        {
            foreach (var file in files)
            {
                if (file.Length == 0) continue;

                var folder = file.ContentType.StartsWith("video")
                    ? "videos"
                    : "photos";

                await _blobService.UploadAsync(file, folder);
            }

            TempData["Success"] = "¡Recuerdos subidos con éxito ❤️";
            return RedirectToAction("Index", "Home");
        }
    }

}
