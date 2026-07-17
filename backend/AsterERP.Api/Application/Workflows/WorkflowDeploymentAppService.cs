using System.Text;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Contracts.Workflows;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using AsterERP.Workflow.Core.Services;
using AsterERP.Workflow.Persistence.Entities;
using SqlSugar;

namespace AsterERP.Api.Application.Workflows;

public sealed class WorkflowDeploymentAppService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IRepositoryService repositoryService) : IWorkflowDeploymentAppService
{
    public async Task<GridPageResult<WorkflowDeploymentListItemResponse>> GetPageAsync(GridQuery query, CancellationToken cancellationToken = default)
    {
        var pageIndex = Math.Max(query.PageIndex, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var total = new RefAsync<int>();
        var deployments = await databaseAccessor.GetCurrentDb().Queryable<DeploymentEntity>()
            .WhereIF(!string.IsNullOrWhiteSpace(query.Keyword), item => item.Name!.Contains(query.Keyword!) || item.Key!.Contains(query.Keyword!))
            .WhereIF(!string.IsNullOrWhiteSpace(query.AppCode), item => item.TenantId == query.AppCode)
            .OrderBy(item => item.DeployTime, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, total, cancellationToken);
        var deploymentIds = deployments.Select(item => item.Id).ToList();
        var resources = deploymentIds.Count == 0
            ? []
            : await databaseAccessor.GetCurrentDb().Queryable<ResourceEntity>()
                .Where(item => deploymentIds.Contains(item.DeploymentId!))
                .ToListAsync(cancellationToken);

        return new GridPageResult<WorkflowDeploymentListItemResponse>
        {
            Total = total.Value,
            Items = deployments.Select(item => new WorkflowDeploymentListItemResponse(
                item.Id,
                item.Name,
                item.Category,
                item.Key,
                item.TenantId,
                item.DeployTime,
                resources.Where(resource => resource.DeploymentId == item.Id).Select(resource => resource.Name ?? string.Empty).Where(name => name.Length > 0).ToList())).ToList()
        };
    }

    public async Task<IReadOnlyList<WorkflowProcessDefinitionResponse>> GetProcessDefinitionsAsync(string? key, CancellationToken cancellationToken = default)
    {
        var normalizedKey = string.IsNullOrWhiteSpace(key) ? null : key.Trim();
        var currentAppCode = currentUser.GetAsterErpAppCode();
        var appCode = string.IsNullOrWhiteSpace(currentAppCode) ? null : currentAppCode.Trim().ToUpperInvariant();
        var definitions = await databaseAccessor.GetCurrentDb().Queryable<ProcessDefinitionEntity>()
            .WhereIF(!string.IsNullOrWhiteSpace(normalizedKey), item => item.Key == normalizedKey)
            .WhereIF(!string.IsNullOrWhiteSpace(appCode), item => item.TenantId == appCode)
            .OrderBy(item => item.Version, OrderByType.Desc)
            .OrderBy(item => item.Name)
            .Take(200)
            .ToListAsync(cancellationToken);

        return definitions
            .Select(item => new WorkflowProcessDefinitionResponse(
                item.Id,
                item.Key,
                item.Name,
                item.DeploymentId,
                item.Version,
                item.Category,
                item.Description,
                item.SuspensionState != 1,
                item.TenantId))
            .ToList();
    }

    public async Task<WorkflowDeploymentResourceResponse> GetResourceAsync(string deploymentId, string resourceName, CancellationToken cancellationToken = default)
    {
        var bytes = await repositoryService.GetResourceAsync(deploymentId, resourceName, cancellationToken)
            ?? throw new NotFoundException("部署资源不存在", ErrorCodes.WorkflowProcessDefinitionNotFound);

        var contentType = resourceName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
                          resourceName.EndsWith(".bpmn", StringComparison.OrdinalIgnoreCase)
            ? "application/xml"
            : "application/octet-stream";

        var content = contentType == "application/xml"
            ? Encoding.UTF8.GetString(bytes)
            : Convert.ToBase64String(bytes);

        return new WorkflowDeploymentResourceResponse(deploymentId, resourceName, contentType, content);
    }
}

