using AsterERP.Workflow.Approval.Api.Models.Workflow;
using AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Model;
using AsterERP.Workflow.Approval.Api.ViewModels.Workflow.ProcessInstance;
using AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Runtime;
using AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Task;
using AsterERP.Workflow.Approval.Api.ViewModels.Pager;
using AsterERP.Workflow.Tools.Pager;
using AsterERP.Workflow.Tools.Vos;

namespace AsterERP.Workflow.Approval.Api.Abstractions;

public interface IWorkflowApprovalApi
{
    ReturnVo<ModelInfoVo> LoadBpmnXmlByModelKey(string modelKey);

    ReturnVo<ModelInfo> GetModelInfoByModelKey(string modelKey);

    ReturnVo<StartorBaseInfoVo> GetStartorBaseInfoVoByProcessInstanceId(string processInstanceId);

    ReturnVo<PagerModel<object>> GetModelInfoVoByPagerModel(ParamVo<ModelInfo> @params);

    ReturnVo<ActivityVo> GetOneActivityVoByProcessInstanceIdAndActivityId(string processInstanceId, string activityId);

    ReturnVo<PagerModel<object>> FindMyProcessinstancesPagerModel(ParamVo<InstanceQueryParamsVo> processInstanceVoParamVo);

    ReturnVo<PagerModel<object>> GetAppingTasksPagerModel(ParamVo<TaskQueryParamsVo> taskQueryParamsVoParamVo);

    ReturnVo<PagerModel<object>> GetApplyedTasksPagerModel(ParamVo<TaskQueryParamsVo> taskQueryParamsVoParamVo);

    ReturnVo<List<object>> GetCommentInfosByProcessInstanceId(string processInstanceId);

    ReturnVo<string> StartProcessInstanceByKey(StartProcessInstanceVo startParams);

    ReturnVo<string> Complete(CompleteTaskVo completeTaskVo);

    ReturnVo<string> StopProcess(EndVo endVo);

    ReturnVo<HighLightedNodeVo> GetHighLightedNodeVoByProcessInstanceId(string processInstanceId);

    ReturnVo<long> GetAppingTaskCont(TaskQueryParamsVo taskQueryParams);

    ReturnVo<List<object>> GetApps();

    ReturnVo<List<object>> GetCategories();
}
