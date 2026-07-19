using System.Diagnostics;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementTaskBatchExecutionService(
    IProjectManagementTaskBatchService batchService,
    IProjectManagementTaskService taskService,
    ICurrentUser currentUser) : IProjectManagementTaskBatchExecutionService
{
    private const int ChunkSize = 25;

    public async Task<ProjectManagementTaskBatchExecutionResult> ExecuteAsync(
        ProjectManagementTaskBatchUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectId)) throw new ValidationException("项目不能为空");
        if (request.Items is null || request.Items.Count == 0 || request.Items.Count > 200)
            throw new ValidationException("批量任务数量必须在 1 到 200 之间");
        EnsurePermission(request.Operation);

        var operationId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
        var results = new List<ProjectManagementTaskBatchItemResult>(request.Items.Count);
        foreach (var chunk in request.Items.Chunk(ChunkSize))
        {
            foreach (var item in chunk)
            {
                cancellationToken.ThrowIfCancellationRequested();
                results.Add(await ExecuteItemAsync(request, item, cancellationToken));
            }
        }

        return new ProjectManagementTaskBatchExecutionResult(
            operationId,
            request.ProjectId,
            results.Count,
            results.Count(item => item.Status == ProjectManagementTaskBatchResultStatuses.Succeeded),
            results.Count(item => item.Status == ProjectManagementTaskBatchResultStatuses.Skipped),
            results.Count(item => item.Status == ProjectManagementTaskBatchResultStatuses.Failed),
            results.Count(item => item.Status == ProjectManagementTaskBatchResultStatuses.Conflict),
            results);
    }

    private async Task<ProjectManagementTaskBatchItemResult> ExecuteItemAsync(
        ProjectManagementTaskBatchUpdateRequest request,
        ProjectManagementTaskBatchItem item,
        CancellationToken cancellationToken)
    {
        ProjectManagementTaskDetailResponse? task = null;
        try
        {
            task = await taskService.GetAsync(item.TaskId, cancellationToken);
            if (!string.Equals(task.ProjectId, request.ProjectId, StringComparison.Ordinal))
                throw new ValidationException("任务不属于当前项目", ErrorCodes.PermissionDenied);

            if (string.Equals(request.Operation, ProjectManagementTaskBatchOperations.Delete, StringComparison.OrdinalIgnoreCase))
            {
                await taskService.DeleteAsync(item.TaskId, new ProjectManagementTaskDeleteRequest(item.VersionNo, request.DeleteMode), cancellationToken);
            }
            else if (string.Equals(request.Operation, ProjectManagementTaskBatchOperations.Update, StringComparison.OrdinalIgnoreCase))
            {
                await batchService.UpdateAsync(request with
                {
                    Items = [item],
                    Operation = ProjectManagementTaskBatchOperations.Update
                }, cancellationToken);
            }
            else
            {
                throw new ValidationException("不支持的批量操作");
            }

            return new ProjectManagementTaskBatchItemResult(
                item.TaskId,
                task.TaskCode,
                ProjectManagementTaskBatchResultStatuses.Succeeded,
                null,
                null,
                task.VersionNo + 1);
        }
        catch (NotFoundException exception)
        {
            return new ProjectManagementTaskBatchItemResult(item.TaskId, task?.TaskCode, ProjectManagementTaskBatchResultStatuses.Skipped, exception.Message, exception.Code, task?.VersionNo);
        }
        catch (BusinessException exception)
        {
            var status = exception.Code == ErrorCodes.ApplicationDevelopmentPageRevisionConflict
                ? ProjectManagementTaskBatchResultStatuses.Conflict
                : ProjectManagementTaskBatchResultStatuses.Failed;
            return new ProjectManagementTaskBatchItemResult(item.TaskId, task?.TaskCode, status, exception.Message, exception.Code, task?.VersionNo);
        }
        catch (KeyNotFoundException exception)
        {
            return new ProjectManagementTaskBatchItemResult(item.TaskId, task?.TaskCode, ProjectManagementTaskBatchResultStatuses.Skipped, exception.Message, null, task?.VersionNo);
        }
    }

    private void EnsurePermission(string operation)
    {
        var permission = string.Equals(operation, ProjectManagementTaskBatchOperations.Delete, StringComparison.OrdinalIgnoreCase)
            ? PermissionCodes.ProjectManagementTaskDelete
            : PermissionCodes.ProjectManagementTaskEdit;
        if (!currentUser.HasAsterErpPermission(permission))
            throw new ValidationException("没有执行批量任务操作的权限", ErrorCodes.PermissionDenied);
    }
}
