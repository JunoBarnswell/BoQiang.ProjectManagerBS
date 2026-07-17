using AsterERP.Workflow.Approval.Api.Models.Workflow;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Model;
using AsterERP.Workflow.Tools.Pager;
using AsterERP.Workflow.Tools.Vos;

namespace AsterERP.Workflow.Approval.Core.Services.Workflow;

public interface IModelInfoService
{
    Task<ModelInfo?> GetByModelIdAsync(string modelId, CancellationToken cancellationToken = default);
    Task<PagerModel<ModelInfo>> GetPagerModelAsync(ModelInfo modelInfo, int pageNum, int pageSize, CancellationToken cancellationToken = default);
    Task<ModelInfo> SaveOrUpdateModelInfoAsync(ModelInfo modelInfo, User user, CancellationToken cancellationToken = default);
    Task<ModelInfo> SaveOrUpdateModelInfoAsync(ModelInfo modelInfo, User user, bool flag, CancellationToken cancellationToken = default);
    Task<ModelInfo?> GetModelInfoByModelKeyAsync(string modelKey, CancellationToken cancellationToken = default);
    Task<ReturnVo<string>> DeleteByIdAsync(List<string> ids, CancellationToken cancellationToken = default);
}
