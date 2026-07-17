using AsterERP.Workflow.Api.Shared;
using AsterERP.Workflow.Approval.Api.Models.Org;
using SqlSugar;

namespace AsterERP.Workflow.Approval.Core.Repositories.Org;

public interface IPositionInfoRepository : IRepository<PositionInfo>
{
    Task<RefAsync<Page<PositionInfo>>> GetPagerModelAsync(PositionInfo positionInfo, int pageNum, int pageSize, CancellationToken cancellationToken = default);
}
