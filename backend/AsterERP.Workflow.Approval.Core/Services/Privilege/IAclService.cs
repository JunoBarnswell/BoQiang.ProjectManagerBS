using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Api.ViewModels.Privilege;

namespace AsterERP.Workflow.Approval.Core.Services.Privilege;

public interface IAclService
{
    Task<List<Module>> GetModuleAclsByGroupIdsAsync(List<string> groupIds, CancellationToken cancellationToken = default);
    Task<HashSet<Acl>> GetAclsByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<HashSet<ModulePermission>> GetModulePermissionsByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<HashSet<ModulePermission>> GetModulePermissionsByAdminIdAsync(string admin, CancellationToken cancellationToken = default);
    Task CreateAclByModuleAsync(Acl acl, bool yes, CancellationToken cancellationToken = default);
    Task CreateAllAclAsync(Acl acl, bool yes, CancellationToken cancellationToken = default);
    Task CreateAclAsync(Acl acl, int position, bool yes, CancellationToken cancellationToken = default);
    Task SetAclModuleListAsync(List<int> positions, string moduleId, string groupId, CancellationToken cancellationToken = default);
    Task<Dictionary<string, List<int>>> GetAllPermissionsAsync(string username, string userId, CancellationToken cancellationToken = default);
}
