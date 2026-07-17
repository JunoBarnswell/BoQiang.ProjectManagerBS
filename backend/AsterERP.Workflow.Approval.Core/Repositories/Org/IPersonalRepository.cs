using AsterERP.Workflow.Api.Shared;
using AsterERP.Workflow.Approval.Api.Models.Org;
using SqlSugar;

namespace AsterERP.Workflow.Approval.Core.Repositories.Org;

public interface IPersonalRepository : IRepository<Personal>
{
    Task<List<Personal>> GetPersonalsByRoleSnsAsync(List<string> roleSns, CancellationToken cancellationToken = default);
    Task<List<Personal>> GetPersonalsByRoleIdsAsync(List<string> roleIds, CancellationToken cancellationToken = default);
    Task<RefAsync<Page<Personal>>> GetPagerModelAsync(Personal personal, int pageNum, int pageSize, CancellationToken cancellationToken = default);
}
