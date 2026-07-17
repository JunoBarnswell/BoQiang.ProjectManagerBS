using AsterERP.Workflow.Api.Shared;
using AsterERP.Workflow.Approval.Api.Models.Workflow;
using SqlSugar;

namespace AsterERP.Workflow.Approval.Core.Repositories.Workflow;

public interface IModelInfoRepository : IRepository<ModelInfo>
{
    Task<RefAsync<Page<ModelInfo>>> GetPagerModelAsync(ModelInfo modelInfo, int pageNum, int pageSize, CancellationToken cancellationToken = default);
}
