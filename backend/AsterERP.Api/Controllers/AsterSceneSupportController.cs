using AsterERP.Api.Application.AsterScene;
using AsterERP.Contracts.AsterScene;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/asterscene/support")]
public sealed class AsterSceneSupportController(AsterSceneCommerceGovernanceService service) : BaseApiController
{
    [HttpGet("tickets/{ticketId}")]
    [Permission(PermissionCodes.AsterSceneSupportView)]
    public async Task<IActionResult> GetTicketAsync(string ticketId, CancellationToken cancellationToken)
    {
        return ApiOk(await service.GetSupportTicketAsync(ticketId, cancellationToken));
    }

    [HttpPost("tickets")]
    [Permission(PermissionCodes.AsterSceneSupportCreate)]
    public async Task<IActionResult> CreateTicketAsync(
        [FromBody] AsterSceneSupportBundleRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await service.CreateSupportTicketAsync(request, cancellationToken));
    }

    [HttpPost("tickets/{ticketId}/comments")]
    [Permission(PermissionCodes.AsterSceneSupportComment)]
    public async Task<IActionResult> AddCommentAsync(
        string ticketId,
        [FromBody] AsterSceneSupportCommentRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await service.AddSupportCommentAsync(ticketId, request, cancellationToken));
    }

    [HttpPost("tickets/{ticketId}/close")]
    [Permission(PermissionCodes.AsterSceneSupportClose)]
    public async Task<IActionResult> CloseTicketAsync(
        string ticketId,
        [FromBody] AsterSceneSupportCloseRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await service.CloseSupportTicketAsync(ticketId, request, cancellationToken));
    }

    [HttpGet("admin/tickets")]
    [Permission(PermissionCodes.AsterSceneSupportAdminView)]
    public async Task<IActionResult> GetAdminTicketsAsync([FromQuery] AsterSceneGridQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await service.GetSupportTicketsForAdminAsync(query, cancellationToken));
    }

    [HttpGet("admin/tickets/{ticketId}")]
    [Permission(PermissionCodes.AsterSceneSupportAdminView)]
    public async Task<IActionResult> GetAdminTicketAsync(string ticketId, CancellationToken cancellationToken)
    {
        return ApiOk(await service.GetSupportTicketForAdminAsync(ticketId, cancellationToken));
    }

    [HttpPost("admin/tickets/{ticketId}/comments")]
    [Permission(PermissionCodes.AsterSceneSupportAdminManage)]
    public async Task<IActionResult> AddAdminCommentAsync(
        string ticketId,
        [FromBody] AsterSceneSupportCommentRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await service.AddAdminSupportCommentAsync(ticketId, request, cancellationToken));
    }

    [HttpPost("admin/tickets/{ticketId}/status")]
    [Permission(PermissionCodes.AsterSceneSupportAdminManage)]
    public async Task<IActionResult> ChangeAdminStatusAsync(
        string ticketId,
        [FromBody] AsterSceneSupportTicketStatusRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await service.ChangeAdminSupportTicketStatusAsync(ticketId, request, cancellationToken));
    }
}
