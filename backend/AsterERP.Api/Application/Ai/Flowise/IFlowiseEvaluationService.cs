using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise.Evaluations;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Flowise;

public interface IFlowiseEvaluationService
{
    Task<GridPageResult<FlowiseDatasetListItemDto>> GetDatasetsAsync(FlowiseStudioQuery query, CancellationToken cancellationToken);

    Task<FlowiseDatasetListItemDto> CreateDatasetAsync(FlowiseDatasetSaveRequest request, CancellationToken cancellationToken);

    Task<FlowiseDatasetListItemDto> UpdateDatasetAsync(string id, FlowiseDatasetSaveRequest request, CancellationToken cancellationToken);

    Task<FlowiseDatasetCsvImportDto> ImportDatasetCsvAsync(string id, Stream csvStream, bool firstRowHeaders, CancellationToken cancellationToken);

    Task<bool> DeleteDatasetAsync(string id, CancellationToken cancellationToken);

    Task<GridPageResult<FlowiseEvaluatorListItemDto>> GetEvaluatorsAsync(FlowiseStudioQuery query, CancellationToken cancellationToken);

    Task<FlowiseEvaluatorListItemDto> CreateEvaluatorAsync(FlowiseEvaluatorSaveRequest request, CancellationToken cancellationToken);

    Task<FlowiseEvaluatorListItemDto> UpdateEvaluatorAsync(string id, FlowiseEvaluatorSaveRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteEvaluatorAsync(string id, CancellationToken cancellationToken);

    Task<GridPageResult<FlowiseEvaluationListItemDto>> GetEvaluationsAsync(FlowiseStudioQuery query, CancellationToken cancellationToken);

    Task<FlowiseEvaluationListItemDto> CreateEvaluationAsync(FlowiseEvaluationSaveRequest request, CancellationToken cancellationToken);

    Task<FlowiseEvaluationListItemDto> UpdateEvaluationAsync(string id, FlowiseEvaluationSaveRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteEvaluationAsync(string id, CancellationToken cancellationToken);

    Task<FlowiseDatasetDto> GetDatasetAsync(string id, CancellationToken cancellationToken);

    Task<IReadOnlyList<FlowiseDatasetRowDto>> GetDatasetRowsAsync(string id, CancellationToken cancellationToken);

    Task<FlowiseEvaluatorDto> GetEvaluatorAsync(string id, CancellationToken cancellationToken);

    Task<FlowiseEvaluationDto> GetEvaluationAsync(string id, CancellationToken cancellationToken);

    Task<FlowiseEvaluationResultDto> GetResultAsync(string id, CancellationToken cancellationToken);

    Task<FlowiseEvaluationResultDto> RunAgainAsync(string id, CancellationToken cancellationToken);
}
