using AsterERP.Workflow.Approval.Api.Models.Privilege;

namespace AsterERP.Workflow.Approval.Core.Repositories.Privilege;

public interface IModuleRepository : IRepository<Module>
{
    Task<List<Module>> GetModulesByIdsAsync(List<string> moduleIds, CancellationToken cancellationToken = default);
}
