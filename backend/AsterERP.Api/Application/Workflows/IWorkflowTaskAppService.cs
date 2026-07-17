using AsterERP.Contracts.Workflows;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Workflows;

public interface IWorkflowTaskAppService
{
    Task<WorkflowTaskSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken = default);

    Task<GridPageResult<WorkflowTaskListItemResponse>> GetTodoAsync(GridQuery query, CancellationToken cancellationToken = default);

    Task<GridPageResult<WorkflowHistoricTaskResponse>> GetDoneAsync(GridQuery query, CancellationToken cancellationToken = default);

    Task<GridPageResult<WorkflowInstanceListItemResponse>> GetMineAsync(GridQuery query, CancellationToken cancellationToken = default);

    Task<GridPageResult<WorkflowTaskListItemResponse>> GetDelegatedAsync(GridQuery query, CancellationToken cancellationToken = default);

    Task<GridPageResult<WorkflowTaskListItemResponse>> GetTimeoutAsync(GridQuery query, CancellationToken cancellationToken = default);

    Task<GridPageResult<WorkflowInstanceListItemResponse>> GetCcAsync(GridQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowTaskListItemResponse>> GetByProcessInstanceAsync(string processInstanceId, CancellationToken cancellationToken = default);

    Task<WorkflowTaskDetailResponse> GetDetailAsync(string taskId, CancellationToken cancellationToken = default);

    Task ClaimAsync(string taskId, WorkflowTaskActionRequest request, CancellationToken cancellationToken = default);

    Task UnclaimAsync(string taskId, CancellationToken cancellationToken = default);

    Task CompleteAsync(string taskId, WorkflowTaskActionRequest request, CancellationToken cancellationToken = default);

    Task RejectAsync(string taskId, WorkflowTaskActionRequest request, CancellationToken cancellationToken = default);

    Task ReturnAsync(string taskId, WorkflowTaskActionRequest request, CancellationToken cancellationToken = default);

    Task TransferAsync(string taskId, WorkflowTaskActionRequest request, CancellationToken cancellationToken = default);

    Task DelegateAsync(string taskId, WorkflowTaskActionRequest request, CancellationToken cancellationToken = default);

    Task ResolveAsync(string taskId, WorkflowTaskActionRequest request, CancellationToken cancellationToken = default);

    Task SetOwnerAsync(string taskId, WorkflowTaskActionRequest request, CancellationToken cancellationToken = default);

    Task<WorkflowTaskListItemResponse> AddSignAsync(string taskId, WorkflowTaskActionRequest request, CancellationToken cancellationToken = default);

    Task AddIdentityLinkAsync(string taskId, WorkflowIdentityLinkRequest request, CancellationToken cancellationToken = default);

    Task DeleteIdentityLinkAsync(string taskId, WorkflowIdentityLinkRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowIdentityLinkResponse>> GetIdentityLinksAsync(string taskId, CancellationToken cancellationToken = default);

    Task<WorkflowCommentResponse> AddCommentAsync(string taskId, WorkflowCommentRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowCommentResponse>> GetCommentsAsync(string taskId, CancellationToken cancellationToken = default);

    Task<WorkflowAttachmentResponse> AddAttachmentAsync(string taskId, WorkflowAttachmentRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowAttachmentResponse>> GetAttachmentsAsync(string taskId, CancellationToken cancellationToken = default);

    Task<(WorkflowAttachmentResponse Metadata, byte[] Content)> DownloadAttachmentAsync(string attachmentId, CancellationToken cancellationToken = default);
}
