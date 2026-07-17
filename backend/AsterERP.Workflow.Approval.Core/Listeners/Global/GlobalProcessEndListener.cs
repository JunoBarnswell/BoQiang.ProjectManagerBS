using AsterERP.Workflow.Core.Delegate;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Approval.Api.Enums.Workflow.Runtime;
using AsterERP.Workflow.Approval.Api.Models.Workflow;
using AsterERP.Workflow.Approval.Core.Services.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AsterERP.Workflow.Approval.Core.Listeners.Global;

public class GlobalProcessEndListener : IWorkflowEventListener
{
    private readonly ILogger<GlobalProcessEndListener> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public GlobalProcessEndListener(
        ILogger<GlobalProcessEndListener> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public bool IsFailOnException => true;

    public void OnEvent(IWorkflowEvent @event)
    {
        _ = ExecuteLegacyAsync(@event);
    }

    public async global::System.Threading.Tasks.Task OnEventAsync(
        IWorkflowEvent @event,
        CancellationToken cancellationToken = default)
    {
        var processInstanceId = @event.ProcessInstanceId;
        if (string.IsNullOrWhiteSpace(processInstanceId))
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;
        var extendHisprocinstService = provider.GetRequiredService<IExtendHisprocinstService>();
        var extendProcinstService = provider.GetRequiredService<IExtendProcinstService>();
        var commentInfoService = provider.GetRequiredService<ICommentInfoService>();

        await UpdateExtendInfoToHisAsync(
            processInstanceId,
            extendHisprocinstService,
            extendProcinstService,
            commentInfoService,
            cancellationToken);
    }

    private async global::System.Threading.Tasks.Task ExecuteLegacyAsync(IWorkflowEvent @event)
    {
        try
        {
            await OnEventAsync(@event);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process legacy workflow event {EventType}", @event.Type);
        }
    }

    private static async global::System.Threading.Tasks.Task UpdateExtendInfoToHisAsync(
        string processInstanceId,
        IExtendHisprocinstService extendHisprocinstService,
        IExtendProcinstService extendProcinstService,
        ICommentInfoService commentInfoService,
        CancellationToken cancellationToken)
    {
        var extendHisProcinst = await extendHisprocinstService
            .FindExtendHisprocinstByProcessInstanceIdAsync(processInstanceId, cancellationToken);

        if (extendHisProcinst != null && ProcessStatusEnum.ZZ.ToString() != extendHisProcinst.ProcessStatus)
        {
            var updateEntity = new ExtendHisprocinst(processInstanceId, ProcessStatusEnum.BJ.ToString());
            await extendHisprocinstService.UpdateAllStatusByProcessInstanceIdAsync(updateEntity, cancellationToken);
        }

        var commentInfo = new CommentInfo
        {
            ProcessInstanceId = processInstanceId,
            Type = CommentTypeEnum.SPJS.ToString(),
            Message = CommentTypeEnumExtensions.GetEnumMsgByType(CommentTypeEnum.SPJS.ToString()),
            PersonalCode = "system"
        };
        await commentInfoService.SaveCommentAsync(commentInfo, cancellationToken);

        await extendProcinstService.DeleteExtendProcinstByProcessInstanceIdAsync(processInstanceId, cancellationToken);
    }
}
