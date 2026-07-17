using AsterERP.Workflow.Approval.Api.Models.Org;

namespace AsterERP.Workflow.Approval.Core.Repositories.Org;

public interface IRolePositionPersonalRepository : IRepository<RolePositionPersonal>
{
    Task<List<Personal>> GetPersonalByRoleIdAndPositionIdAsync(string roleId, string positionCode, CancellationToken cancellationToken = default);
}
