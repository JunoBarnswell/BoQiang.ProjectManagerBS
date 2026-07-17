using AsterERP.Api.Application.AsterScene;
using AsterERP.Contracts.AsterScene;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/subscriptions/asterscene")]
public sealed class AsterSceneSubscriptionsController(AsterSceneCommerceGovernanceService service) : BaseApiController
{
    [HttpGet("plans")]
    public async Task<IActionResult> GetPlansAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await service.GetPlansAsync(cancellationToken));
    }

    [HttpGet("current")]
    [Permission(PermissionCodes.AsterSceneSubscriptionManage)]
    public async Task<IActionResult> GetCurrentAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await service.GetSubscriptionAsync(cancellationToken));
    }

    [HttpPost("current")]
    [Permission(PermissionCodes.AsterSceneSubscriptionManage)]
    public async Task<IActionResult> SubscribeAsync([FromBody] AsterSceneSubscribeRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await service.SubscribeAsync(request, cancellationToken));
    }

    [HttpPost("current/cancel")]
    [Permission(PermissionCodes.AsterSceneSubscriptionManage)]
    public async Task<IActionResult> CancelAsync([FromBody] AsterSceneSubscriptionLifecycleRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await service.CancelSubscriptionAsync(request, cancellationToken));
    }

    [HttpPost("current/payment-failed")]
    [Permission(PermissionCodes.AsterSceneSubscriptionManage)]
    public async Task<IActionResult> MarkPaymentFailedAsync([FromBody] AsterSceneSubscriptionLifecycleRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await service.MarkSubscriptionPaymentFailedAsync(request, cancellationToken));
    }

    [HttpPost("current/expire")]
    [Permission(PermissionCodes.AsterSceneSubscriptionManage)]
    public async Task<IActionResult> ExpireAsync([FromBody] AsterSceneSubscriptionLifecycleRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await service.ExpireSubscriptionAsync(request, cancellationToken));
    }
}
