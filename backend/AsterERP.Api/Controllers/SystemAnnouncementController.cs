using AsterERP.Api.Application.System.Announcements;
using AsterERP.Contracts.System.Announcements;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/system/announcements")]
public sealed class SystemAnnouncementController(IAnnouncementService announcementService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.SystemAnnouncementQuery)]
    public async Task<IActionResult> GetPageAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await announcementService.GetPageAsync(gridQuery, cancellationToken));
    }

    [HttpPost]
    [Permission(PermissionCodes.SystemAnnouncementAdd)]
    public async Task<IActionResult> CreateAsync([FromBody] AnnouncementUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await announcementService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id}")]
    [Permission(PermissionCodes.SystemAnnouncementEdit)]
    public async Task<IActionResult> UpdateAsync(string id, [FromBody] AnnouncementUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await announcementService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.SystemAnnouncementDelete)]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await announcementService.DeleteAsync(id, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("{id}/publish")]
    [Permission(PermissionCodes.SystemAnnouncementPublish)]
    public async Task<IActionResult> PublishAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await announcementService.PublishAsync(id, cancellationToken));
    }

    [HttpPost("{id}/withdraw")]
    [Permission(PermissionCodes.SystemAnnouncementWithdraw)]
    public async Task<IActionResult> WithdrawAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await announcementService.WithdrawAsync(id, cancellationToken));
    }

    [HttpPost("{id}/top")]
    [Permission(PermissionCodes.SystemAnnouncementTop)]
    public async Task<IActionResult> SetTopAsync(string id, [FromBody] AnnouncementTopRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await announcementService.SetTopAsync(id, request, cancellationToken));
    }
}
