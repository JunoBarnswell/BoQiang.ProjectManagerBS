using AsterERP.Workflow.Core.Delegate;
using AsterERP.Workflow.Approval.Core.Services.Hr;
using Microsoft.Extensions.Logging;

namespace AsterERP.Workflow.Approval.Core.Listeners.Leave;

public class SendMessageExecutionCallListener : IExecutionListener
{
    private readonly ILeaveService _leaveService;
    private readonly ILogger<SendMessageExecutionCallListener> _logger;

    public SendMessageExecutionCallListener(ILeaveService leaveService, ILogger<SendMessageExecutionCallListener> logger)
    {
        _leaveService = leaveService;
        _logger = logger;
    }

    public async System.Threading.Tasks.Task NotifyAsync(IDelegateExecution execution, CancellationToken cancellationToken = default)
    {
        var userCodeList = new List<string>();

        if (execution.Variables.TryGetValue("userCodes", out var userCodesObj)
            && userCodesObj is string userCodesStr
            && !string.IsNullOrWhiteSpace(userCodesStr))
        {
            foreach (var userId in userCodesStr.Split(','))
            {
                if (!string.IsNullOrWhiteSpace(userId))
                    userCodeList.Add(userId.Trim());
            }
        }

        if (userCodeList.Count > 0)
        {
            try
            {
                await _leaveService.SendMessageAsync(userCodeList, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendMessageExecutionCallListener 发送消息失败，userCodes={UserCodes}",
                    string.Join(",", userCodeList));
            }
        }
        else
        {
            _logger.LogWarning("SendMessageExecutionCallListener: 流程变量 userCodes 为空，跳过消息发送");
        }
    }
}
