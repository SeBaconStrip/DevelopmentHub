using DevelopmentHub.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DevelopmentHub.Api.Controllers;

[ApiController]
[Route("api/version")]
public class VersionController(IAppVersionService versionService) : ControllerBase
{
    /// <summary>
    /// Returns the running application version. Used by the About section in settings.
    /// </summary>
    [HttpGet]
    public IActionResult Get() => Ok(new { version = versionService.Version });
}
