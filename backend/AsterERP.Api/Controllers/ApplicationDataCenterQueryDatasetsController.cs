using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/application-data-center/query-datasets")]
public sealed class ApplicationDataCenterQueryDatasetsController(
    ApplicationQueryDatasetService service,
    ApplicationDataMutationLedgerService mutationLedgerService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.AppDataCenterQueryDatasetView)]
    public async Task<IActionResult> GetPageAsync([FromQuery] ApplicationDataCenterObjectListQuery query, CancellationToken cancellationToken) => ApiOk(await service.GetPageAsync(query, cancellationToken));

    [HttpGet("{id}")]
    [Permission(PermissionCodes.AppDataCenterQueryDatasetView)]
    public async Task<IActionResult> GetAsync(string id, CancellationToken cancellationToken) => ApiOk(await service.GetAsync(id, cancellationToken));

    [HttpPost]
    [Permission(PermissionCodes.AppDataCenterQueryDatasetAdd)]
    public async Task<IActionResult> CreateAsync([FromBody] ApplicationDataCenterObjectUpsertRequest request, CancellationToken cancellationToken) => ApiOk(await service.CreateAsync(request, cancellationToken));

    [HttpPut("{id}")]
    [Permission(PermissionCodes.AppDataCenterQueryDatasetEdit)]
    public async Task<IActionResult> UpdateAsync(string id, [FromBody] ApplicationDataCenterObjectUpsertRequest request, CancellationToken cancellationToken) => ApiOk(await service.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.AppDataCenterQueryDatasetDelete)]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken) => ApiOk(await service.DeleteAsync(id, cancellationToken));

    [HttpPost("{id}/enable")]
    [Permission(PermissionCodes.AppDataCenterQueryDatasetEnable)]
    public async Task<IActionResult> EnableAsync(string id, CancellationToken cancellationToken) => ApiOk(await service.EnableAsync(id, cancellationToken));

    [HttpPost("{id}/disable")]
    [Permission(PermissionCodes.AppDataCenterQueryDatasetDisable)]
    public async Task<IActionResult> DisableAsync(string id, CancellationToken cancellationToken) => ApiOk(await service.DisableAsync(id, cancellationToken));

    [HttpPost("{id}/test")]
    [Permission(PermissionCodes.AppDataCenterQueryDatasetTest)]
    public async Task<IActionResult> TestAsync(string id, [FromBody] ApplicationDataCenterActionRequest request, CancellationToken cancellationToken) => ApiOk(await service.TestAsync(id, request, cancellationToken));

    [HttpPost("{id}/preview")]
    [Permission(PermissionCodes.AppDataCenterQueryDatasetPreview)]
    public async Task<IActionResult> PreviewAsync(string id, [FromBody] ApplicationDataCenterPreviewRequest request, CancellationToken cancellationToken) => ApiOk(await service.PreviewAsync(id, request, cancellationToken));

    [HttpPost("{id}/runtime/query")]
    [Permission(PermissionCodes.AppDataCenterQueryDatasetView)]
    public async Task<IActionResult> QueryRuntimeAsync(string id, [FromBody] ApplicationDataCenterRuntimeQueryRequest request, CancellationToken cancellationToken) => ApiOk(await service.QueryRuntimeAsync(id, request, cancellationToken));

    [HttpPost("query-plan/diagnose")]
    [Permission(PermissionCodes.AppDataCenterQueryDatasetPreview)]
    public async Task<IActionResult> DiagnoseQueryPlanAsync([FromBody] ApplicationQueryPlanRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.DiagnoseQueryPlanAsync(request, cancellationToken));

    [HttpPost("query-plan/risk-confirmation")]
    [Permission(PermissionCodes.AppDataCenterQueryDatasetEdit)]
    public async Task<IActionResult> IssueQueryPlanRiskConfirmationAsync([FromBody] ApplicationQueryPlanRiskConfirmationRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.IssueRiskConfirmationAsync(request, cancellationToken));

    [HttpPost("query-plan/preview")]
    [Permission(PermissionCodes.AppDataCenterQueryDatasetPreview)]
    public async Task<IActionResult> PreviewQueryPlanAsync([FromBody] ApplicationQueryPlanRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.PreviewQueryPlanAsync(request, cancellationToken));

    [HttpPost("query-plan/execute")]
    [Permission(PermissionCodes.AppDataCenterQueryDatasetView)]
    public async Task<IActionResult> ExecuteQueryPlanAsync([FromBody] ApplicationQueryPlanRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.ExecuteQueryPlanAsync(request, cancellationToken));

    [HttpPost("query-plan/write")]
    [Permission(PermissionCodes.AppDataCenterQueryDatasetEdit)]
    public async Task<IActionResult> ExecuteControlledWriteQueryPlanAsync([FromBody] ApplicationQueryPlanRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.ExecuteControlledWriteQueryPlanAsync(request, cancellationToken));

    [HttpGet("mutation-ledgers/{ledgerId}")]
    [Permission(PermissionCodes.AppDataCenterDataSourceView)]
    public async Task<IActionResult> GetMutationLedgerAsync(string ledgerId, CancellationToken cancellationToken) =>
        ApiOk(await mutationLedgerService.GetAsync(ledgerId, cancellationToken));

    [HttpPost("mutation-ledgers/{ledgerId}/reconcile")]
    [Permission(PermissionCodes.AppDataCenterMutationRecovery)]
    public async Task<IActionResult> ReconcileMutationLedgerAsync(
        string ledgerId,
        [FromBody] ApplicationDataMutationLedgerReconcileRequest request,
        CancellationToken cancellationToken)
    {
        request.LedgerId = ledgerId;
        return ApiOk(await mutationLedgerService.ReconcileAsync(request, cancellationToken));
    }

    [HttpPost("{id}/publish")]
    [Permission(PermissionCodes.AppDataCenterQueryDatasetPublish)]
    public async Task<IActionResult> PublishAsync(string id, [FromBody] ApplicationDataCenterPublishRequest request, CancellationToken cancellationToken) => ApiOk(await service.PublishAsync(id, request, cancellationToken));

    [HttpGet("{id}/references")]
    [Permission(PermissionCodes.AppDataCenterQueryDatasetReference)]
    public async Task<IActionResult> GetReferencesAsync(string id, CancellationToken cancellationToken) => ApiOk(await service.GetReferencesAsync(id, cancellationToken));
}
