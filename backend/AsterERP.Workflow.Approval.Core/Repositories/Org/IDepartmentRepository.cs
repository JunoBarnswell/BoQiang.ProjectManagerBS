using AsterERP.Workflow.Api.Shared;
using AsterERP.Workflow.Approval.Api.Models.Org;
using SqlSugar;

namespace AsterERP.Workflow.Approval.Core.Repositories.Org;

public interface IDepartmentRepository : IRepository<Department>
{
    Task<List<Department>> GetDepartmentsAsync(Department department, CancellationToken cancellationToken = default);
    Task<RefAsync<Page<Department>>> GetPagerModelAsync(Department department, int pageNum, int pageSize, CancellationToken cancellationToken = default);
}
