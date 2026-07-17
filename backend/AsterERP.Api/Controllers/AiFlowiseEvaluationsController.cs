using AsterERP.Api.Application.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise.Evaluations;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/ai/flowise")]
public sealed class AiFlowiseEvaluationsController(IFlowiseEvaluationService evaluationService) : BaseApiController
{
    [HttpGet("datasets")]
    [Permission(PermissionCodes.FlowiseDatasetsView)]
    public async Task<IActionResult> GetDatasetsAsync([FromQuery] FlowiseStudioQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await evaluationService.GetDatasetsAsync(query, cancellationToken));
    }

    [HttpPost("datasets")]
    [Permission(PermissionCodes.FlowiseDatasetsEdit)]
    public async Task<IActionResult> CreateDatasetAsync([FromBody] FlowiseDatasetSaveRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await evaluationService.CreateDatasetAsync(request, cancellationToken));
    }

    [HttpPut("datasets/{id}")]
    [Permission(PermissionCodes.FlowiseDatasetsEdit)]
    public async Task<IActionResult> UpdateDatasetAsync(string id, [FromBody] FlowiseDatasetSaveRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await evaluationService.UpdateDatasetAsync(id, request, cancellationToken));
    }

    [HttpPost("datasets/{id}/upload-csv")]
    [Permission(PermissionCodes.FlowiseDatasetsEdit)]
    public async Task<IActionResult> UploadDatasetCsvAsync(
        string id,
        [FromForm] IFormFile file,
        [FromForm] bool firstRowHeaders,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new ValidationException("CSV 文件不能为空", ErrorCodes.ParameterInvalid);
        }

        await using var stream = file.OpenReadStream();
        return ApiOk(await evaluationService.ImportDatasetCsvAsync(id, stream, firstRowHeaders, cancellationToken));
    }

    [HttpDelete("datasets/{id}")]
    [Permission(PermissionCodes.FlowiseDatasetsEdit)]
    public async Task<IActionResult> DeleteDatasetAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await evaluationService.DeleteDatasetAsync(id, cancellationToken));
    }

    [HttpGet("datasets/{id}/detail")]
    [Permission(PermissionCodes.FlowiseDatasetsView)]
    public async Task<IActionResult> GetDatasetAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await evaluationService.GetDatasetAsync(id, cancellationToken));
    }

    [HttpGet("datasets/{id}/rows")]
    [Permission(PermissionCodes.FlowiseDatasetsView)]
    public async Task<IActionResult> GetDatasetRowsAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await evaluationService.GetDatasetRowsAsync(id, cancellationToken));
    }

    [HttpGet("evaluators")]
    [Permission(PermissionCodes.FlowiseEvaluatorsView)]
    public async Task<IActionResult> GetEvaluatorsAsync([FromQuery] FlowiseStudioQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await evaluationService.GetEvaluatorsAsync(query, cancellationToken));
    }

    [HttpPost("evaluators")]
    [Permission(PermissionCodes.FlowiseEvaluatorsEdit)]
    public async Task<IActionResult> CreateEvaluatorAsync([FromBody] FlowiseEvaluatorSaveRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await evaluationService.CreateEvaluatorAsync(request, cancellationToken));
    }

    [HttpPut("evaluators/{id}")]
    [Permission(PermissionCodes.FlowiseEvaluatorsEdit)]
    public async Task<IActionResult> UpdateEvaluatorAsync(string id, [FromBody] FlowiseEvaluatorSaveRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await evaluationService.UpdateEvaluatorAsync(id, request, cancellationToken));
    }

    [HttpDelete("evaluators/{id}")]
    [Permission(PermissionCodes.FlowiseEvaluatorsEdit)]
    public async Task<IActionResult> DeleteEvaluatorAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await evaluationService.DeleteEvaluatorAsync(id, cancellationToken));
    }

    [HttpGet("evaluators/{id}/detail")]
    [Permission(PermissionCodes.FlowiseEvaluatorsView)]
    public async Task<IActionResult> GetEvaluatorAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await evaluationService.GetEvaluatorAsync(id, cancellationToken));
    }

    [HttpGet("evaluations")]
    [Permission(PermissionCodes.FlowiseEvaluationsView)]
    public async Task<IActionResult> GetEvaluationsAsync([FromQuery] FlowiseStudioQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await evaluationService.GetEvaluationsAsync(query, cancellationToken));
    }

    [HttpPost("evaluations")]
    [Permission(PermissionCodes.FlowiseEvaluationsEdit)]
    public async Task<IActionResult> CreateEvaluationAsync([FromBody] FlowiseEvaluationSaveRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await evaluationService.CreateEvaluationAsync(request, cancellationToken));
    }

    [HttpPut("evaluations/{id}")]
    [Permission(PermissionCodes.FlowiseEvaluationsEdit)]
    public async Task<IActionResult> UpdateEvaluationAsync(string id, [FromBody] FlowiseEvaluationSaveRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await evaluationService.UpdateEvaluationAsync(id, request, cancellationToken));
    }

    [HttpDelete("evaluations/{id}")]
    [Permission(PermissionCodes.FlowiseEvaluationsEdit)]
    public async Task<IActionResult> DeleteEvaluationAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await evaluationService.DeleteEvaluationAsync(id, cancellationToken));
    }

    [HttpGet("evaluations/{id}/detail")]
    [Permission(PermissionCodes.FlowiseEvaluationsView)]
    public async Task<IActionResult> GetEvaluationAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await evaluationService.GetEvaluationAsync(id, cancellationToken));
    }

    [HttpGet("evaluations/{id}/result")]
    [Permission(PermissionCodes.FlowiseEvaluationsView)]
    public async Task<IActionResult> GetResultAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await evaluationService.GetResultAsync(id, cancellationToken));
    }

    [HttpPost("evaluations/{id}/run-again")]
    [Permission(PermissionCodes.FlowiseRun)]
    public async Task<IActionResult> RunAgainAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await evaluationService.RunAgainAsync(id, cancellationToken));
    }
}
