using AsterERP.Workflow.Approval.Api.Models.Base;
using AsterERP.Workflow.Approval.Api.Models.Workflow;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Model;
using AsterERP.Workflow.Approval.Api.ViewModels.Workflow.ProcessInstance;
using AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Runtime;
using AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Task;
using AsterERP.Workflow.Approval.Core.Services.Base;
using AsterERP.Workflow.Approval.Core.Services.Workflow;
using AsterERP.Workflow.Approval.Core.Repositories.Workflow;
using AsterERP.Workflow.Tools.Common;
using AsterERP.Workflow.Tools.Pager;
using AsterERP.Workflow.Tools.Vos;

namespace AsterERP.Workflow.Approval.Core.Abstractions;

public abstract class WorkflowApprovalApiBase
{
    private readonly IWorkflowProcessInstanceRuntimeService _workflowProcessInstanceRuntimeService;
    private readonly IWorkflowTaskRuntimeService _workflowTaskRuntimeService;
    private readonly ICommentInfoRepository _commentInfoRepository;
    private readonly IFlowProcessDiagramService _flowProcessDiagramService;
    private readonly IWorkflowBpmnDefinitionService _workflowBpmnDefinitionService;
    private readonly IAppService _appService;
    private readonly ICategoryService _categoryService;
    private readonly IModelInfoService _modelInfoService;

    protected WorkflowApprovalApiBase(
        IWorkflowProcessInstanceRuntimeService workflowProcessInstanceRuntimeService,
        IWorkflowTaskRuntimeService workflowTaskRuntimeService,
        ICommentInfoRepository commentInfoRepository,
        IFlowProcessDiagramService flowProcessDiagramService,
        IWorkflowBpmnDefinitionService workflowBpmnDefinitionService,
        IAppService appService,
        ICategoryService categoryService,
        IModelInfoService modelInfoService)
    {
        _workflowProcessInstanceRuntimeService = workflowProcessInstanceRuntimeService;
        _workflowTaskRuntimeService = workflowTaskRuntimeService;
        _commentInfoRepository = commentInfoRepository;
        _flowProcessDiagramService = flowProcessDiagramService;
        _workflowBpmnDefinitionService = workflowBpmnDefinitionService;
        _appService = appService;
        _categoryService = categoryService;
        _modelInfoService = modelInfoService;
    }

    public async Task<ReturnVo<ModelInfoVo>> LoadBpmnXmlByModelKey(string modelKey, CancellationToken cancellationToken = default)
    {
        var modelInfoVo = await _workflowBpmnDefinitionService.LoadBpmnXmlByModelKeyAsync(modelKey, cancellationToken);
        return new ReturnVo<ModelInfoVo>(ReturnCode.SUCCESS, "OK") { Data = modelInfoVo };
    }

    public async Task<ReturnVo<ModelInfo>> GetModelInfoByModelKey(string modelKey, CancellationToken cancellationToken = default)
    {
        var modelInfo = await _modelInfoService.GetModelInfoByModelKeyAsync(modelKey, cancellationToken);
        return new ReturnVo<ModelInfo>(ReturnCode.SUCCESS, "OK") { Data = modelInfo };
    }

    public async Task<ReturnVo<PagerModel<ModelInfo>>> GetModelInfoVoByPagerModel(ModelInfo modelInfo, int pageNum, int pageSize, CancellationToken cancellationToken = default)
    {
        var pm = await _modelInfoService.GetPagerModelAsync(modelInfo, pageNum, pageSize, cancellationToken);
        return new ReturnVo<PagerModel<ModelInfo>>(ReturnCode.SUCCESS, "OK") { Data = pm };
    }

    public async Task<ReturnVo<StartorBaseInfoVo>> GetStartorBaseInfoVoByProcessInstanceId(string processInstanceId, CancellationToken cancellationToken = default)
    {
        var vo = await _workflowProcessInstanceRuntimeService.GetStartorBaseInfoVoByProcessInstanceIdAsync(processInstanceId, cancellationToken);
        return new ReturnVo<StartorBaseInfoVo>(ReturnCode.SUCCESS, "OK") { Data = vo };
    }

    public async Task<ReturnVo<ActivityVo>> GetOneActivityVoByProcessInstanceIdAndActivityId(string processInstanceId, string activityId, CancellationToken cancellationToken = default)
    {
        var vo = _flowProcessDiagramService.GetOneActivityVoByProcessInstanceIdAndActivityId(processInstanceId, activityId);
        return new ReturnVo<ActivityVo>(ReturnCode.SUCCESS, "OK") { Data = vo };
    }

