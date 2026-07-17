using AsterERP.Workflow.Approval.Api.Models.Privilege;

namespace AsterERP.Workflow.Approval.Core.Repositories.Privilege;

public interface IAclRepository : IRepository<Acl>
{
    Task<List<Acl>> GetAclsByGroupIdsAsync(List<string> groupIds, CancellationToken cancellationToken = default);
}
