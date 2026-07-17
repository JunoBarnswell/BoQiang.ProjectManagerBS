using AsterERP.Workflow.BpmnModel;
using BpmnModelType = AsterERP.Workflow.BpmnModel.BpmnModel;
using AsterERP.Workflow.BpmnParser.Converter;
using AsterERP.Workflow.Core.Services;
using AsterERP.Workflow.Persistence.Entities;
using AsterERP.Workflow.Approval.Api.Enums.Form;
using AsterERP.Workflow.Approval.Api.Models.Workflow;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Model;
using AsterERP.Workflow.Approval.Core.Repositories.Workflow;
using AsterERP.Workflow.Tools.Common;
using AsterERP.Workflow.Tools.Vos;
using Microsoft.Extensions.Logging;
using Volo.Abp.Guids;
using Volo.Abp.Timing;
using SqlSugar;

namespace AsterERP.Workflow.Approval.Core.Services.Workflow;

public class WorkflowBpmnDefinitionService : IWorkflowBpmnDefinitionService
{
    private const string BPMN_EXTENSION = ".bpmn";
    private const string BPMN20_XML_EXTENSION = ".bpmn20.xml";

    private readonly IModelInfoService _modelInfoService;
    private readonly IModelInfoRepository _modelInfoRepository;
    private readonly IRepositoryService _repositoryService;
    private readonly ILogger<WorkflowBpmnDefinitionService> _logger;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guidGenerator;

    public WorkflowBpmnDefinitionService(
        IModelInfoService modelInfoService,
        IModelInfoRepository modelInfoRepository,
        IRepositoryService repositoryService,
        ILogger<WorkflowBpmnDefinitionService> logger,
        IClock clock,
        IGuidGenerator guidGenerator)
    {
        _modelInfoService = modelInfoService;
        _modelInfoRepository = modelInfoRepository;
        _repositoryService = repositoryService;
        _logger = logger;
        _clock = clock;
        _guidGenerator = guidGenerator;
    }

    public async Task<ReturnVo<string>> ValidateBpmnModelAsync(string modelId, string fileName, Stream modelStream, CancellationToken cancellationToken = default)
    {
        var returnVo = new ReturnVo<string>(ReturnCode.SUCCESS, "OK");
        if (string.IsNullOrWhiteSpace(fileName))
        {
            var modelInfo = await _modelInfoService.GetByModelIdAsync(modelId, cancellationToken);
            fileName = (modelInfo?.ModelKey ?? modelId) + BPMN_EXTENSION;
        }
        try
        {
            using var reader = new StreamReader(modelStream, System.Text.Encoding.UTF8);
            var xmlContent = await reader.ReadToEndAsync(cancellationToken);
            var bpmnXMLConverter = new BpmnXMLConverter();
            var bpmnModel = bpmnXMLConverter.ConvertToBpmnModel(xmlContent);
            var modelInfo = await _modelInfoService.GetByModelIdAsync(modelId, cancellationToken);
            if (modelInfo?.ModelKey != null)
            {
                var mainProcess = bpmnModel.Processes.FirstOrDefault();
                if (mainProcess != null)
                {
                    mainProcess.Id = modelInfo.ModelKey;
                }
            }
            if (bpmnModel.Processes == null || bpmnModel.Processes.Count == 0)
            {
                return new ReturnVo<string>(ReturnCode.FAIL, "No process found in definition " + fileName);
            }
            returnVo = await ValidationErrorsAsync(bpmnModel, cancellationToken);
        }
        catch (Exception e)
        {
            return new ReturnVo<string>(ReturnCode.FAIL, "bpmn.js failed for " + fileName + ", error message " + e.Message);
        }
        return returnVo;
    }

