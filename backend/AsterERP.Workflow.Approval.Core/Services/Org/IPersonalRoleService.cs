using AsterERP.Workflow.Approval.Api.Models.Org;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Api.ViewModels.Org;

namespace AsterERP.Workflow.Approval.Core.Services.Org;

public interface IPersonalRoleService
{
    Task AddPersonalRolesByPersonalAsync(string personalId, List<Role> roles, User loginUser, CancellationToken cancellationToken = default);
    Task AddPersonalRolesByRoleAsync(string roleId, List<Personal> personals, User loginUser, CancellationToken cancellationToken = default);
    Task<List<RolePersonalVo>> GetRolePersonalsAsync(PersonalRole personalRole, CancellationToken cancellationToken = default);
}
