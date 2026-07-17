using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/application-data-center/data-sources")]
public sealed class ApplicationDataCenterDataSourcesController(
    ApplicationDataSourceService service,
    ApplicationDataSourceWorkbenchService workbenchService,
    ApplicationDataSourceCatalogService catalogService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.AppDataCenterDataSourceView)]
    public async Task<IActionResult> GetPageAsync([FromQuery] ApplicationDataCenterObjectListQuery query, CancellationToken cancellationToken) => ApiOk(await service.GetPageAsync(query, cancellationToken));

    [HttpGet("migration-required")]
    [Permission(PermissionCodes.AppDataCenterDataSourceView)]
    public async Task<IActionResult> GetMigrationInventoryAsync(CancellationToken cancellationToken) =>
        ApiOk(await service.GetMigrationInventoryAsync(cancellationToken));

    [HttpGet("{id}")]
    [Permission(PermissionCodes.AppDataCenterDataSourceView)]
    public async Task<IActionResult> GetAsync(string id, CancellationToken cancellationToken) => ApiOk(await service.GetAsync(id, cancellationToken));

    [HttpPost]
    [Permission(PermissionCodes.AppDataCenterDataSourceAdd)]
    public async Task<IActionResult> CreateAsync([FromBody] UpdateDataSourceDefinitionCommand request, CancellationToken cancellationToken) => ApiOk(await service.CreateAsync(request, cancellationToken));

    [HttpPut("{id}")]
    [Permission(PermissionCodes.AppDataCenterDataSourceEdit)]
    public async Task<IActionResult> UpdateAsync(string id, [FromBody] UpdateDataSourceDefinitionCommand request, CancellationToken cancellationToken) => ApiOk(await service.UpdateDefinitionAsync(id, request, cancellationToken));

    [HttpPost("{id}/secret/replace")]
    [Permission(PermissionCodes.AppDataCenterDataSourceEdit)]
    public async Task<IActionResult> ReplaceSecretAsync(string id, [FromBody] ApplicationDataSourceSecretReplaceRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.ReplaceSecretAsync(id, request, cancellationToken));

    [HttpPost("{id}/secret/clear")]
    [Permission(PermissionCodes.AppDataCenterDataSourceEdit)]
    public async Task<IActionResult> ClearSecretAsync(string id, [FromBody] ApplicationDataSourceSecretClearRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.ClearSecretAsync(id, request, cancellationToken));

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.AppDataCenterDataSourceDelete)]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken) => ApiOk(await service.DeleteAsync(id, cancellationToken));

    [HttpPost("{id}/enable")]
    [Permission(PermissionCodes.AppDataCenterDataSourceEnable)]
    public async Task<IActionResult> EnableAsync(string id, CancellationToken cancellationToken) => ApiOk(await service.EnableAsync(id, cancellationToken));

    [HttpPost("{id}/disable")]
    [Permission(PermissionCodes.AppDataCenterDataSourceDisable)]
    public async Task<IActionResult> DisableAsync(string id, CancellationToken cancellationToken) => ApiOk(await service.DisableAsync(id, cancellationToken));

    [HttpPost("{id}/test")]
    [Permission(PermissionCodes.AppDataCenterDataSourceTest)]
    public async Task<IActionResult> TestAsync(string id, [FromBody] ApplicationDataCenterActionRequest request, CancellationToken cancellationToken) => ApiOk(await service.TestAsync(id, request, cancellationToken));

    [HttpPost("{id}/diagnose")]
    [Permission(PermissionCodes.AppDataCenterDataSourceTest)]
    public async Task<IActionResult> DiagnoseAsync(string id, CancellationToken cancellationToken) => ApiOk(await service.DiagnoseAsync(id, cancellationToken));

    [HttpPost("draft/diagnose")]
    [Permission(PermissionCodes.AppDataCenterDataSourceTest)]
    public async Task<IActionResult> DiagnoseDraftAsync(
        [FromBody] ApplicationDataSourceDraftDiagnosticRequest request,
        CancellationToken cancellationToken) =>
        ApiOk(await service.DiagnoseDraftAsync(request, cancellationToken));

    [HttpPost("{id}/preview")]
    [Permission(PermissionCodes.AppDataCenterDataSourcePreview)]
    public async Task<IActionResult> PreviewAsync(string id, [FromBody] ApplicationDataCenterPreviewRequest request, CancellationToken cancellationToken) => ApiOk(await service.PreviewAsync(id, request, cancellationToken));

    [HttpPost("{id}/publish")]
    [Permission(PermissionCodes.AppDataCenterDataSourcePublish)]
    public async Task<IActionResult> PublishAsync(string id, [FromBody] ApplicationDataCenterPublishRequest request, CancellationToken cancellationToken) => ApiOk(await service.PublishAsync(id, request, cancellationToken));

    [HttpGet("{id}/references")]
    [Permission(PermissionCodes.AppDataCenterDataSourceReference)]
    public async Task<IActionResult> GetReferencesAsync(string id, CancellationToken cancellationToken) => ApiOk(await service.GetReferencesAsync(id, cancellationToken));

    [HttpGet("{id}/metadata/tables")]
    [Permission(PermissionCodes.AppDataCenterDataSourceView)]
    public async Task<IActionResult> GetTablesAsync(string id, CancellationToken cancellationToken) => ApiOk(await service.GetTablesAsync(id, cancellationToken));

    [HttpGet("{id}/metadata/tables/{tableName}/columns")]
    [Permission(PermissionCodes.AppDataCenterDataSourceView)]
    public async Task<IActionResult> GetColumnsAsync(string id, string tableName, CancellationToken cancellationToken) => ApiOk(await service.GetColumnsAsync(id, tableName, cancellationToken));

    [HttpGet("{id}/workbench")]
    [Permission(PermissionCodes.AppDataCenterDataSourceView)]
    public async Task<IActionResult> GetWorkbenchAsync(string id, CancellationToken cancellationToken) => ApiOk(await workbenchService.GetAsync(id, cancellationToken));

    [HttpGet("{id}/runtime-checks")]
    [Permission(PermissionCodes.AppDataCenterDataSourceView)]
    public async Task<IActionResult> GetRuntimeChecksAsync(string id, CancellationToken cancellationToken) => ApiOk(await workbenchService.GetRuntimeChecksAsync(id, cancellationToken));

    [HttpGet("{id}/catalog")]
    [Permission(PermissionCodes.AppDataCenterDataSourceView)]
    public async Task<IActionResult> GetCatalogAsync(string id, CancellationToken cancellationToken) => ApiOk(await catalogService.GetLatestAsync(id, cancellationToken));

    [HttpPost("{id}/catalog/refresh")]
    [Permission(PermissionCodes.AppDataCenterDataSourceView)]
    public async Task<IActionResult> RefreshCatalogAsync(string id, CancellationToken cancellationToken) => ApiOk(await catalogService.RefreshAsync(id, cancellationToken));

    [HttpPost("{id}/catalog/refresh-node")]
    [Permission(PermissionCodes.AppDataCenterDataSourceView)]
    public async Task<IActionResult> RefreshCatalogNodeAsync(
        string id,
        [FromBody] ApplicationDataSourceCatalogRefreshRequest request,
        CancellationToken cancellationToken) =>
        ApiOk(await catalogService.RefreshNodeAsync(id, request, cancellationToken));

    [HttpPost("{id}/sqlite-path-approvals")]
    [Permission(PermissionCodes.AppDataCenterDataSourceEdit)]
    public async Task<IActionResult> RequestSqlitePathApprovalAsync(
        string id,
        [FromBody] ApplicationDataSourceSqlitePathApprovalRequest request,
        ApplicationDataSourceSqlitePathApprovalService approvalService,
        CancellationToken cancellationToken)
    {
        var normalizedRequest = request with { DataSourceId = id };
        return ApiOk(await approvalService.RequestAsync(normalizedRequest, cancellationToken));
    }

    [HttpGet("{id}/sqlite-path-approvals")]
    [Permission(PermissionCodes.AppDataCenterDataSourceView)]
    public async Task<IActionResult> ListSqlitePathApprovalsAsync(
        string id,
        ApplicationDataSourceSqlitePathApprovalService approvalService,
        CancellationToken cancellationToken) =>
        ApiOk(await approvalService.ListAsync(id, cancellationToken));

    [HttpPost("{id}/sqlite-path-approvals/{approvalId}/approve")]
    [Permission(PermissionCodes.AppDataCenterDataSourcePublish)]
    public async Task<IActionResult> ApproveSqlitePathApprovalAsync(
        string id,
        string approvalId,
        ApplicationDataSourceSqlitePathApprovalService approvalService,
        CancellationToken cancellationToken) =>
        ApiOk(await approvalService.ApproveAsync(id, new ApplicationDataSourceSqlitePathApprovalDecisionRequest(approvalId), cancellationToken));

    [HttpPost("{id}/sqlite-path-approvals/{approvalId}/reject")]
    [Permission(PermissionCodes.AppDataCenterDataSourcePublish)]
    public async Task<IActionResult> RejectSqlitePathApprovalAsync(
        string id,
        string approvalId,
        ApplicationDataSourceSqlitePathApprovalService approvalService,
        CancellationToken cancellationToken) =>
        ApiOk(await approvalService.RejectAsync(id, new ApplicationDataSourceSqlitePathApprovalDecisionRequest(approvalId), cancellationToken));

    [HttpPost("{id}/sqlite-path-approvals/{approvalId}/revoke")]
    [Permission(PermissionCodes.AppDataCenterDataSourcePublish)]
    public async Task<IActionResult> RevokeSqlitePathApprovalAsync(
        string id,
        string approvalId,
        ApplicationDataSourceSqlitePathApprovalService approvalService,
        CancellationToken cancellationToken) =>
        ApiOk(await approvalService.RevokeAsync(id, new ApplicationDataSourceSqlitePathApprovalDecisionRequest(approvalId), cancellationToken));
}
