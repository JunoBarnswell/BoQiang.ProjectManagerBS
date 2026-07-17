using AsterERP.Api.Application.System.InfrastructureSettings;
using AsterERP.Contracts.System.InfrastructureSettings;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/system/infrastructure-settings")]
public sealed class InfrastructureSettingsController(IInfrastructureSettingsService infrastructureSettingsService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.SystemAbpSettingQuery)]
    public async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await infrastructureSettingsService.GetAsync(cancellationToken));
    }

    [HttpPut]
    [Permission(PermissionCodes.SystemAbpSettingEdit)]
    public async Task<IActionResult> UpdateAsync(
        [FromBody] InfrastructureSettingsUpdateRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await infrastructureSettingsService.UpdateAsync(request, cancellationToken));
    }

    [HttpPost("email/test")]
    [Permission(PermissionCodes.SystemAbpSettingTest)]
    public async Task<IActionResult> TestEmailAsync(
        [FromBody] InfrastructureEmailTestRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await infrastructureSettingsService.TestEmailAsync(request, cancellationToken));
    }

    [HttpPost("sms/test")]
    [Permission(PermissionCodes.SystemAbpSettingTest)]
    public async Task<IActionResult> TestSmsAsync(
        [FromBody] InfrastructureSmsTestRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await infrastructureSettingsService.TestSmsAsync(request, cancellationToken));
    }

    [HttpPost("object-storage/test")]
    [Permission(PermissionCodes.SystemAbpSettingTest)]
    public async Task<IActionResult> TestObjectStorageAsync(
        [FromBody] InfrastructureObjectStorageTestRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await infrastructureSettingsService.TestObjectStorageAsync(request, cancellationToken));
    }

    [HttpGet("message-logs")]
    [Permission(PermissionCodes.SystemAbpSettingQuery)]
    public async Task<IActionResult> GetMessageLogsAsync(
        [FromQuery] MessageSendLogQuery query,
        CancellationToken cancellationToken)
    {
        return ApiOk(await infrastructureSettingsService.GetMessageLogsAsync(query, cancellationToken));
    }
}
