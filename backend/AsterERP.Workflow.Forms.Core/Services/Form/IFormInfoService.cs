using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Forms.Api.Models.Form;
using AsterERP.Workflow.Tools.Pager;

namespace AsterERP.Workflow.Forms.Core.Services.Form;

public interface IFormInfoService
{
    Task<PagerModel<FormInfo>> GetPagerModelByWrapperAsync(FormInfo formInfo, int pageNum, int pageSize, CancellationToken cancellationToken = default);

    Task SaveOrUpdateAsync(FormInfo formInfo, User loginUser, CancellationToken cancellationToken = default);

    Task<FormInfo?> GetModelInfoByCodeAsync(string code, CancellationToken cancellationToken = default);
}