    public async Task<ReturnVo<PagerModel<ProcessInstanceVo>>> FindMyProcessinstancesPagerModel(InstanceQueryParamsVo paramsVo, int pageNum, int pageSize, CancellationToken cancellationToken = default)
    {
        var pm = await _workflowProcessInstanceRuntimeService.FindMyProcessinstancesPagerModelAsync(paramsVo, pageNum, pageSize, cancellationToken);
        return new ReturnVo<PagerModel<ProcessInstanceVo>>(ReturnCode.SUCCESS, "OK") { Data = pm };
    }

    public async Task<ReturnVo<long>> GetAppingTaskCont(TaskQueryParamsVo @params, CancellationToken cancellationToken = default)
    {
        var count = await _workflowTaskRuntimeService.GetAppingTaskContAsync(@params, cancellationToken);
        return new ReturnVo<long>(ReturnCode.SUCCESS, "OK") { Data = count };
    }

    public async Task<ReturnVo<PagerModel<TaskVo>>> GetAppingTasksPagerModel(TaskQueryParamsVo paramsVo, int pageNum, int pageSize, CancellationToken cancellationToken = default)
    {
        var pm = await _workflowTaskRuntimeService.GetAppingTasksPagerModelAsync(paramsVo, pageNum, pageSize, cancellationToken);
        return new ReturnVo<PagerModel<TaskVo>>(ReturnCode.SUCCESS, "OK") { Data = pm };
    }

    public async Task<ReturnVo<PagerModel<TaskVo>>> GetApplyedTasksPagerModel(TaskQueryParamsVo paramsVo, int pageNum, int pageSize, CancellationToken cancellationToken = default)
    {
        var pm = await _workflowTaskRuntimeService.GetApplyedTasksPagerModelAsync(paramsVo, pageNum, pageSize, cancellationToken);
        return new ReturnVo<PagerModel<TaskVo>>(ReturnCode.SUCCESS, "OK") { Data = pm };
    }

    public async Task<ReturnVo<List<CommentInfo>>> GetCommentInfosByProcessInstanceId(string processInstanceId, CancellationToken cancellationToken = default)
    {
        var list = await _commentInfoRepository.GetCommentInfosByProcessInstanceIdAsync(processInstanceId, cancellationToken);
        return new ReturnVo<List<CommentInfo>>(ReturnCode.SUCCESS, "OK") { Data = list };
    }

    public async Task<ReturnVo<string>> StartProcessInstanceByKey(StartProcessInstanceVo @params, CancellationToken cancellationToken = default)
    {
        var result = await _workflowProcessInstanceRuntimeService.StartProcessInstanceByKeyAsync(@params, cancellationToken);
        if (result.Code == ReturnCode.SUCCESS && result.Data != null)
        {
            return new ReturnVo<string>(ReturnCode.SUCCESS, "OK") { Data = result.Data.Id };
        }
        return new ReturnVo<string>(result.Code, result.Msg);
    }

    public async Task<ReturnVo<string>> Complete(CompleteTaskVo completeTaskVo, CancellationToken cancellationToken = default)
    {
        return await _workflowTaskRuntimeService.CompleteAsync(completeTaskVo, cancellationToken);
    }

    public async Task<ReturnVo<string>> StopProcess(EndVo endVo, CancellationToken cancellationToken = default)
    {
        return await _workflowProcessInstanceRuntimeService.StopProcessAsync(endVo, cancellationToken);
    }

    public async Task<ReturnVo<HighLightedNodeVo>> GetHighLightedNodeVoByProcessInstanceId(
        string processInstanceId,
        CancellationToken cancellationToken = default)
    {
        var vo = await _flowProcessDiagramService
            .GetHighLightedNodeVoByProcessInstanceIdAsync(processInstanceId, cancellationToken);
        return new ReturnVo<HighLightedNodeVo>(ReturnCode.SUCCESS, "OK") { Data = vo };
    }

    public async Task<ReturnVo<List<App>>> GetApps(CancellationToken cancellationToken = default)
    {
        var apps = await _appService.GetActiveAppsAsync(cancellationToken);
        return new ReturnVo<List<App>>(ReturnCode.SUCCESS, "OK") { Data = apps?.Select(a => new App { Id = a.Id, Name = a.Name, Sn = a.Sn }).ToList() ?? new List<App>() };
    }

    public async Task<ReturnVo<List<Category>>> GetCategories(CancellationToken cancellationToken = default)
    {
        var category = new Category { FrontShow = 1 };
        var categories = await _categoryService.GetCategoriesAsync(category, cancellationToken);
        return new ReturnVo<List<Category>>(ReturnCode.SUCCESS, "OK") { Data = categories?.Select(c => new Category { Id = c.Id, Name = c.Name, Code = c.Code }).ToList() ?? new List<Category>() };
    }
}
