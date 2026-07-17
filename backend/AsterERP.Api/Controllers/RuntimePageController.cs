using AsterERP.Api.Application.Runtime;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/runtime/pages")]
public sealed class RuntimePageController(IRuntimePageSchemaService runtimePageSchemaService) : BaseApiController
{
    [HttpGet("{pageCode}")]
    public async Task<IActionResult> GetAsync(
        string pageCode,
        [FromQuery] string? previewPageId,
        CancellationToken cancellationToken)
    {
        return ApiOk(await runtimePageSchemaService.GetPublishedPageAsync(pageCode, previewPageId, cancellationToken));
    }
}
