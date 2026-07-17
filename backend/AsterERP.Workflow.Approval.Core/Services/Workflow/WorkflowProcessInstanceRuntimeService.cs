using System.Text.Json.Nodes;
using AsterERP.Workflow.Api.Process.Payload;
using AsterERP.Workflow.Api.Process.Runtime;
using AsterERP.Workflow.Approval.Api.Constants;
using AsterERP.Workflow.Approval.Api.Enums.Workflow.Runtime;
using AsterERP.Workflow.Approval.Api.Models.Workflow;
using AsterERP.Workflow.Approval.Api.Models.Org;
using AsterERP.Workflow.Approval.Api.ViewModels.Workflow.ProcessInstance;
using AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Runtime;
using AsterERP.Workflow.Approval.Core.Repositories.Workflow;
using AsterERP.Workflow.Approval.Core.Services.Org;
using AsterERP.Workflow.Tools.Common;
using AsterERP.Workflow.Tools.Pager;
using AsterERP.Workflow.Tools.Vos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Approval.Core.Services.Workflow;

public class WorkflowProcessInstanceRuntimeService : BaseProcessService, IWorkflowProcessInstanceRuntimeService
{
    private readonly IWorkflowProcessInstanceRepository _workflowProcessInstanceRepository;
    private readonly IPersonalService _personalService;
    private readonly IBpmnModelService _bpmnModelService;
    private readonly IWorkflowActivityInstanceService _workflowActivityInstanceService;
    private readonly IExtendProcinstService _extendProcinstService;
    private readonly IModelInfoService _modelInfoService;
    private readonly IProcessRuntime _processRuntime;
    private readonly ILogger<WorkflowProcessInstanceRuntimeService> _logger;
    private readonly IClock _clock;

    public WorkflowProcessInstanceRuntimeService(
        IWorkflowProcessInstanceRepository workflowProcessInstanceRepository,
        IPersonalService personalService,
        IBpmnModelService bpmnModelService,
        IWorkflowActivityInstanceService workflowActivityInstanceService,
        IExtendProcinstService extendProcinstService,
        IExtendHisprocinstService extendHisprocinstService,
        IModelInfoService modelInfoService,
        ICommentInfoService commentInfoService,
        IProcessRuntime processRuntime,
        IMemoryCache cache,
        IClock clock,
        ILogger<WorkflowProcessInstanceRuntimeService> logger)
        : base(commentInfoService, extendHisprocinstService, cache, clock)
    {
        _workflowProcessInstanceRepository = workflowProcessInstanceRepository;
        _personalService = personalService;
        _bpmnModelService = bpmnModelService;
        _workflowActivityInstanceService = workflowActivityInstanceService;
        _extendProcinstService = extendProcinstService;
        _modelInfoService = modelInfoService;
        _processRuntime = processRuntime;
        _logger = logger;
        _clock = clock;
    }

