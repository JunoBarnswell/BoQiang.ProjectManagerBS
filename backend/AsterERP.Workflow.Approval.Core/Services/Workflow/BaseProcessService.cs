using AsterERP.Workflow.Approval.Api.Enums.Workflow.Runtime;
using AsterERP.Workflow.Approval.Api.Exceptions;
using AsterERP.Workflow.Approval.Api.Models.Workflow;
using AsterERP.Workflow.Approval.Api.ViewModels.Workflow;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.Extensions.Caching.Memory;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Approval.Core.Services.Workflow;

public abstract class BaseProcessService
{
    protected readonly ICommentInfoService CommentInfoService;
    protected readonly IExtendHisprocinstService ExtendHisprocinstService;
    protected readonly IMemoryCache Cache;
    private readonly IClock _clock;

    protected BaseProcessService(
        ICommentInfoService commentInfoService,
        IExtendHisprocinstService extendHisprocinstService,
        IMemoryCache cache,
        IClock clock)
    {
        CommentInfoService = commentInfoService;
        ExtendHisprocinstService = extendHisprocinstService;
        Cache = cache;
        _clock = clock;
    }

    protected async Task AddFlowCommentInfoAsync(CommentInfo commentInfo, CancellationToken cancellationToken = default)
    {
        await CommentInfoService.SaveCommentAsync(commentInfo, cancellationToken);
    }

    protected async Task AddFlowCommentInfoAndProcessStatusAsync(BaseProcessVo baseProcessVo, CancellationToken cancellationToken = default)
    {
        Cache.Set(baseProcessVo.ProcessInstanceId, baseProcessVo.ProcessStatusEnum.ToString());

        var commentInfo = new CommentInfo
        {
            Type = baseProcessVo.CommentTypeEnum.ToString(),
            PersonalCode = baseProcessVo.UserCode,
            ProcessInstanceId = baseProcessVo.ProcessInstanceId,
            Message = baseProcessVo.Message,
            TaskId = baseProcessVo.TaskId,
            ActivityId = baseProcessVo.ActivityId,
            ActivityName = baseProcessVo.ActivityName,
            Creator = baseProcessVo.UserCode,
            Updator = baseProcessVo.UserCode,
            CreateTime = _clock.Now,
            UpdateTime = _clock.Now
        };

        await CommentInfoService.SaveCommentAsync(commentInfo, cancellationToken);

        if (baseProcessVo.CommentTypeEnum != CommentTypeEnum.YY)
        {
            if (string.IsNullOrWhiteSpace(baseProcessVo.ProcessInstanceId))
            {
                throw new ValidationException("请传入流程实例id", ErrorCodes.ParameterInvalid);
            }

            var extendHisprocinst = new ExtendHisprocinst
            {
                ProcessInstanceId = baseProcessVo.ProcessInstanceId,
                ProcessStatus = baseProcessVo.ProcessStatusEnum.ToString()
            };

            await ExtendHisprocinstService.UpdateAllStatusByProcessInstanceIdAsync(extendHisprocinst, cancellationToken);
        }
    }

    protected void EvictHighLightedNodeCache(string processInstanceId)
    {
        Cache.Remove(processInstanceId);
    }

    protected void EvictOneActivityVoCache(string processInstanceId, string activityId)
    {
        Cache.Remove($"{processInstanceId}-{activityId}");
    }
}