    public async Task<ReturnVo<string>> ImportBpmnModelAsync(string modelId, string fileName, Stream modelStream, User user, CancellationToken cancellationToken = default)
    {
        var returnVo = new ReturnVo<string>(ReturnCode.SUCCESS, "OK");
        var modelInfo = await _modelInfoService.GetByModelIdAsync(modelId, cancellationToken);
        if (modelInfo == null)
        {
            return new ReturnVo<string>(ReturnCode.FAIL, "没有找到对应的模型，请确认!");
        }
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = modelInfo.ModelKey + BPMN_EXTENSION;
        }
        if (fileName != null && (fileName.EndsWith(BPMN_EXTENSION) || fileName.EndsWith(BPMN20_XML_EXTENSION)))
        {
            try
            {
                using var reader = new StreamReader(modelStream, System.Text.Encoding.UTF8);
                var xmlContent = await reader.ReadToEndAsync(cancellationToken);
                var bpmnXMLConverter = new BpmnXMLConverter();
                var bpmnModel = bpmnXMLConverter.ConvertToBpmnModel(xmlContent);
                var mainProcess = bpmnModel.Processes.FirstOrDefault();
                if (mainProcess != null)
                {
                    mainProcess.Id = modelInfo.ModelKey;
                    mainProcess.Name = string.IsNullOrWhiteSpace(mainProcess.Name) ? modelInfo.Name : mainProcess.Name;
                }
                if (bpmnModel.Processes == null || bpmnModel.Processes.Count == 0)
                {
                    return new ReturnVo<string>(ReturnCode.FAIL, "No process found in definition " + fileName);
                }
                modelInfo.ModelXml = xmlContent;
                modelInfo.Status = ModelFormStatusEnum.DFB.GetStatus();
                modelInfo.ExtendStatus = ModelFormStatusEnum.DFB.GetStatus();
                modelInfo.UpdateTime = _clock.Now;
                modelInfo.Updator = user.UserNo;
                await _modelInfoRepository.UpdateAsync(modelInfo, cancellationToken);
                returnVo.Data = modelId;
                return returnVo;
            }
            catch (Exception e)
            {
                return new ReturnVo<string>(ReturnCode.FAIL, "bpmn.js failed for " + fileName + ", error message " + e.Message);
            }
        }
        else
        {
            return new ReturnVo<string>(ReturnCode.FAIL, "Invalid file name, only .bpmn and .bpmn20.xml files are supported not " + fileName);
        }
    }

    public async Task<ReturnVo<string>> PublishBpmnAsync(string modelId, CancellationToken cancellationToken = default)
    {
        var returnVo = new ReturnVo<string>(ReturnCode.FAIL, "发布失败！");
        var modelInfo = await _modelInfoService.GetByModelIdAsync(modelId, cancellationToken);
        if (modelInfo == null)
        {
            returnVo.Msg = "没有找到对应的模型，请确认!";
            return returnVo;
        }
        if (string.IsNullOrWhiteSpace(modelInfo.AppSn))
        {
            returnVo.Msg = "发布失败，请设置流程所属系统！";
            return returnVo;
        }
        var editorSource = System.Text.Encoding.UTF8.GetBytes(modelInfo.ModelXml ?? string.Empty);
        var validationErrors = await _repositoryService.ValidateProcessAsync(editorSource, cancellationToken);
        if (validationErrors != null && validationErrors.Count > 0)
        {
            var errorMsg = string.Join("\n", validationErrors.Select(FormatValidationError));
            returnVo.Msg = errorMsg;
            return returnVo;
        }
        var statusReturnVo = CheckActive(modelInfo.Status, modelInfo.ExtendStatus);
        if (statusReturnVo.IsSuccess())
        {
            await DeployBpmnAsync(modelInfo, cancellationToken);
            modelInfo.Status = ModelFormStatusEnum.YFB.GetStatus();
            modelInfo.ExtendStatus = ModelFormStatusEnum.YFB.GetStatus();
            modelInfo.UpdateTime = _clock.Now;
            await _modelInfoRepository.UpdateAsync(modelInfo, cancellationToken);
            returnVo.Code = ReturnCode.SUCCESS;
            returnVo.Msg = "发布成功！";
        }
        else
        {
            returnVo.Msg = statusReturnVo.Msg;
        }
        return returnVo;
    }

    public async Task<ReturnVo<dynamic>> DeployBpmnAsync(ModelInfo modelInfo, CancellationToken cancellationToken = default)
    {
        var returnVo = new ReturnVo<dynamic>(ReturnCode.SUCCESS, "OK");
        var editorSource = System.Text.Encoding.UTF8.GetBytes(modelInfo.ModelXml ?? string.Empty);
        if (editorSource == null || editorSource.Length == 0)
        {
            returnVo.Code = ReturnCode.FAIL;
            returnVo.Msg = "模型编辑器源数据为空";
            return returnVo;
        }
        var resourceName = modelInfo.ModelKey + BPMN_EXTENSION;
        var resources = new Dictionary<string, byte[]>
        {
            [resourceName] = editorSource
        };
        var deploymentId = await _repositoryService.DeployAsync(
            modelInfo.Name,
            modelInfo.CategoryCode,
            modelInfo.AppSn,
            resources,
            cancellationToken: cancellationToken);
        returnVo.Data = deploymentId;
        return returnVo;
    }

    public async Task<ReturnVo<dynamic>> CreateInitBpmnAsync(ModelInfo modelInfo, User user, CancellationToken cancellationToken = default)
    {
        modelInfo.ModelId = string.IsNullOrWhiteSpace(modelInfo.ModelId)
            ? $"model-{_guidGenerator.Create():N}"
            : modelInfo.ModelId;
        modelInfo.ModelXml = BuildInitialBpmnXml(modelInfo.ModelKey, modelInfo.Name);
        modelInfo.UpdateTime = _clock.Now;
        modelInfo.Updator = user.UserNo;
        await _modelInfoRepository.UpdateAsync(modelInfo, cancellationToken);

        return new ReturnVo<dynamic>(ReturnCode.SUCCESS, "OK")
        {
            Data = modelInfo
        };
    }

    public async Task<ModelInfoVo?> LoadBpmnXmlByModelIdAsync(string modelId, CancellationToken cancellationToken = default)
    {
        var modelInfo = await _modelInfoService.GetByModelIdAsync(modelId, cancellationToken);
        if (modelInfo == null)
        {
            return null;
        }
        if (string.IsNullOrWhiteSpace(modelInfo.ModelXml))
        {
            modelInfo.ModelXml = BuildInitialBpmnXml(modelInfo.ModelKey, modelInfo.Name);
            modelInfo.UpdateTime = _clock.Now;
            await _modelInfoRepository.UpdateAsync(modelInfo, cancellationToken);
        }
        var modelInfoVo = new ModelInfoVo
        {
            ModelId = modelId,
            ModelName = modelInfo.Name,
            ModelKey = modelInfo.ModelKey,
            FileName = modelInfo.ModelKey + BPMN_EXTENSION,
            ModelXml = modelInfo.ModelXml,
            AppSn = modelInfo.AppSn,
            CategoryCode = modelInfo.CategoryCode
        };
        return modelInfoVo;
    }

    public async Task<ModelInfoVo?> LoadBpmnXmlByModelKeyAsync(string modelKey, CancellationToken cancellationToken = default)
    {
        var info = await _modelInfoService.GetModelInfoByModelKeyAsync(modelKey, cancellationToken);
        if (info != null)
        {
            return await LoadBpmnXmlByModelIdAsync(info.ModelId, cancellationToken);
        }
        return null;
    }

    public async Task<ReturnVo<string>> StopBpmnAsync(string modelId, CancellationToken cancellationToken = default)
    {
        var returnVo = new ReturnVo<string>(ReturnCode.SUCCESS, "OK");
        var modelInfo = await _modelInfoService.GetByModelIdAsync(modelId, cancellationToken);
        if (modelInfo != null)
        {
            modelInfo.Status = ModelFormStatusEnum.TY.GetStatus();
            modelInfo.ExtendStatus = ModelFormStatusEnum.TY.GetStatus();
            await _modelInfoRepository.UpdateAsync(modelInfo, cancellationToken);
        }
        return returnVo;
    }

    private static string BuildInitialBpmnXml(string modelKey, string modelName)
    {
        var processId = ToXmlId(modelKey, "process");
        var escapedName = EscapeXml(modelName);
        return $"""
<?xml version="1.0" encoding="UTF-8"?>
<bpmn2:definitions xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:bpmn2="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI" xmlns:dc="http://www.omg.org/spec/DD/20100524/DC" xmlns:di="http://www.omg.org/spec/DD/20100524/DI" id="Definitions_{processId}" targetNamespace="http://flowmaster.local/bpmn">
  <bpmn2:process id="{processId}" name="{escapedName}" isExecutable="true">
    <bpmn2:startEvent id="StartEvent_1" name="开始">
      <bpmn2:outgoing>Flow_1</bpmn2:outgoing>
    </bpmn2:startEvent>
    <bpmn2:sequenceFlow id="Flow_1" sourceRef="StartEvent_1" targetRef="EndEvent_1" />
    <bpmn2:endEvent id="EndEvent_1" name="结束">
      <bpmn2:incoming>Flow_1</bpmn2:incoming>
    </bpmn2:endEvent>
  </bpmn2:process>
  <bpmndi:BPMNDiagram id="BPMNDiagram_1">
    <bpmndi:BPMNPlane id="BPMNPlane_1" bpmnElement="{processId}">
      <bpmndi:BPMNShape id="StartEvent_1_di" bpmnElement="StartEvent_1">
        <dc:Bounds x="180" y="160" width="36" height="36" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id="EndEvent_1_di" bpmnElement="EndEvent_1">
        <dc:Bounds x="360" y="160" width="36" height="36" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNEdge id="Flow_1_di" bpmnElement="Flow_1">
        <di:waypoint x="216" y="178" />
        <di:waypoint x="360" y="178" />
      </bpmndi:BPMNEdge>
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</bpmn2:definitions>
""";
    }

    private static string ToXmlId(string value, string fallback)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        var chars = normalized.Select(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' ? ch : '_').ToArray();
        var result = new string(chars);
        return char.IsLetter(result[0]) || result[0] == '_' ? result : $"{fallback}_{result}";
    }

    private static string EscapeXml(string value)
    {
        return (value ?? string.Empty)
            .Replace("&", "&amp;")
            .Replace("\"", "&quot;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    private async Task<ReturnVo<string>> ValidationErrorsAsync(BpmnModelType bpmnModel, CancellationToken cancellationToken = default)
    {
        var returnVo = new ReturnVo<string>(ReturnCode.SUCCESS, "OK");
        var bpmnXMLConverter = new BpmnXMLConverter();
        var xmlBytes = bpmnXMLConverter.ConvertToXML(bpmnModel);
        var validationErrors = await _repositoryService.ValidateProcessAsync(xmlBytes, cancellationToken);
        if (validationErrors != null && validationErrors.Count > 0)
        {
            var message = string.Join("\n", validationErrors.Select(FormatValidationError));
            return new ReturnVo<string>(ReturnCode.FAIL, message);
        }
        return returnVo;
    }

    private static string FormatValidationError(object error)
    {
        var type = error.GetType();
        var message = type.GetProperty("Message")?.GetValue(error)?.ToString();
        var activityId = type.GetProperty("ActivityId")?.GetValue(error)?.ToString();
        var id = type.GetProperty("Id")?.GetValue(error)?.ToString();
        var errorType = type.GetProperty("Type")?.GetValue(error)?.ToString();

        var details = new List<string>();
        if (!string.IsNullOrWhiteSpace(errorType))
        {
            details.Add(errorType);
        }
        if (!string.IsNullOrWhiteSpace(id))
        {
            details.Add($"Id={id}");
        }
        if (!string.IsNullOrWhiteSpace(activityId))
        {
            details.Add($"ActivityId={activityId}");
        }

        var prefix = details.Count == 0 ? string.Empty : $"[{string.Join(", ", details)}] ";
        return prefix + (string.IsNullOrWhiteSpace(message) ? error.ToString() : message);
    }

    private static ReturnVo<string> CheckActive(int? modelStatus, int? extendStatus)
    {
        var returnVo = new ReturnVo<string>(ReturnCode.SUCCESS, "OK");
        if (modelStatus == null || extendStatus == null)
        {
            return new ReturnVo<string>(ReturnCode.FAIL, "流程模型状态异常");
        }
        var dfbStatus = ModelFormStatusEnum.DFB.GetStatus();
        var yfbStatus = ModelFormStatusEnum.YFB.GetStatus();
        var tyStatus = ModelFormStatusEnum.TY.GetStatus();
        if (modelStatus != dfbStatus && modelStatus != yfbStatus && modelStatus != tyStatus)
        {
            return new ReturnVo<string>(ReturnCode.FAIL, "未定义或发布流程模型信息");
        }
        if (extendStatus != dfbStatus && extendStatus != yfbStatus && extendStatus != tyStatus)
        {
            return new ReturnVo<string>(ReturnCode.FAIL, "未定义或发布流程配置信息");
        }
        return returnVo;
    }
}
