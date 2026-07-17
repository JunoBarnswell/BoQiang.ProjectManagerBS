using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/application-data-center/application-assignments")]
public sealed class ApplicationSystemAssignmentsController(ApplicationSystemAssignmentService service) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.AppDataCenterDataSourceView)]
    public async Task<IActionResult> GetListAsync(CancellationToken cancellationToken) =>
        ApiOk(await service.GetListAsync(cancellationToken));

    [HttpPut]
    [Permission(PermissionCodes.AppDataCenterDataSourceEdit)]
    public async Task<IActionResult> UpdateAsync([FromBody] ApplicationSystemAssignmentUpdateRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.UpdateAsync(request, cancellationToken));
}
