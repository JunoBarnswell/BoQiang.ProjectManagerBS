using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementReversibleCommandService
{
    Task<ProjectManagementReversibleCommandStackResponse> GetStackAsync(CancellationToken cancellationToken = default);
    Task<ProjectManagementReversibleCommandResponse> UndoAsync(ProjectManagementReversibleCommandExecuteRequest request, CancellationToken cancellationToken = default);
    Task<ProjectManagementReversibleCommandResponse> RedoAsync(ProjectManagementReversibleCommandExecuteRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// 仅供业务服务在自身事务成功提交后调用。登记失败不得回滚已提交的业务变更。
/// </summary>
public interface IProjectManagementReversibleCommandWriter
{
    Task TryRecordCommittedAsync(
        ProjectManagementReversibleCommandCapability capability,
        ProjectManagementReversibleCommandRecordRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>阻止控制器或外部调用方伪造撤销历史的 capability。</summary>
public sealed class ProjectManagementReversibleCommandCapability
{
    private ProjectManagementReversibleCommandCapability() { }
    public static ProjectManagementReversibleCommandCapability Instance { get; } = new();
}
