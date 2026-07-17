using System.Text;
using System.Xml.Linq;
using AsterERP.Workflow.Approval.Api.Models.Workflow;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Tools.Common;
using AsterERP.Workflow.Tools.Vos;
using Microsoft.Extensions.Logging;
using Volo.Abp.Guids;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Approval.Core.Services.Workflow;

public class WorkflowModelRuntimeService : IWorkflowModelRuntimeService
{
    private readonly ILogger<WorkflowModelRuntimeService> _logger;
    private readonly IModelInfoService _modelInfoService;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guidGenerator;

    public WorkflowModelRuntimeService(
        ILogger<WorkflowModelRuntimeService> logger,
        IModelInfoService modelInfoService,
        IClock clock,
        IGuidGenerator guidGenerator)
    {
        _logger = logger;
        _modelInfoService = modelInfoService;
        _clock = clock;
        _guidGenerator = guidGenerator;
    }

    public async Task<ReturnVo<dynamic>> CreateModelAsync(dynamic modelRepresentation, User user, CancellationToken cancellationToken = default)
    {
        var returnVo = new ReturnVo<dynamic>(ReturnCode.SUCCESS, "OK");
        string key = ((string)modelRepresentation.Key).Replace(" ", "");
        var existingModel = await _modelInfoService.GetModelInfoByModelKeyAsync(key, cancellationToken);
        if (existingModel != null)
        {
            returnVo.Data = existingModel;
            return returnVo;
        }
        string name = modelRepresentation.Name;
        int modelType = modelRepresentation.ModelType ?? 0;
        string? description = modelRepresentation.Description;
        var json = BuildModelJson(key, name, modelType, description, _guidGenerator.Create().ToString(), _clock.Now);
        var modelInfo = new ModelInfo
        {
            Name = name,
            ModelKey = key,
            ModelType = modelType,
            ModelXml = json
        };
        var savedModel = await _modelInfoService.SaveOrUpdateModelInfoAsync(modelInfo, user, cancellationToken);
        returnVo.Data = savedModel;
        return returnVo;
    }

    private static string BuildModelJson(string key, string name, int modelType, string? description, string modelId, DateTime now)
    {
        var modelObj = new
        {
            modelId,
            name = name,
            key = key,
            description = description ?? "",
            modelType = modelType,
            lastUpdated = now.ToUniversalTime().ToString("o"),
            created = now.ToUniversalTime().ToString("o"),
            version = "1",
            stencil = new
            {
                id = "BPMNDiagram"
            }
        };
        return System.Text.Json.JsonSerializer.Serialize(modelObj);
    }

    public async Task<dynamic> ImportDecisionServiceModelAsync(string modelId, Stream file, User user, CancellationToken cancellationToken = default)
    {
        var fileName = string.Empty;
        try
        {
            using var reader = new StreamReader(file, Encoding.UTF8);
            var xmlContent = await reader.ReadToEndAsync(cancellationToken);
            var xdoc = XDocument.Parse(xmlContent);
            var ns = xdoc.Root?.Name.Namespace;
            var decisionServices = xdoc.Root?.Elements(ns + "decisionService").ToList();
            if (decisionServices == null || decisionServices.Count == 0)
            {
                throw new InvalidOperationException("No decision services found in definition " + fileName);
            }
            var firstDecisionService = decisionServices[0];
            var name = firstDecisionService.Attribute("id")?.Value ?? "decisionService";
            var nameAttr = firstDecisionService.Attribute("name")?.Value;
            if (!string.IsNullOrWhiteSpace(nameAttr))
            {
                name = nameAttr;
            }
            var modelInfo = new ModelInfo
            {
                Name = name,
                ModelKey = firstDecisionService.Attribute("id")?.Value?.Replace(" ", "") ?? _guidGenerator.Create().ToString(),
                ModelType = 4,
                ModelXml = xmlContent
            };
            if (!string.IsNullOrWhiteSpace(modelId))
            {
                var existingModel = await _modelInfoService.GetByModelIdAsync(modelId, cancellationToken);
                if (existingModel != null)
                {
                    modelInfo.Id = existingModel.Id;
                    modelInfo.ModelId = existingModel.ModelId;
                }
            }
            var savedModel = await _modelInfoService.SaveOrUpdateModelInfoAsync(modelInfo, user, cancellationToken);
            return savedModel;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Import failed for {FileName}", fileName);
            throw new InvalidOperationException("Import failed for " + fileName + ", error message " + e.Message);
        }
    }

    public async Task<dynamic> DuplicateModelAsync(string modelId, dynamic modelRepresentation, User user, CancellationToken cancellationToken = default)
    {
        string? json = null;
        ModelInfo? model = null;
        if (modelId != null)
        {
            model = await _modelInfoService.GetByModelIdAsync(modelId, cancellationToken);
            if (model != null)
            {
                json = model.ModelXml;
            }
        }
        if (model == null)
        {
            throw new InvalidOperationException("Error duplicating model : Unknown original model");
        }
        string key = ((string)modelRepresentation.Key).Replace(" ", "");
        string name = modelRepresentation.Name;
        var existingModel = await _modelInfoService.GetModelInfoByModelKeyAsync(key, cancellationToken);
        if (existingModel != null)
        {
            throw new InvalidOperationException("Provided model key already exists: " + key);
        }
        if (modelRepresentation.ModelType == null || (int)modelRepresentation.ModelType == 0)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var jsonDoc = System.Text.Json.JsonDocument.Parse(json);
                    var root = jsonDoc.RootElement;
                    if (root.TryGetProperty("properties", out var properties))
                    {
                        var modifiedJson = json;
                        var processId = key.Replace(" ", "");
                        modifiedJson = modifiedJson.Replace("\"process_id\"", "\"" + processId + "\"");
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            modifiedJson = modifiedJson.Replace("\"name\"", "\"" + name + "\"");
                        }
                        json = modifiedJson;
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error modifying BPMN model JSON");
            }
        }
        var newModelInfo = new ModelInfo
        {
            Name = name,
            ModelKey = key,
            ModelType = modelRepresentation.ModelType,
            ModelXml = json
        };
        var savedModel = await _modelInfoService.SaveOrUpdateModelInfoAsync(newModelInfo, user, true, cancellationToken);
        return savedModel;
    }
}
