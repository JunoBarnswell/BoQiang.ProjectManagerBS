using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Core.Repositories.Privilege;
using AsterERP.Workflow.Tools.Pager;
using SqlSugar;

namespace AsterERP.Workflow.Approval.Core.Services.Privilege;

public class LoginLogService : ILoginLogService
{
    private readonly ILoginLogRepository _repository;

    public LoginLogService(ILoginLogRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagerModel<LoginLog>> GetPagerModelByWrapperAsync(LoginLog loginLog, int pageNum, int pageSize, CancellationToken cancellationToken = default)
    {
        RefAsync<int> total = new();
        var query = _repository.Db.Queryable<LoginLog>();

        if (!string.IsNullOrWhiteSpace(loginLog.StartTimeStr) && string.IsNullOrWhiteSpace(loginLog.EndTimeStr))
        {
            query = query.Where(l => l.OperationTime > DateTime.Parse(loginLog.StartTimeStr));
        }
        if (string.IsNullOrWhiteSpace(loginLog.StartTimeStr) && !string.IsNullOrWhiteSpace(loginLog.EndTimeStr))
        {
            query = query.Where(l => l.OperationTime <= DateTime.Parse(loginLog.EndTimeStr));
        }
        if (!string.IsNullOrWhiteSpace(loginLog.StartTimeStr) && !string.IsNullOrWhiteSpace(loginLog.EndTimeStr))
        {
            query = query.Where(l => l.OperationTime > DateTime.Parse(loginLog.StartTimeStr) && l.OperationTime <= DateTime.Parse(loginLog.EndTimeStr));
        }
        if (!string.IsNullOrWhiteSpace(loginLog.Keyword))
        {
            query = query.Where(l => l.OperationPerson.Contains(loginLog.Keyword) || l.OperationUsername.Contains(loginLog.Keyword));
        }

        var list = await query.OrderByDescending(l => l.OperationTime)
            .ToPageListAsync(pageNum, pageSize, total, cancellationToken);
        return new PagerModel<LoginLog>(total.Value, list);
    }
}
