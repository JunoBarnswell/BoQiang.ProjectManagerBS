using AsterERP.Workflow.Approval.Api.Enums.Form;
using AsterERP.Workflow.Approval.Api.Models.Workflow;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Model;
using AsterERP.Workflow.Approval.Core.Repositories.Workflow;
using AsterERP.Workflow.Tools.Common;
using AsterERP.Workflow.Tools.Pager;
using AsterERP.Workflow.Tools.Vos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SqlSugar;
using Volo.Abp.Guids;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Approval.Core.Services.Workflow;

public class ModelInfoService : IModelInfoService
{
    private readonly IModelInfoRepository _modelInfoRepository;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ModelInfoService> _logger;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guidGenerator;

    [ActivatorUtilitiesConstructor]
    public ModelInfoService(
        IModelInfoRepository modelInfoRepository,
        IServiceProvider serviceProvider,
        ILogger<ModelInfoService> logger,
        IClock clock,
        IGuidGenerator guidGenerator)
    {
        _modelInfoRepository = modelInfoRepository;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _clock = clock;
        _guidGenerator = guidGenerator;
    }

    private IWorkflowBpmnDefinitionService WorkflowBpmn =>
        _serviceProvider.GetRequiredService<IWorkflowBpmnDefinitionService>();

    public async Task<ModelInfo?> GetByModelIdAsync(string modelId, CancellationToken cancellationToken = default)
    {
        return await _modelInfoRepository.Db.Queryable<ModelInfo>()
            .FirstAsync(m => m.ModelId == modelId && m.DelFlag == 1, cancellationToken);
    }

    public async Task<PagerModel<ModelInfo>> GetPagerModelAsync(ModelInfo modelInfo, int pageNum, int pageSize, CancellationToken cancellationToken = default)
    {
        RefAsync<int> total = new();
        var list = await _modelInfoRepository.Db.Queryable<ModelInfo>()
            .Where(m => m.DelFlag == 1)
            .WhereIF(!string.IsNullOrWhiteSpace(modelInfo.AppSn), m => m.AppSn == modelInfo.AppSn)
            .WhereIF(!string.IsNullOrWhiteSpace(modelInfo.CategoryCode), m => m.CategoryCode == modelInfo.CategoryCode)
            .WhereIF(!string.IsNullOrWhiteSpace(modelInfo.ModelKey), m => m.ModelKey == modelInfo.ModelKey)
            .WhereIF(!string.IsNullOrWhiteSpace(modelInfo.Name), m => m.Name.Contains(modelInfo.Name))
            .WhereIF(modelInfo.Status.HasValue, m => m.Status == modelInfo.Status)
            .WhereIF(modelInfo.ExtendStatus.HasValue, m => m.ExtendStatus == modelInfo.ExtendStatus)
            .ToPageListAsync(pageNum, pageSize, total, cancellationToken);
        foreach (var item in list)
        {
            ApplyDisplayFields(item);
        }
        return new PagerModel<ModelInfo>(total.Value, list);
    }

    public async Task<ModelInfo> SaveOrUpdateModelInfoAsync(ModelInfo modelInfo, User user, CancellationToken cancellationToken = default)
    {
        return await SaveOrUpdateModelInfoAsync(modelInfo, user, false, cancellationToken);
    }

    public async Task<ModelInfo> SaveOrUpdateModelInfoAsync(ModelInfo modelInfo, User user, bool flag, CancellationToken cancellationToken = default)
    {
        NormalizeModelInfo(modelInfo, user);
        if (string.IsNullOrWhiteSpace(modelInfo.Id))
        {
            var now = _clock.Now;
            modelInfo.Id = _guidGenerator.Create().ToString("N");
            modelInfo.ModelId ??= $"model-{_guidGenerator.Create():N}";
            modelInfo.CreateTime = now;
            modelInfo.Creator = user.UserNo;
            modelInfo.UpdateTime = now;
            modelInfo.Status = ModelFormStatusEnum.CG.GetStatus();
            modelInfo.ExtendStatus = ModelFormStatusEnum.CG.GetStatus();
            await _modelInfoRepository.InsertAsync(modelInfo, cancellationToken);

            if (flag)
            {
                var returnVo = await WorkflowBpmn.CreateInitBpmnAsync(modelInfo, user, cancellationToken);
                if (returnVo.IsSuccess() && returnVo.Data != null)
                {
                    var engineModelId = ReadModelId(returnVo.Data);
                    if (!string.IsNullOrWhiteSpace(engineModelId) && engineModelId != modelInfo.ModelId)
                    {
                        modelInfo.ModelId = engineModelId;
                        modelInfo.UpdateTime = _clock.Now;
                        await _modelInfoRepository.UpdateAsync(modelInfo, cancellationToken);
                    }
                }
            }
        }
        else
        {
            modelInfo.UpdateTime = _clock.Now;
            modelInfo.Updator = user.UserNo;
            modelInfo.ExtendStatus = ModelFormStatusEnum.DFB.GetStatus();
            await _modelInfoRepository.UpdateAsync(modelInfo, cancellationToken);
        }
        return modelInfo;
    }

