using AsterERP.Workflow.Approval.Api.Models.Workflow;
using AsterERP.Workflow.Approval.Core.Repositories.Workflow;
using Volo.Abp.Guids;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Approval.Core.Services.Workflow;

public class CommentInfoService : ICommentInfoService
{
    private readonly ICommentInfoRepository _commentInfoRepository;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guidGenerator;

    public CommentInfoService(
        ICommentInfoRepository commentInfoRepository,
        IClock clock,
        IGuidGenerator guidGenerator)
    {
        _commentInfoRepository = commentInfoRepository;
        _clock = clock;
        _guidGenerator = guidGenerator;
    }

    public async Task SaveCommentAsync(CommentInfo commentInfo, CancellationToken cancellationToken = default)
    {
        var isNew = string.IsNullOrWhiteSpace(commentInfo.Id);
        commentInfo.Id = isNew ? _guidGenerator.Create().ToString("N") : commentInfo.Id;
        commentInfo.Type ??= string.Empty;
        commentInfo.PersonalCode ??= string.Empty;
        commentInfo.TaskId ??= string.Empty;
        commentInfo.ActivityId ??= string.Empty;
        commentInfo.ActivityName ??= string.Empty;
        commentInfo.ProcessInstanceId ??= string.Empty;
        commentInfo.Action ??= string.Empty;
        commentInfo.Message ??= string.Empty;
        commentInfo.Creator ??= string.Empty;
        commentInfo.Updator ??= string.Empty;
        commentInfo.CreateTime ??= _clock.Now;
        commentInfo.UpdateTime ??= _clock.Now;
        commentInfo.Time ??= _clock.Now;
        commentInfo.Keyword ??= string.Empty;
        commentInfo.DelFlag ??= 1;
        if (isNew)
        {
            await _commentInfoRepository.InsertAsync(commentInfo, cancellationToken);
        }
        else
        {
            await _commentInfoRepository.UpdateAsync(commentInfo, cancellationToken);
        }
    }
}
