using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Shared;
using Microsoft.AspNetCore.Http;

namespace AsterERP.Api.Application.Ai.Flowise;

public interface IFlowisePredictionService
{
    Task<GridPageResult<FlowiseChatMessageDto>> GetMessagesAsync(FlowisePredictionListQuery query, CancellationToken cancellationToken);

    Task<GridPageResult<FlowiseLeadDto>> GetLeadsAsync(FlowisePredictionListQuery query, CancellationToken cancellationToken);

    Task<FlowisePredictionResponse> PredictAsync(FlowisePredictionRequest request, CancellationToken cancellationToken);

    Task StreamAsync(FlowisePredictionRequest request, HttpResponse response, CancellationToken cancellationToken);

    Task<FlowiseFeedbackDto> SaveFeedbackAsync(FlowiseFeedbackRequest request, CancellationToken cancellationToken);

    Task<FlowiseLeadDto> SaveLeadAsync(FlowiseLeadRequest request, CancellationToken cancellationToken);

    Task<bool> ClearChatAsync(FlowiseChatClearRequest request, CancellationToken cancellationToken);

    Task<bool> AbortChatAsync(FlowisePredictionAbortRequest request, CancellationToken cancellationToken);
}
