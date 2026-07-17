using AsterERP.Workflow.Approval.Api.Models.Privilege;

namespace AsterERP.Workflow.Approval.Core.Services.Privilege;

public interface IAppPrivilegeValueService
{
    Task<List<AppPrivilegeValue>> GetAppPrivilegeValuesAsync(CancellationToken cancellationToken = default);
    Task SaveOrUpdateAsync(AppPrivilegeValue appPrivilegeValue, CancellationToken cancellationToken = default);
    Task DeleteByIdsAsync(List<string> appIds, CancellationToken cancellationToken = default);
}
