namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// 将任务树的叶子进度投影到项目和里程碑。
/// 调用方必须在同一业务事务中调用，确保任务、投影、活动和同步日志要么全部提交，要么全部回滚。
/// </summary>
public interface IProjectManagementTaskProgressProjector
{
    Task RefreshAsync(string projectId, CancellationToken cancellationToken = default);
}
