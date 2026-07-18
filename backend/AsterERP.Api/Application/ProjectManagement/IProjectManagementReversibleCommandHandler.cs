using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// 反向命令必须回到各自业务服务的授权、状态机和乐观并发入口；禁止直接更新业务表。
/// </summary>
public interface IProjectManagementReversibleCommandHandler
{
    bool CanHandle(string commandType);

    Task<ProjectManagementReversibleCommandReplayResult> ReplayAsync(
        ProjectManagementReversibleCommandReplayRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ProjectManagementReversibleCommandReplayRequest(
    string CommandId,
    string ExecutionId,
    string Direction,
    string CommandType,
    string ProjectId,
    string AggregateType,
    string AggregateId,
    string CommandJson,
    string ForwardCommandJson,
    string InverseCommandJson,
    string TraceId);

public sealed record ProjectManagementReversibleCommandReplayResult(
    string ProjectId,
    string AggregateType,
    string AggregateId,
    long? VersionNo,
    string? Summary = null,
    /// <summary>成功重放后供下一次 redo 使用的正向命令载荷；应包含新的乐观并发版本。</summary>
    string? NextForwardCommandJson = null,
    /// <summary>成功重放后供下一次 undo 使用的反向命令载荷；应包含新的乐观并发版本。</summary>
    string? NextInverseCommandJson = null);
