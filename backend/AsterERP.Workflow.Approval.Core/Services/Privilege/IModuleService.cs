using AsterERP.Workflow.Approval.Api.Models.Privilege;

namespace AsterERP.Workflow.Approval.Core.Services.Privilege;

public interface IModuleService
{
    Task SaveOrUpdateModuleAsync(Module module, CancellationToken cancellationToken = default);
    Task DeleteByIdsAsync(List<string> ids, CancellationToken cancellationToken = default);
    Task DeletePriValAsync(string appPrivilegeValueId, string moduleId, CancellationToken cancellationToken = default);
    Task AddPriValAsync(List<int> positions, string moduleId, CancellationToken cancellationToken = default);
    Task<List<Module>> GetModulesAsync(Module module, CancellationToken cancellationToken = default);
    Task<List<Module>> GetActiveModulesAsync(CancellationToken cancellationToken = default);
    Task<List<Module>> GetModulesByIdsAsync(List<string> moduleIds, CancellationToken cancellationToken = default);
    Task<Module?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<Module?> GetModuleBySnAsync(string sn, CancellationToken cancellationToken = default);
}
