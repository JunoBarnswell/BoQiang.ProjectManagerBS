using AsterERP.Workflow.Approval.Api.Models.Base;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Tools.Pager;
using AsterERP.Workflow.Tools.Vos;

namespace AsterERP.Workflow.Approval.Core.Services.Base;

public interface IDicItemService
{
    Task<List<DicItem>> GetDicItemsByMainIdAsync(string mainId, CancellationToken cancellationToken = default);
    Task<PagerModel<DicItem>> GetPagerModelByWrapperAsync(DicItem dicItem, int pageNum, int pageSize, CancellationToken cancellationToken = default);
    Task SaveOrUpdateAsync(DicItem dicItem, User loginUser, CancellationToken cancellationToken = default);
    Task<ReturnVo<string>> DeleteByIdsAsync(string[] ids, CancellationToken cancellationToken = default);
}
