using AsterERP.Workflow.Approval.Api.Models.Org;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Api.ViewModels.Org;
using AsterERP.Workflow.Tools.Pager;
using AsterERP.Workflow.Tools.Vos;

namespace AsterERP.Workflow.Approval.Core.Services.Org;

public interface IPositionInfoService
{
    Task<List<OrgTreeVo>> GetPositionTreeAsync(CancellationToken cancellationToken = default);
    Task SaveOrUpdateAsync(PositionInfo positionInfo, User loginUser, CancellationToken cancellationToken = default);
    Task<ReturnVo<string>> DeleteByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<PagerModel<PositionInfo>> GetPagerModelByWrapperAsync(PositionInfo positionInfo, int pageNum, int pageSize, CancellationToken cancellationToken = default);
    Task BatchSaveOrUpdatePositionSeqAndPositionAsync(PositionSeq positionSeq, List<PositionInfo> positionInfos, User loginUser, CancellationToken cancellationToken = default);
}
