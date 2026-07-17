using AsterERP.Api.Application.Ai;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/ai/settings")]
public sealed class AiSettingsController(AiSettingsService service) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.AiSettingsView)]
    public async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await service.GetAsync(cancellationToken));
    }

    [HttpPut]
    [Permission(PermissionCodes.AiSettingsEdit)]
    public async Task<IActionResult> UpdateAsync([FromBody] AiSettingsUpdateRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await service.UpdateAsync(request, cancellationToken));
    }

    [HttpGet("export")]
    [Permission(PermissionCodes.AiSettingsView)]
    public async Task<IActionResult> ExportAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await service.ExportAsync(cancellationToken));
    }

    [HttpPost("import")]
    [Permission(PermissionCodes.AiSettingsEdit)]
    public async Task<IActionResult> ImportAsync([FromBody] AiSettingsImportRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await service.ImportAsync(request, cancellationToken));
    }

    [HttpPost("cleanup")]
    [Permission(PermissionCodes.AiSettingsEdit)]
    public async Task<IActionResult> CleanupAsync([FromBody] AiCleanupRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await service.CleanupAsync(request, cancellationToken));
    }
}
