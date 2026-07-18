using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using SqlSugar;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementTaskParticipantService
{
    Task<IReadOnlyList<ProjectManagementTaskParticipantResponse>> QueryAsync(string taskId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectManagementTaskParticipantResponse>> QueryHistoryAsync(string taskId, CancellationToken cancellationToken = default);
    Task<GridPageResult<ProjectManagementTaskParticipantCandidateResponse>> QueryCandidatesAsync(string taskId, ProjectManagementTaskParticipantCandidateQuery query, CancellationToken cancellationToken = default);
    /// <summary>
    /// 批量协作入口：调用方必须已在 <paramref name="db"/> 上开启外层事务。
    /// 本方法只执行参与人关系、成员范围校验和活动写入，不会提交或回滚该事务。
    /// </summary>
    Task<ProjectManagementTaskParticipantBatchMutationResult> ReplaceParticipantsForTasksAsync(ISqlSugarClient db, ProjectManagementTaskParticipantBatchReplaceRequest request, CancellationToken cancellationToken = default);
    /// <summary>
    /// 只能在包含 <see cref="ReplaceParticipantsForTasksAsync"/> 的外层事务成功提交后调用。
    /// </summary>
    Task PublishCommittedBatchMutationAsync(ProjectManagementTaskParticipantBatchMutationResult result, CancellationToken cancellationToken = default);
    Task<ProjectManagementTaskParticipantResponse> AddAsync(string taskId, ProjectManagementTaskParticipantUpsertRequest request, CancellationToken cancellationToken = default);
    Task RemoveAsync(string taskId, string id, long versionNo, CancellationToken cancellationToken = default);
}