    public async Task<ModelInfo?> GetModelInfoByModelKeyAsync(string modelKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelKey)) return null;
        return await _modelInfoRepository.Db.Queryable<ModelInfo>()
            .FirstAsync(m => m.ModelKey == modelKey && m.DelFlag == 1, cancellationToken);
    }

    public async Task<ReturnVo<string>> DeleteByIdAsync(List<string> ids, CancellationToken cancellationToken = default)
    {
        if (ids == null || ids.Count == 0)
            return new ReturnVo<string>(ReturnCode.SUCCESS, "OK");

        var modelInfos = await _modelInfoRepository.Db.Queryable<ModelInfo>()
            .Where(m => m.DelFlag == 1)
            .In(m => m.Id, ids)
            .ToListAsync(cancellationToken);
        if (modelInfos.Count == 0)
        {
            return new ReturnVo<string>(ReturnCode.SUCCESS, "OK");
        }

        var nonDraftExists = modelInfos.Any(modelInfo =>
            modelInfo.Status != ModelFormStatusEnum.CG.GetStatus() ||
            modelInfo.ExtendStatus != ModelFormStatusEnum.CG.GetStatus());
        if (nonDraftExists)
        {
            return new ReturnVo<string>(ReturnCode.FAIL, "模型不是草稿状态，请勿删除！");
        }

        await _modelInfoRepository.Db.Deleteable<ModelInfo>()
            .In(m => m.Id, modelInfos.Select(modelInfo => modelInfo.Id).ToList())
            .ExecuteCommandAsync(cancellationToken);
        return new ReturnVo<string>(ReturnCode.SUCCESS, "OK");
    }

    private void NormalizeModelInfo(ModelInfo modelInfo, User user)
    {
        var now = _clock.Now;
        modelInfo.ModelId = string.IsNullOrWhiteSpace(modelInfo.ModelId)
            ? $"model-{_guidGenerator.Create():N}"
            : modelInfo.ModelId;
        modelInfo.Name = modelInfo.Name?.Trim() ?? string.Empty;
        modelInfo.ModelKey = modelInfo.ModelKey?.Trim() ?? string.Empty;
        modelInfo.ModelType ??= ModelInfo.CUSTOM_MODEL_TYPE;
        modelInfo.FormType ??= 0;
        modelInfo.AppSn = string.IsNullOrWhiteSpace(modelInfo.AppSn) ? "FLOWMASTER" : modelInfo.AppSn.Trim();
        modelInfo.CategoryCode = modelInfo.CategoryCode?.Trim() ?? "FLOW_GENERAL";
        modelInfo.OwnDeptId ??= string.Empty;
        modelInfo.OwnDeptName ??= string.Empty;
        modelInfo.FlowOwnerNo ??= string.Empty;
        modelInfo.FlowOwnerName ??= string.Empty;
        modelInfo.ProcessDockingNo ??= string.Empty;
        modelInfo.ProcessDockingName ??= string.Empty;
        modelInfo.ApplyCompanies ??= string.Empty;
        modelInfo.ShowStatus ??= string.Empty;
        modelInfo.AppliedRange ??= 0;
        modelInfo.AuthPointList ??= string.Empty;
        modelInfo.Superuser ??= string.Empty;
        modelInfo.BusinessUrl ??= string.Empty;
        modelInfo.SkipSet ??= 0;
        modelInfo.ModelIcon ??= string.Empty;
        modelInfo.OrderNo ??= 0;
        modelInfo.ModelXml ??= string.Empty;
        modelInfo.Creator = string.IsNullOrWhiteSpace(modelInfo.Creator) ? user.UserNo : modelInfo.Creator;
        modelInfo.Updator = user.UserNo;
        modelInfo.CreateTime ??= now;
        modelInfo.UpdateTime = now;
        modelInfo.DelFlag ??= 1;
        modelInfo.Keyword = $"{modelInfo.Name} {modelInfo.ModelKey}".Trim();
    }

    private static void ApplyDisplayFields(ModelInfo modelInfo)
    {
        var minStatus = ModelFormStatusEnumExtensions.GetMinStatus(modelInfo.Status, modelInfo.ExtendStatus);
        if (minStatus != null)
        {
            modelInfo.StatusName = minStatus.Value.GetMsg();
            modelInfo.Status = minStatus.Value.GetStatus();
        }
    }

    private static string? ReadModelId(object created)
    {
        if (created is ModelInfo modelInfo)
        {
            return string.IsNullOrWhiteSpace(modelInfo.ModelId) ? modelInfo.Id : modelInfo.ModelId;
        }

        if (created is IDictionary<string, object?> dictionary)
        {
            var dictionaryModelId = dictionary.TryGetValue("ModelId", out var modelIdValue) ? modelIdValue?.ToString() : null;
            if (!string.IsNullOrWhiteSpace(dictionaryModelId))
            {
                return dictionaryModelId;
            }
            return dictionary.TryGetValue("Id", out var idValue) ? idValue?.ToString() : null;
        }

        var type = created.GetType();
        var modelId = type.GetProperty("ModelId")?.GetValue(created)?.ToString();
        if (!string.IsNullOrWhiteSpace(modelId))
        {
            return modelId;
        }
        return type.GetProperty("Id")?.GetValue(created)?.ToString();
    }
}
