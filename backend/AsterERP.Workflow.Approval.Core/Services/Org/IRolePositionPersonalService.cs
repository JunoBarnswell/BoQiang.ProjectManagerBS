using AsterERP.Workflow.Approval.Api.Models.Org;
using AsterERP.Workflow.Approval.Api.Models.Privilege;

namespace AsterERP.Workflow.Approval.Core.Services.Org;

public interface IRolePositionPersonalService
{
    Task SaveOrUpdateBatchAsync(List<RolePositionPersonal> rolePositionPersonalList, User user, CancellationToken cancellationToken = default);
    Task<List<Personal>> GetPersonalByRoleIdAndPositionCodeAsync(string roleId, string positionCode, CancellationToken cancellationToken = default);
}
