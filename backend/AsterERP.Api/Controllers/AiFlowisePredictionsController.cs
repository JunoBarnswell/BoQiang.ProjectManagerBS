using AsterERP.Api.Application.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/ai/flowise/prediction")]
public sealed class AiFlowisePredictionsController(IFlowisePredictionService predictionService) : BaseApiController
{
    [HttpGet("messages")]
    [Permission(PermissionCodes.FlowiseView)]
    public async Task<IActionResult> GetMessagesAsync([FromQuery] FlowisePredictionListQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await predictionService.GetMessagesAsync(query, cancellationToken));
    }

    [HttpGet("leads")]
    [Permission(PermissionCodes.FlowiseView)]
    public async Task<IActionResult> GetLeadsAsync([FromQuery] FlowisePredictionListQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await predictionService.GetLeadsAsync(query, cancellationToken));
    }

    [HttpPost]
    [Permission(PermissionCodes.FlowiseRun)]
    public async Task<IActionResult> PredictAsync([FromBody] FlowisePredictionRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await predictionService.PredictAsync(request, cancellationToken));
    }

    [HttpPost("stream")]
    [Permission(PermissionCodes.FlowiseRun)]
    public async Task StreamAsync([FromBody] FlowisePredictionRequest request, CancellationToken cancellationToken)
    {
        await predictionService.StreamAsync(request, Response, cancellationToken);
    }

    [HttpPost("feedback")]
    [Permission(PermissionCodes.FlowiseView)]
    public async Task<IActionResult> SaveFeedbackAsync([FromBody] FlowiseFeedbackRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await predictionService.SaveFeedbackAsync(request, cancellationToken));
    }

    [HttpPost("lead")]
    [Permission(PermissionCodes.FlowiseView)]
    public async Task<IActionResult> SaveLeadAsync([FromBody] FlowiseLeadRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await predictionService.SaveLeadAsync(request, cancellationToken));
    }

    [HttpPost("messages/clear")]
    [Permission(PermissionCodes.FlowiseView)]
    public async Task<IActionResult> ClearChatAsync([FromBody] FlowiseChatClearRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await predictionService.ClearChatAsync(request, cancellationToken));
    }

    [HttpPost("messages/abort")]
    [Permission(PermissionCodes.FlowiseRun)]
    public async Task<IActionResult> AbortChatAsync([FromBody] FlowisePredictionAbortRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await predictionService.AbortChatAsync(request, cancellationToken));
    }
}
