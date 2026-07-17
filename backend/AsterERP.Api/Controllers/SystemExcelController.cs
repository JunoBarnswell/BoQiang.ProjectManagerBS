using AsterERP.Api.Application.System.Excel;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/system/excel")]
public sealed class SystemExcelController(IParameterExcelService parameterExcelService) : BaseApiController
{
    [HttpGet("parameters/export")]
    [Permission(PermissionCodes.SystemExcelManage)]
    public async Task<IActionResult> ExportParametersAsync(CancellationToken cancellationToken)
    {
        var bytes = await parameterExcelService.ExportParametersAsync(cancellationToken);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"parameters-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
    }

    [HttpPost("parameters/import")]
    [Permission(PermissionCodes.SystemExcelManage)]
    public async Task<IActionResult> ImportParametersAsync([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        return ApiOk(await parameterExcelService.ImportParametersAsync(stream, cancellationToken));
    }
}
