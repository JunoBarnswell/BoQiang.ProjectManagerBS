using AsterERP.Workflow.Approval.Api.Models.Workflow;

namespace AsterERP.Workflow.Approval.Core.Services.Workflow;

public interface ICommentInfoService
{
    Task SaveCommentAsync(CommentInfo commentInfo, CancellationToken cancellationToken = default);
}
