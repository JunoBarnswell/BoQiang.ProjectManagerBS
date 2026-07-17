using AsterERP.Workflow.Approval.Api.Models.Org;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Api.ViewModels.Org;
using AsterERP.Workflow.Tools.Vos;

namespace AsterERP.Workflow.Approval.Core.Services.Org;

public interface ICompanyService
{
    Task<ReturnVo<string>> ImportCompanyAsync(List<Company> companies, User loginUser, CancellationToken cancellationToken = default);
    Task SaveOrUpdateAsync(Company company, User loginUser, CancellationToken cancellationToken = default);
    Task<List<Company>> GetCompaniesAsync(Company company, CancellationToken cancellationToken = default);
    Task<ReturnVo<string>> DeleteByIdsAsync(List<string> ids, CancellationToken cancellationToken = default);
}
