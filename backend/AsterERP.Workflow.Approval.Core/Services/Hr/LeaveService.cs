using AsterERP.Workflow.Approval.Api.Models.Hr;
using AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Runtime;
using AsterERP.Workflow.Approval.Core.Repositories.Hr;
using AsterERP.Workflow.Approval.Core.Services.Workflow;
using AsterERP.Workflow.Tools.Common;
using Microsoft.Extensions.Logging;
using Volo.Abp.Guids;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Approval.Core.Services.Hr;

public class LeaveService : ILeaveService
{
    private readonly ILeaveRepository _leaveRepository;
    private readonly IWorkflowProcessInstanceRuntimeService _workflowProcessInstanceRuntimeService;
    private readonly ILogger<LeaveService> _logger;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guidGenerator;

    public LeaveService(
        ILeaveRepository leaveRepository,
        IWorkflowProcessInstanceRuntimeService workflowProcessInstanceRuntimeService,
        ILogger<LeaveService> logger,
        IClock clock,
        IGuidGenerator guidGenerator)
    {
        _leaveRepository = leaveRepository;
        _workflowProcessInstanceRuntimeService = workflowProcessInstanceRuntimeService;
        _logger = logger;
        _clock = clock;
        _guidGenerator = guidGenerator;
    }

    public async Task SendMessageAsync(List<string> userCodes, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("发送消息给用户: {UserCodes}", string.Join(",", userCodes));
    }

    public async Task SaveLeaveAsync(Leave leave, CancellationToken cancellationToken = default)
    {
        var businessKey = _guidGenerator.Create().ToString("N");
        var startProcessInstanceVo = new StartProcessInstanceVo
        {
            BusinessKey = businessKey,
            CurrentUserCode = leave.ApplyerCode,
            Creator = leave.ApplyerCode,
            AppSn = "flow",
            FormName = leave.Title,
            ProcessDefinitionKey = "test_leave"
        };
        var processInstanceReturnVo = await _workflowProcessInstanceRuntimeService.StartProcessInstanceByKeyAsync(startProcessInstanceVo, cancellationToken);
        if (processInstanceReturnVo.Code == ReturnCode.SUCCESS && processInstanceReturnVo.Data != null)
        {
            leave.Id = businessKey;
            leave.ProcessInstanceId = processInstanceReturnVo.Data.Id;
            leave.CreateTime = _clock.Now;
            await _leaveRepository.InsertAsync(leave, cancellationToken);
        }
    }

    public async Task<Leave?> GetLeaveByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _leaveRepository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<Leave?> GetLeaveByProcessInstanceIdAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        return await _leaveRepository.Db.Queryable<Leave>()
            .FirstAsync(l => l.ProcessInstanceId == processInstanceId && l.DelFlag == 1, cancellationToken);
    }
}