    public async Task<ReturnVo<ProcessInstancePayload>> StartProcessInstanceByKeyAsync(StartProcessInstanceVo @params, CancellationToken cancellationToken = default)
    {
        var returnVo = new ReturnVo<ProcessInstancePayload>(ReturnCode.FAIL, "发起流程失败");

        if (string.IsNullOrWhiteSpace(@params.ProcessDefinitionKey) ||
            string.IsNullOrWhiteSpace(@params.BusinessKey) ||
            string.IsNullOrWhiteSpace(@params.AppSn))
        {
            return new ReturnVo<ProcessInstancePayload>(ReturnCode.FAIL, "Parameters should not be null");
        }

        try
        {
            var personal = await _personalService.GetPersonalByCodeAsync(@params.CurrentUserCode, cancellationToken);
            if (personal == null)
            {
                return new ReturnVo<ProcessInstancePayload>(ReturnCode.FAIL, $"工号为：{@params.CurrentUserCode}的当前发起人用户匹配不到，请确认!");
            }

            var variables = await GetStartVariablesAsync(@params, personal, cancellationToken);
            var payload = new StartPayload
            {
                ProcessDefinitionKey = @params.ProcessDefinitionKey.Trim(),
                BusinessKey = @params.BusinessKey.Trim(),
                Name = @params.FormName?.Trim(),
                Variables = variables.ToDictionary(kv => kv.Key, kv => (object?)kv.Value)
            };

            var processInstance = await _processRuntime.StartAsync(payload, cancellationToken);

            var extendProcinst = new ExtendProcinst
            {
                CurrentUserCode = @params.CurrentUserCode,
                ProcessInstanceId = processInstance.Id,
                ProcessDefinitionId = processInstance.ProcessDefinitionId,
                ProcessStatus = ProcessStatusEnum.SPZ.ToString(),
                ProcessName = @params.FormName,
                ModelKey = @params.ProcessDefinitionKey,
                BusinessKey = @params.BusinessKey,
                TenantId = @params.AppSn,
                Creator = @params.Creator ?? @params.CurrentUserCode,
                Updator = @params.Creator ?? @params.CurrentUserCode,
                UserInfo = string.Empty,
                FormData = @params.FormData ?? string.Empty,
                Keyword = string.Empty,
                DelFlag = 1,
                CreateTime = _clock.Now,
                UpdateTime = _clock.Now
            };

            await _extendProcinstService.SaveExtendProcinstAndHisAsync(extendProcinst, cancellationToken);
            await _workflowActivityInstanceService.SyncRuntimeTasksAsync(processInstance.Id, cancellationToken);

            returnVo.Code = ReturnCode.SUCCESS;
            returnVo.Msg = "OK";
            returnVo.Data = processInstance;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "StartProcessInstanceByKey failed");
            returnVo.Msg = $"发起流程失败，原因：{e.Message}";
        }
        return returnVo;
    }

    public Task<Dictionary<string, object>> GetStartVariablesAsync(StartProcessInstanceVo @params, Personal personal, CancellationToken cancellationToken = default)
    {
        if (@params.Variables == null)
        {
            @params.Variables = new Dictionary<string, object>();
        }

        if (!@params.Variables.ContainsKey(StartVariableEnum.Form.GetCode()))
        {
            if (!string.IsNullOrWhiteSpace(@params.FormData))
            {
                try
                {
                    var formNode = JsonNode.Parse(@params.FormData);
                    @params.Variables[StartVariableEnum.Form.GetCode()] = formNode!;
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "转化json出错");
                }
            }
        }

        if (string.IsNullOrWhiteSpace(@params.DeptId))
        {
            @params.DeptId = personal.DeptId;
        }

        @params.Variables[WorkflowApprovalConstants.FlowStarterCodeVar] = @params.CurrentUserCode;
        @params.Variables[WorkflowApprovalConstants.FlowSubmitterVar] = "";
        @params.Variables[WorkflowApprovalConstants.WorkflowSkipExpressionEnabled] = true;

        return Task.FromResult(@params.Variables);
    }

    public async Task<StartorBaseInfoVo?> GetStartorBaseInfoVoByProcessInstanceIdAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        var extendHisprocinst = await ExtendHisprocinstService.FindExtendHisprocinstByProcessInstanceIdAsync(processInstanceId, cancellationToken);
        if (extendHisprocinst == null) return null;

        var modelInfo = await _modelInfoService.GetModelInfoByModelKeyAsync(extendHisprocinst.ModelKey, cancellationToken);
        return new StartorBaseInfoVo
        {
            ProcessInstanceId = processInstanceId,
            BusinessKey = extendHisprocinst.BusinessKey,
            FormName = extendHisprocinst.ProcessName,
            ModelKey = extendHisprocinst.ModelKey,
            ModelName = modelInfo?.Name,
            CreateTime = extendHisprocinst.CreateTime
        };
    }

    public async Task<PagerModel<ProcessInstanceVo>> FindMyProcessinstancesPagerModelAsync(InstanceQueryParamsVo paramsVo, int pageNum, int pageSize, CancellationToken cancellationToken = default)
    {
        var result = await _workflowProcessInstanceRepository.FindMyProcessinstancesPagerModelAsync(paramsVo, pageNum, pageSize, cancellationToken);
        return new PagerModel<ProcessInstanceVo>(result.Value.TotalElements, result.Value.Content?.ToList() ?? new List<ProcessInstanceVo>());
    }

    public async Task<ReturnVo<string>> StopProcessAsync(EndVo endVo, CancellationToken cancellationToken = default)
    {
        var returnVo = new ReturnVo<string>(ReturnCode.SUCCESS, "流程已拒绝！");
        endVo.CommentTypeEnum = CommentTypeEnum.LCZZ;
        endVo.ProcessStatusEnum = ProcessStatusEnum.ZZ;
        await AddFlowCommentInfoAndProcessStatusAsync(endVo, cancellationToken);
        return returnVo;
    }
}
