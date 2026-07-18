using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// 用户级撤销/重做账本。该服务只管理命令顺序、幂等和审计；实际业务重放交给 handler 回到原业务内核。
/// </summary>
public sealed class ProjectManagementReversibleCommandService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IServiceProvider serviceProvider,
    IProjectManagementActivityWriter? activityWriter = null,
    ILogger<ProjectManagementReversibleCommandService>? logger = null) : IProjectManagementReversibleCommandService, IProjectManagementReversibleCommandWriter
{
    private const int RetentionLimit = 50;
    private const int MaxCommandPayloadBytes = 64 * 1024;
    private static readonly TimeSpan ReplayLease = TimeSpan.FromMinutes(5);

    public async Task TryRecordCommittedAsync(
        ProjectManagementReversibleCommandCapability capability,
        ProjectManagementReversibleCommandRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ReferenceEquals(capability, ProjectManagementReversibleCommandCapability.Instance))
            throw new InvalidOperationException("可逆命令登记缺少内部 capability");

        try
        {
            await RecordCommittedAsync(request, cancellationToken);
        }
        catch (Exception exception)
        {
            // 业务事务已经提交，账本登记不允许把已完成的业务命令变成客户端失败。
            logger?.LogError(exception, "项目管理可逆命令登记失败。OriginRequestId: {OriginRequestId}", request.OriginRequestId);
        }
    }

    public async Task<ProjectManagementReversibleCommandStackResponse> GetStackAsync(CancellationToken cancellationToken = default)
    {
        RequireSystemWorkspace();
        var rows = await ActiveCommandsQuery()
            .OrderBy(item => item.SequenceNo, OrderByType.Desc)
            .OrderBy(item => item.Id, OrderByType.Desc)
            .ToListAsync(cancellationToken);
        return new ProjectManagementReversibleCommandStackResponse(
            rows.Select(Map).ToList(),
            rows.Any(item => item.State == ProjectManagementReversibleCommandStates.Applied),
            rows.Any(item => item.State == ProjectManagementReversibleCommandStates.Undone));
    }

    public Task<ProjectManagementReversibleCommandResponse> UndoAsync(ProjectManagementReversibleCommandExecuteRequest request, CancellationToken cancellationToken = default) =>
        ReplayAsync(ProjectManagementReversibleCommandDirections.Undo, request, cancellationToken);

    public Task<ProjectManagementReversibleCommandResponse> RedoAsync(ProjectManagementReversibleCommandExecuteRequest request, CancellationToken cancellationToken = default) =>
        ReplayAsync(ProjectManagementReversibleCommandDirections.Redo, request, cancellationToken);

    private async Task RecordCommittedAsync(ProjectManagementReversibleCommandRecordRequest request, CancellationToken cancellationToken)
    {
        RequireSystemWorkspace();
        ValidateRecord(request);
        var db = databaseAccessor.GetProjectManagementDb();
        var existing = await FindByOriginRequestIdAsync(request.OriginRequestId, cancellationToken);
        if (existing is not null) return;

        var now = DateTime.UtcNow;
        try
        {
            await ProjectManagementMutationTransaction.RunAsync(db, async () =>
            {
                existing = await FindByOriginRequestIdAsync(request.OriginRequestId, cancellationToken);
                if (existing is not null) return;

                var commands = await ActiveCommandsQuery()
                    .OrderBy(item => item.SequenceNo, OrderByType.Asc)
                    .ToListAsync(cancellationToken);
                var nextSequence = commands.Count == 0 ? 1 : commands[^1].SequenceNo + 1;
                var redoCommands = commands.Where(item => item.State == ProjectManagementReversibleCommandStates.Undone).ToList();
                if (redoCommands.Count > 0)
                {
                    foreach (var command in redoCommands)
                    {
                        command.State = ProjectManagementReversibleCommandStates.Invalidated;
                        command.VersionNo++;
                        command.UpdatedBy = UserId();
                        command.UpdatedTime = now;
                    }
                    await db.Updateable(redoCommands)
                        .UpdateColumns(item => new { item.State, item.VersionNo, item.UpdatedBy, item.UpdatedTime })
                        .ExecuteCommandAsync(cancellationToken);
                }

                var entity = new ProjectManagementReversibleCommandEntity
                {
                    Id = Guid.NewGuid().ToString("N"),
                    TenantId = Tenant(),
                    AppCode = App(),
                    ActorUserId = UserId(),
                    OriginRequestId = request.OriginRequestId.Trim(),
                    SequenceNo = nextSequence,
                    CommandType = request.CommandType.Trim(),
                    ProjectId = request.ProjectId.Trim(),
                    AggregateType = request.AggregateType.Trim(),
                    AggregateId = request.AggregateId.Trim(),
                    State = ProjectManagementReversibleCommandStates.Applied,
                    ForwardCommandJson = request.ForwardCommandJson.Trim(),
                    InverseCommandJson = request.InverseCommandJson.Trim(),
                    TraceId = request.TraceId.Trim(),
                    Summary = NormalizeOptional(request.Summary, 1_000),
                    VersionNo = 1,
                    CreatedBy = UserId(),
                    CreatedTime = now
                };
                await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);

                var retained = commands.Where(item => item.State == ProjectManagementReversibleCommandStates.Applied).Append(entity)
                    .OrderBy(item => item.SequenceNo).ToList();
                var overflow = retained.Take(Math.Max(0, retained.Count - RetentionLimit)).ToList();
                if (overflow.Count > 0)
                {
                    foreach (var command in overflow)
                    {
                        command.State = ProjectManagementReversibleCommandStates.Invalidated;
                        command.VersionNo++;
                        command.UpdatedBy = UserId();
                        command.UpdatedTime = now;
                    }
                    await db.Updateable(overflow)
                        .UpdateColumns(item => new { item.State, item.VersionNo, item.UpdatedBy, item.UpdatedTime })
                        .ExecuteCommandAsync(cancellationToken);
                }
            });
        }
        catch (Exception exception) when (IsDuplicateOriginRequest(exception))
        {
            // 并发的同一业务请求只保留一条账本记录。
            if (await FindByOriginRequestIdAsync(request.OriginRequestId, cancellationToken) is null) throw;
        }
    }

    private async Task<ProjectManagementReversibleCommandResponse> ReplayAsync(
        string direction,
        ProjectManagementReversibleCommandExecuteRequest request,
        CancellationToken cancellationToken)
    {
        RequireSystemWorkspace();
        var requestId = Required(request.RequestId, "撤销/重做请求标识不能为空", 128);
        var targetState = direction == ProjectManagementReversibleCommandDirections.Undo
            ? ProjectManagementReversibleCommandStates.Undone
            : ProjectManagementReversibleCommandStates.Applied;

        var duplicate = await FindCompletedRequestAsync(direction, requestId, targetState, cancellationToken);
        if (duplicate is not null) return Map(duplicate);

        var sourceState = direction == ProjectManagementReversibleCommandDirections.Undo
            ? ProjectManagementReversibleCommandStates.Applied
            : ProjectManagementReversibleCommandStates.Undone;
        var command = await FindTopCommandAsync(sourceState, direction, cancellationToken)
            ?? throw new ValidationException(direction == ProjectManagementReversibleCommandDirections.Undo ? "没有可撤销的业务命令" : "没有可重做的业务命令");
        EnsureSupported(command.CommandType);
        var handler = serviceProvider.GetServices<IProjectManagementReversibleCommandHandler>().FirstOrDefault(item => item.CanHandle(command.CommandType))
            ?? throw new ValidationException("该业务命令当前没有可用的反向命令处理器");

        var claim = await ClaimAsync(command, direction, requestId, cancellationToken);
        if (claim.Status == ReplayClaimStatus.Completed) return Map(claim.Command);
        if (claim.Status == ReplayClaimStatus.InProgress) return Map(claim.Command);
        command = claim.Command;

        var commandJson = direction == ProjectManagementReversibleCommandDirections.Undo
            ? command.InverseCommandJson
            : command.ForwardCommandJson;
        var executionId = command.ActiveReplayExecutionId!;
        try
        {
            using var replayScope = ProjectManagementReversibleCommandReplayScope.Enter();
            var result = await handler.ReplayAsync(new ProjectManagementReversibleCommandReplayRequest(
                command.Id, executionId, direction, command.CommandType, command.ProjectId, command.AggregateType, command.AggregateId,
                commandJson, command.ForwardCommandJson, command.InverseCommandJson, command.TraceId), cancellationToken);
            EnsureReplayResult(command, result);
            var completed = await CompleteAsync(command, direction, requestId, result, cancellationToken);
            await TryWriteActivityAsync(completed, direction, result, cancellationToken);
            return Map(completed);
        }
        catch
        {
            await ReleaseClaimAsync(command, cancellationToken);
            throw;
        }
    }

    private async Task<ReplayClaim> ClaimAsync(ProjectManagementReversibleCommandEntity command, string direction, string requestId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var targetState = direction == ProjectManagementReversibleCommandDirections.Undo
            ? ProjectManagementReversibleCommandStates.Undone
            : ProjectManagementReversibleCommandStates.Applied;
        if (command.State == targetState && IsCompletedRequest(command, direction, requestId))
            return new ReplayClaim(ReplayClaimStatus.Completed, command);
        if (!string.IsNullOrWhiteSpace(command.ActiveReplayRequestId) && command.ActiveReplayLeaseExpiresAt > now)
        {
            if (string.Equals(command.ActiveReplayRequestId, requestId, StringComparison.Ordinal) && string.Equals(command.ActiveReplayDirection, direction, StringComparison.Ordinal))
                return new ReplayClaim(ReplayClaimStatus.InProgress, command);
            throw new ValidationException("撤销栈正在处理另一个请求，请稍后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict);
        }

        var sourceVersion = command.VersionNo;
        var resumesSameRequest = string.Equals(command.ActiveReplayRequestId, requestId, StringComparison.Ordinal) &&
            string.Equals(command.ActiveReplayDirection, direction, StringComparison.Ordinal);
        command.ActiveReplayDirection = direction;
        command.ActiveReplayRequestId = requestId;
        command.ActiveReplayExecutionId = resumesSameRequest
            ? command.ActiveReplayExecutionId ?? CreateExecutionId(command.Id, direction, requestId)
            : CreateExecutionId(command.Id, direction, requestId);
        command.ActiveReplayLeaseExpiresAt = now.Add(ReplayLease);
        command.VersionNo++;
        command.UpdatedBy = UserId();
        command.UpdatedTime = now;
        var affected = await databaseAccessor.GetProjectManagementDb().Updateable(command)
            .Where(item => item.Id == command.Id && item.VersionNo == sourceVersion && item.State == command.State)
            .UpdateColumns(item => new
            {
                item.ActiveReplayDirection,
                item.ActiveReplayRequestId,
                item.ActiveReplayExecutionId,
                item.ActiveReplayLeaseExpiresAt,
                item.VersionNo,
                item.UpdatedBy,
                item.UpdatedTime
            })
            .ExecuteCommandAsync(cancellationToken);
        if (affected == 1) return new ReplayClaim(ReplayClaimStatus.Claimed, command);

        var concurrent = await FindByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException("可逆命令不存在", ErrorCodes.PlatformResourceNotFound);
        if (concurrent.State == targetState && IsCompletedRequest(concurrent, direction, requestId))
            return new ReplayClaim(ReplayClaimStatus.Completed, concurrent);
        if (string.Equals(concurrent.ActiveReplayRequestId, requestId, StringComparison.Ordinal) && string.Equals(concurrent.ActiveReplayDirection, direction, StringComparison.Ordinal))
            return new ReplayClaim(ReplayClaimStatus.InProgress, concurrent);
        throw new ValidationException("撤销栈已被其他请求改变，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict);
    }

    private async Task<ProjectManagementReversibleCommandEntity> CompleteAsync(
        ProjectManagementReversibleCommandEntity command,
        string direction,
        string requestId,
        ProjectManagementReversibleCommandReplayResult result,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var sourceVersion = command.VersionNo;
        command.State = direction == ProjectManagementReversibleCommandDirections.Undo
            ? ProjectManagementReversibleCommandStates.Undone
            : ProjectManagementReversibleCommandStates.Applied;
        if (direction == ProjectManagementReversibleCommandDirections.Undo) command.LastUndoRequestId = requestId;
        else command.LastRedoRequestId = requestId;
        command.ActiveReplayDirection = null;
        command.ActiveReplayRequestId = null;
        command.ActiveReplayExecutionId = null;
        command.ActiveReplayLeaseExpiresAt = null;
        if (!string.IsNullOrWhiteSpace(result.NextForwardCommandJson))
        {
            ValidateCommandJson(result.NextForwardCommandJson, "下一次正向命令");
            command.ForwardCommandJson = result.NextForwardCommandJson.Trim();
        }
        if (!string.IsNullOrWhiteSpace(result.NextInverseCommandJson))
        {
            ValidateCommandJson(result.NextInverseCommandJson, "下一次反向命令");
            command.InverseCommandJson = result.NextInverseCommandJson.Trim();
        }
        command.LastReplayedTime = now;
        command.VersionNo++;
        command.UpdatedBy = UserId();
        command.UpdatedTime = now;
        var affected = await databaseAccessor.GetProjectManagementDb().Updateable(command)
            .Where(item => item.Id == command.Id && item.VersionNo == sourceVersion && item.ActiveReplayRequestId == requestId)
            .UpdateColumns(item => new
            {
                item.State,
                item.LastUndoRequestId,
                item.LastRedoRequestId,
                item.ActiveReplayDirection,
                item.ActiveReplayRequestId,
                item.ActiveReplayExecutionId,
                item.ActiveReplayLeaseExpiresAt,
                item.ForwardCommandJson,
                item.InverseCommandJson,
                item.LastReplayedTime,
                item.VersionNo,
                item.UpdatedBy,
                item.UpdatedTime
            })
            .ExecuteCommandAsync(cancellationToken);
        if (affected == 1) return command;
        throw new ValidationException("业务命令已完成，但撤销账本确认失败；请使用相同请求标识重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict);
    }

    private async Task ReleaseClaimAsync(ProjectManagementReversibleCommandEntity command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.ActiveReplayRequestId)) return;
        var sourceVersion = command.VersionNo;
        command.ActiveReplayDirection = null;
        command.ActiveReplayRequestId = null;
        command.ActiveReplayExecutionId = null;
        command.ActiveReplayLeaseExpiresAt = null;
        command.VersionNo++;
        command.UpdatedBy = UserId();
        command.UpdatedTime = DateTime.UtcNow;
        try
        {
            await databaseAccessor.GetProjectManagementDb().Updateable(command)
                .Where(item => item.Id == command.Id && item.VersionNo == sourceVersion)
                .UpdateColumns(item => new
                {
                    item.ActiveReplayDirection,
                    item.ActiveReplayRequestId,
                    item.ActiveReplayExecutionId,
                    item.ActiveReplayLeaseExpiresAt,
                    item.VersionNo,
                    item.UpdatedBy,
                    item.UpdatedTime
                })
                .ExecuteCommandAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger?.LogError(exception, "释放可逆命令重放租约失败。CommandId: {CommandId}", command.Id);
        }
    }

    private async Task TryWriteActivityAsync(ProjectManagementReversibleCommandEntity command, string direction, ProjectManagementReversibleCommandReplayResult result, CancellationToken cancellationToken)
    {
        if (activityWriter is null) return;
        try
        {
            var action = direction == ProjectManagementReversibleCommandDirections.Undo ? "撤销" : "重做";
            await activityWriter.AppendAsync(new ProjectManagementActivityEvent(
                Tenant(), App(), command.AggregateType, command.AggregateId, direction == ProjectManagementReversibleCommandDirections.Undo ? "command.undone" : "command.redone",
                result.Summary ?? $"{action}业务命令 {command.CommandType}",
                Activity.Current?.Id ?? command.TraceId, UserId(), command.ProjectId, "UndoRedo",
                [
                    new ProjectManagementActivityFieldChange("CommandType", "命令类型", command.CommandType, command.CommandType),
                    new ProjectManagementActivityFieldChange("Direction", "操作方向", null, direction),
                    new ProjectManagementActivityFieldChange("TargetVersion", "目标版本", null, result.VersionNo?.ToString())
                ]), cancellationToken);
        }
        catch (Exception exception)
        {
            // 业务命令和账本均已提交；不能把活动流暂时不可用伪装成业务撤销失败。
            logger?.LogError(exception, "可逆命令活动审计写入失败。CommandId: {CommandId}", command.Id);
        }
    }

    private ISugarQueryable<ProjectManagementReversibleCommandEntity> ActiveCommandsQuery() =>
        databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementReversibleCommandEntity>()
            .Where(item => item.TenantId == Tenant() && item.AppCode == App() && item.ActorUserId == UserId() &&
                !item.IsDeleted && item.State != ProjectManagementReversibleCommandStates.Invalidated);

    private async Task<ProjectManagementReversibleCommandEntity?> FindTopCommandAsync(string state, string direction, CancellationToken cancellationToken)
    {
        var query = ActiveCommandsQuery().Where(item => item.State == state);
        query = direction == ProjectManagementReversibleCommandDirections.Undo
            ? query.OrderBy(item => item.SequenceNo, OrderByType.Desc).OrderBy(item => item.Id, OrderByType.Desc)
            : query.OrderBy(item => item.SequenceNo, OrderByType.Asc).OrderBy(item => item.Id, OrderByType.Asc);
        return (await query.Take(1).ToListAsync(cancellationToken)).FirstOrDefault();
    }

    private async Task<ProjectManagementReversibleCommandEntity?> FindCompletedRequestAsync(string direction, string requestId, string targetState, CancellationToken cancellationToken)
    {
        var query = ActiveCommandsQuery().Where(item => item.State == targetState);
        query = direction == ProjectManagementReversibleCommandDirections.Undo
            ? query.Where(item => item.LastUndoRequestId == requestId)
            : query.Where(item => item.LastRedoRequestId == requestId);
        return (await query.Take(1).ToListAsync(cancellationToken)).FirstOrDefault();
    }

    private async Task<ProjectManagementReversibleCommandEntity?> FindByOriginRequestIdAsync(string originRequestId, CancellationToken cancellationToken) =>
        (await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementReversibleCommandEntity>()
            .Where(item => item.TenantId == Tenant() && item.AppCode == App() && item.ActorUserId == UserId() && item.OriginRequestId == originRequestId.Trim() && !item.IsDeleted)
            .Take(1).ToListAsync(cancellationToken)).FirstOrDefault();

    private async Task<ProjectManagementReversibleCommandEntity?> FindByIdAsync(string id, CancellationToken cancellationToken) =>
        (await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementReversibleCommandEntity>()
            .Where(item => item.Id == id && item.TenantId == Tenant() && item.AppCode == App() && item.ActorUserId == UserId() && !item.IsDeleted)
            .Take(1).ToListAsync(cancellationToken)).FirstOrDefault();

    private static void ValidateRecord(ProjectManagementReversibleCommandRecordRequest request)
    {
        Required(request.OriginRequestId, "原始请求标识不能为空", 128);
        EnsureSupported(request.CommandType);
        Required(request.ProjectId, "项目标识不能为空", 64);
        Required(request.AggregateType, "聚合类型不能为空", 64);
        Required(request.AggregateId, "聚合标识不能为空", 64);
        ValidateCommandJson(request.ForwardCommandJson, "正向命令");
        ValidateCommandJson(request.InverseCommandJson, "反向命令");
        Required(request.TraceId, "追踪标识不能为空", 256);
        _ = NormalizeOptional(request.Summary, 1_000);
    }

    private static void ValidateCommandJson(string? value, string name)
    {
        var json = Required(value, $"{name}不能为空", MaxCommandPayloadBytes);
        if (Encoding.UTF8.GetByteCount(json) > MaxCommandPayloadBytes) throw new ValidationException($"{name}超过 {MaxCommandPayloadBytes / 1024}KB 限制");
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new ValidationException($"{name}必须是 JSON 对象");
        }
        catch (JsonException)
        {
            throw new ValidationException($"{name}不是有效 JSON");
        }
    }

    private static void EnsureSupported(string? commandType)
    {
        var normalized = Required(commandType, "可逆命令类型不能为空", 128);
        if (!ProjectManagementReversibleCommandTypes.Supported.Contains(normalized))
            throw new ValidationException("该业务操作不可撤销；永久删除、导入、备份和同步不进入撤销栈");
    }

    private static void EnsureReplayResult(ProjectManagementReversibleCommandEntity command, ProjectManagementReversibleCommandReplayResult result)
    {
        if (!string.Equals(command.ProjectId, result.ProjectId, StringComparison.Ordinal) ||
            !string.Equals(command.AggregateType, result.AggregateType, StringComparison.Ordinal) ||
            !string.Equals(command.AggregateId, result.AggregateId, StringComparison.Ordinal))
            throw new InvalidOperationException("反向命令处理器返回了不匹配的聚合结果");
    }

    private static bool IsCompletedRequest(ProjectManagementReversibleCommandEntity command, string direction, string requestId) =>
        direction == ProjectManagementReversibleCommandDirections.Undo
            ? string.Equals(command.LastUndoRequestId, requestId, StringComparison.Ordinal)
            : string.Equals(command.LastRedoRequestId, requestId, StringComparison.Ordinal);

    private static string CreateExecutionId(string commandId, string direction, string requestId) => $"{commandId}:{direction}:{requestId}";
    private static bool IsDuplicateOriginRequest(Exception exception) => exception.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) || exception.Message.Contains("constraint", StringComparison.OrdinalIgnoreCase);
    private static ProjectManagementReversibleCommandResponse Map(ProjectManagementReversibleCommandEntity entity) => new(entity.Id, entity.SequenceNo, entity.CommandType, entity.ProjectId, entity.AggregateType, entity.AggregateId, entity.State, entity.Summary, entity.TraceId, entity.CreatedTime, entity.LastReplayedTime, !string.IsNullOrWhiteSpace(entity.ActiveReplayRequestId));
    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用", ErrorCodes.PermissionDenied);
    private string UserId() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);
    private void RequireSystemWorkspace() => ProjectManagementPlatformScope.RequireSystemWorkspace(currentUser);
    private static string Required(string? value, string message, int maximum) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException(message) : value.Trim().Length > maximum ? throw new ValidationException($"{message.TrimEnd('。')}长度不能超过 {maximum}") : value.Trim();
    private static string? NormalizeOptional(string? value, int maximum) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length > maximum ? throw new ValidationException($"文本长度不能超过 {maximum}") : value.Trim();

    private enum ReplayClaimStatus { Claimed, InProgress, Completed }
    private sealed record ReplayClaim(ReplayClaimStatus Status, ProjectManagementReversibleCommandEntity Command);
}
