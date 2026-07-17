using AsterERP.Workflow.Approval.Api.Models.Org;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Api.ViewModels.Org;
using AsterERP.Workflow.Tools.Pager;
using AsterERP.Workflow.Tools.Vos;

namespace AsterERP.Workflow.Approval.Core.Services.Org;

public interface IDepartmentService
{
    Task<ReturnVo<List<Department>>> ImportDepartmentAsync(List<Department> departments, User loginUser, CancellationToken cancellationToken = default);
    Task SaveOrUpdateAsync(Department department, User loginUser, CancellationToken cancellationToken = default);
    Task<ReturnVo<string>> DeleteByIdsAsync(List<string> ids, CancellationToken cancellationToken = default);
    Task<PagerModel<Department>> GetPagerModelByWrapperAsync(Department department, int pageNum, int pageSize, CancellationToken cancellationToken = default);
    Task<List<OrgTreeVo>> GetOrgTreeAsync(CancellationToken cancellationToken = default);
    Task<List<OrgTreeVo>> GetDepartmentTreeAsync(string companyId, string deptName, CancellationToken cancellationToken = default);
}
