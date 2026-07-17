using AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Runtime;
using AsterERP.Workflow.Forms.Api.Models.Form;
using AsterERP.Workflow.Approval.Core.Services.Workflow;
using AsterERP.Workflow.Forms.Core.Repositories.Form;
using AsterERP.Workflow.Tools.Common;
using AsterERP.Workflow.Tools.Vos;
using Volo.Abp.Guids;
using Volo.Abp.Timing;
using Microsoft.Extensions.Logging;

namespace AsterERP.Workflow.Forms.Core.Services.Form;

public class FormFlowOperationService : IFormFlowOperationService
{
    private readonly IWorkflowProcessInstanceRuntimeService _workflowProcessInstanceRuntimeService;
    private readonly IFormDataInfoRepository _formDataInfoRepository;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILogger<FormFlowOperationService> _logger;

    public FormFlowOperationService(
        IWorkflowProcessInstanceRuntimeService workflowProcessInstanceRuntimeService,
        IFormDataInfoRepository formDataInfoRepository,
        IClock clock,
        IGuidGenerator guidGenerator,
        ILogger<FormFlowOperationService> logger)
    {
        _workflowProcessInstanceRuntimeService = workflowProcessInstanceRuntimeService;
        _formDataInfoRepository = formDataInfoRepository;
        _clock = clock;
        _guidGenerator = guidGenerator;
        _logger = logger;
    }

    public async Task<ReturnVo<string>> StartFormFlowAsync(StartProcessInstanceVo @params, CancellationToken cancellationToken = default)
    {
        var returnVo = new ReturnVo<string>(ReturnCode.FAIL, "启动失败!");
        try
        {
            var processInstanceReturnVo = await _workflowProcessInstanceRuntimeService.StartProcessInstanceByKeyAsync(@params, cancellationToken);
            if (!processInstanceReturnVo.IsSuccess() || processInstanceReturnVo.Data == null)
            {
                returnVo.Msg = processInstanceReturnVo.Msg;
                return returnVo;
            }

            var processInstanceId = processInstanceReturnVo.Data.Id;
            var now = _clock.Now;

            var creator = string.IsNullOrWhiteSpace(@params.Creator) ? @params.CurrentUserCode : @params.Creator;
            var formDataInfo = new FormDataInfo
            {
                Id = _guidGenerator.Create().ToString("N"),
                Creator = creator,
                CreateTime = now,
                Updator = creator,
                UpdateTime = now,
                DelFlag = 1,
                Keyword = string.Empty,
                BusinessKey = @params.BusinessKey,
                ProcessInstanceId = processInstanceId,
                ModelKey = @params.ProcessDefinitionKey,
                FormData = @params.FormData
            };

            await _formDataInfoRepository.InsertAsync(formDataInfo, cancellationToken);

            returnVo = new ReturnVo<string>(ReturnCode.SUCCESS, "启动成功!")
            {
                Data = processInstanceId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动表单流程失败");
            returnVo.Msg = $"启动流程失败，原因：{ex.Message}";
        }
        return returnVo;
    }
}
