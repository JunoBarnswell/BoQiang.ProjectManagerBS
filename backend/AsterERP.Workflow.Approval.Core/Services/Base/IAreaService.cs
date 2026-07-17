using AsterERP.Workflow.Approval.Api.Models.Base;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Tools.Vos;

namespace AsterERP.Workflow.Approval.Core.Services.Base;

public interface IAreaService
{
    Task SaveOrUpdateAsync(Area area, User loginUser, CancellationToken cancellationToken = default);
    Task<ReturnVo<string>> DeleteByCodesAsync(string[] codes, CancellationToken cancellationToken = default);
}
