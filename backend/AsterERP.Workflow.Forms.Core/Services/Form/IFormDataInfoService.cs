using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Forms.Api.Models.Form;
using AsterERP.Workflow.Tools.Pager;

namespace AsterERP.Workflow.Forms.Core.Services.Form;

public interface IFormDataInfoService
{
    Task<PagerModel<FormDataInfo>> GetPagerModelByWrapperAsync(FormDataInfo formDataInfo, int pageNum, int pageSize, CancellationToken cancellationToken = default);

    Task SaveOrUpdateAsync(FormDataInfo formDataInfo, User loginUser, CancellationToken cancellationToken = default);

    Task<FormDataInfo?> GetFormDataInfoByModelKeyAndBusinessKeyAsync(string modelKey, string businessKey, CancellationToken cancellationToken = default);

    Task<FormDataInfo?> GetFormDataInfoByProcessInstanceIdAsync(string processInstanceId, CancellationToken cancellationToken = default);
}
