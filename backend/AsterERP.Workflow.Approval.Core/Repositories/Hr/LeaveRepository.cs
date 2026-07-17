using AsterERP.Workflow.Approval.Api.Models.Hr;
using SqlSugar;

namespace AsterERP.Workflow.Approval.Core.Repositories.Hr;

public class LeaveRepository : SqlSugarRepository<Leave>, ILeaveRepository
{
    public LeaveRepository(ISqlSugarClient db) : base(db)
    {
    }
}
