using AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Runtime;
using AsterERP.Workflow.Forms.Api.Abstractions;
using AsterERP.Workflow.Forms.Api.Models.Form;
using AsterERP.Workflow.Forms.Core.Services.Form;
using AsterERP.Workflow.Tools.Common;
using AsterERP.Workflow.Tools.Vos;
using Microsoft.Extensions.Logging;

namespace AsterERP.Workflow.Forms.Core.Abstractions;

public abstract class FormApiBase : IFormApi
{
    private readonly IFormInfoService _formInfoService;
    private readonly IFormDataInfoService _formDataInfoService;
    private readonly IFormFlowOperationService _formFlowOperationService;
    private readonly ILogger<FormApiBase> _logger;

    protected FormApiBase(
        IFormInfoService formInfoService,
        IFormDataInfoService formDataInfoService,
        IFormFlowOperationService formFlowOperationService,
        ILogger<FormApiBase> logger)
    {
        _formInfoService = formInfoService;
        _formDataInfoService = formDataInfoService;
        _formFlowOperationService = formFlowOperationService;
        _logger = logger;
    }

    public virtual async Task<ReturnVo<FormInfo>> GetFormInfoByModelKey(string modelKey)
    {
        var returnVo = new ReturnVo<FormInfo>(ReturnCode.SUCCESS, "OK");
        var formInfo = await _formInfoService.GetModelInfoByCodeAsync(modelKey);
        returnVo.Data = formInfo;
        return returnVo;
    }

    public virtual async Task<ReturnVo<string>> StartFormFlow(StartProcessInstanceVo startProcessInstanceVo)
    {
        var returnVo = await _formFlowOperationService.StartFormFlowAsync(startProcessInstanceVo);
        return returnVo;
    }

    public virtual async Task<ReturnVo<FormDataInfo>> GetFormDataInfoByProcessInstanceId(string procInstId)
    {
        var returnVo = new ReturnVo<FormDataInfo>(ReturnCode.FAIL, "获取表单数据失败！");
        try
        {
            var formDataInfo = await _formDataInfoService.GetFormDataInfoByProcessInstanceIdAsync(procInstId);
            returnVo.Msg = "获取表单数据成功！";
            returnVo.Code = ReturnCode.SUCCESS;
            returnVo.Data = formDataInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取表单数据异常");
            returnVo.Msg = "获取表单数据异常！原因：" + ex.Message;
        }
        return returnVo;
    }
}
