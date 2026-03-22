using Microsoft.AspNetCore.Mvc;

namespace DevelopmentHub.Api.Controllers;

[ApiController]
[Route("api/icon-extractor")]
public class IconExtractorController : ControllerBase
{
    [HttpGet]
    public IActionResult GetIcon([FromQuery] string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            return NotFound();

        try
        {
            var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon is null) return NotFound();

            using var bmp = icon.ToBitmap();
            using var ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return File(ms.ToArray(), "image/png");
        }
        catch
        {
            return NotFound();
        }
    }
}
