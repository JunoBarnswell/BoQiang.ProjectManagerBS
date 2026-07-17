using AsterERP.Workflow.Approval.Api.Models.Workflow;

namespace AsterERP.Workflow.Approval.Core.Repositories.Workflow;

public interface ICommentInfoRepository : IRepository<CommentInfo>
{
    Task<List<CommentInfo>> GetCommentInfosByProcessInstanceIdAsync(string processInstanceId, CancellationToken cancellationToken = default);
}
