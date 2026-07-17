using AsterERP.Workflow.Approval.Api.Models.Org;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Tools.Vos;

namespace AsterERP.Workflow.Approval.Core.Services.Org;

public interface IJobGradeTypeService
{
    Task SaveOrUpdateAsync(JobGradeType jobGradeType, User loginUser, CancellationToken cancellationToken = default);
    Task<ReturnVo<string>> DeleteByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<List<JobGradeType>> GetActiveJobGradeTypesAsync(CancellationToken cancellationToken = default);
}
