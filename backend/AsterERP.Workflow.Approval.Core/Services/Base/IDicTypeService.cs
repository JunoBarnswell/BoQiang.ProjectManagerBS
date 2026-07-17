using AsterERP.Workflow.Approval.Api.Models.Base;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Tools.Pager;
using AsterERP.Workflow.Tools.Vos;

namespace AsterERP.Workflow.Approval.Core.Services.Base;

public interface IDicTypeService
{
    Task<List<DicType>> GetDicTypesAsync(DicType dicType, CancellationToken cancellationToken = default);
    Task<PagerModel<DicType>> GetPagerModelByWrapperAsync(DicType dicType, int pageNum, int pageSize, CancellationToken cancellationToken = default);
    Task SaveOrUpdateAsync(DicType dicType, User loginUser, CancellationToken cancellationToken = default);
    Task<ReturnVo<string>> DeleteByIdsAsync(List<string> ids, CancellationToken cancellationToken = default);
}
