using AsterERP.Workflow.Approval.Api.Models.Org;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Tools.Pager;
using AsterERP.Workflow.Tools.Vos;

namespace AsterERP.Workflow.Approval.Core.Services.Org;

public interface IPositionSeqService
{
    Task SaveOrUpdateAsync(PositionSeq positionSeq, User loginUser, CancellationToken cancellationToken = default);
    Task<ReturnVo<string>> DeleteByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<List<PositionSeq>> GetActivePositionSeqsAsync(CancellationToken cancellationToken = default);
}
