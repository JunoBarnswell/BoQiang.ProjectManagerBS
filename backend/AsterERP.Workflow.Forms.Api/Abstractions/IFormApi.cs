using AsterERP.Workflow.Forms.Api.Models.Form;
using AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Runtime;
using AsterERP.Workflow.Tools.Vos;

namespace AsterERP.Workflow.Forms.Api.Abstractions;

public interface IFormApi
{
    Task<ReturnVo<FormInfo>> GetFormInfoByModelKey(string modelKey);

    Task<ReturnVo<string>> StartFormFlow(StartProcessInstanceVo startProcessInstanceVo);

    Task<ReturnVo<FormDataInfo>> GetFormDataInfoByProcessInstanceId(string procInstId);
}
