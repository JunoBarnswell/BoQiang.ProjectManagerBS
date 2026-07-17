using AsterERP.Workflow.Approval.Api.Models.Base;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Tools.Pager;
using AsterERP.Workflow.Tools.Vos;

namespace AsterERP.Workflow.Approval.Core.Services.Base;

public interface ICategoryService
{
    Task<List<Category>> GetCategoriesAsync(Category category, CancellationToken cancellationToken = default);
    Task<PagerModel<Category>> GetPagerModelByWrapperAsync(Category category, int pageNum, int pageSize, CancellationToken cancellationToken = default);
    Task SaveOrUpdateAsync(Category category, User loginUser, CancellationToken cancellationToken = default);
    Task<ReturnVo<string>> DeleteByIdsAsync(List<string> ids, CancellationToken cancellationToken = default);
}
