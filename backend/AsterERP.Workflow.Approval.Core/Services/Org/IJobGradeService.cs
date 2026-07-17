using AsterERP.Workflow.Approval.Api.Models.Org;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Api.ViewModels.Org;
using AsterERP.Workflow.Tools.Vos;

namespace AsterERP.Workflow.Approval.Core.Services.Org;

public interface IJobGradeService
{
    Task<List<OrgTreeVo>> GetJobGradeTreeAsync(CancellationToken cancellationToken = default);
    Task<ReturnVo<string>> DeleteByIdAsync(string id, CancellationToken cancellationToken = default);
    Task SaveOrUpdateAsync(JobGrade jobGrade, User loginUser, CancellationToken cancellationToken = default);
    Task BatchSaveOrUpdateJobGradeTypeAndJobGradeAsync(JobGradeType jobGradeType, List<JobGrade> jobGrades, User loginUser, CancellationToken cancellationToken = default);
}
