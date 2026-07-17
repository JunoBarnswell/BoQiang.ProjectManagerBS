using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Core.Repositories.Privilege;

namespace AsterERP.Workflow.Approval.Core.Services.Privilege;

public class ShiroSessionService : IShiroSessionService
{
    private readonly IShiroSessionRepository _repository;

    public ShiroSessionService(IShiroSessionRepository repository)
    {
        _repository = repository;
    }
}
