using AsterERP.Api.Application.System.Organizations;
using AsterERP.Shared;
using AsterERP.Contracts.System;
using AsterERP.Contracts.System.Organizations;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/system/departments")]
public sealed class SystemDepartmentController(ISystemDepartmentService departmentService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.SystemDeptQuery)]
    public async Task<IActionResult> GetPageAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await departmentService.GetPageAsync(gridQuery, cancellationToken));
    }

    [HttpGet("tree")]
    [Permission(PermissionCodes.SystemDeptQuery)]
    public async Task<IActionResult> GetTreeAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await departmentService.GetTreeAsync(cancellationToken));
    }

    [HttpGet("{id}")]
    [Permission(PermissionCodes.SystemDeptQuery)]
    public async Task<IActionResult> GetDetailAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await departmentService.GetDetailAsync(id, cancellationToken));
    }

    [HttpPost]
    [Permission(PermissionCodes.SystemDeptAdd)]
    public async Task<IActionResult> CreateAsync([FromBody] DepartmentUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await departmentService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id}")]
    [Permission(PermissionCodes.SystemDeptEdit)]
    public async Task<IActionResult> UpdateAsync(string id, [FromBody] DepartmentUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await departmentService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.SystemDeptDelete)]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await departmentService.DeleteAsync(id, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("batch-delete")]
    [Permission(PermissionCodes.SystemDeptDelete)]
    public async Task<IActionResult> BatchDeleteAsync([FromBody] BatchDeleteRequest request, CancellationToken cancellationToken)
    {
        await departmentService.BatchDeleteAsync(request.Ids, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("batch-status")]
    [Permission(PermissionCodes.SystemDeptEdit)]
    public async Task<IActionResult> BatchUpdateStatusAsync([FromBody] BatchStatusUpdateRequest request, CancellationToken cancellationToken)
    {
        await departmentService.BatchUpdateStatusAsync(request.Ids, request.Status, cancellationToken);
        return ApiOk(true);
    }
}
