using AsterERP.Workflow.Approval.Api.Models.Org;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Api.ViewModels.Org;
using AsterERP.Workflow.Approval.Core.Repositories.Org;
using AsterERP.Workflow.Tools.Common;
using AsterERP.Workflow.Tools.Vos;
using SqlSugar;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Approval.Core.Services.Org;

public class CompanyService : ICompanyService
{
    private readonly ICompanyRepository _companyRepository;
    private readonly IClock _clock;

    public CompanyService(ICompanyRepository companyRepository, IClock clock)
    {
        _companyRepository = companyRepository;
        _clock = clock;
    }

    public async Task<ReturnVo<string>> ImportCompanyAsync(List<Company> companies, User loginUser, CancellationToken cancellationToken = default)
    {
        var returnVo = new ReturnVo<string>(ReturnCode.SUCCESS, "OK");
        if (companies != null && companies.Count > 0)
        {
            foreach (var company in companies)
            {
                await SaveOrUpdateAsync(company, loginUser, cancellationToken);
            }
        }
        return returnVo;
    }

    public async Task SaveOrUpdateAsync(Company company, User loginUser, CancellationToken cancellationToken = default)
    {
        var exists = !string.IsNullOrWhiteSpace(company.Id) &&
                     await _companyRepository.GetByIdAsync(company.Id, cancellationToken) != null;

        if (exists)
        {
            company.UpdateTime = _clock.Now;
            company.Updator = loginUser.UserNo;
        }
        else
        {
            company.CreateTime = _clock.Now;
            company.Creator = loginUser.UserNo;
        }
        if (!exists)
        {
            company.CreateTime ??= _clock.Now;
            company.UpdateTime ??= company.CreateTime;
            company.Creator = string.IsNullOrWhiteSpace(company.Creator) ? loginUser.UserNo : company.Creator;
            company.Updator ??= string.Empty;
            company.Keyword ??= string.Empty;
            await _companyRepository.InsertAsync(company, cancellationToken);
        }
        else
        {
            company.Keyword ??= string.Empty;
            await _companyRepository.UpdateAsync(company, cancellationToken);
        }
    }

    public async Task<List<Company>> GetCompaniesAsync(Company company, CancellationToken cancellationToken = default)
    {
        return await _companyRepository.Db.Queryable<Company>()
            .WhereIF(!string.IsNullOrWhiteSpace(company.Keyword), c => c.Cname.Contains(company.Keyword) || c.Code.Contains(company.Keyword))
            .Where(c => c.DelFlag == 1)
            .OrderBy(c => c.OrderNo)
            .ToListAsync(cancellationToken);
    }

    public async Task<ReturnVo<string>> DeleteByIdsAsync(List<string> ids, CancellationToken cancellationToken = default)
    {
        if (ids == null || ids.Count == 0)
        {
            return new ReturnVo<string>(ReturnCode.SUCCESS, "OK");
        }

        var childCompanyCount = await _companyRepository.Db.Queryable<Company>()
            .Where(c => c.DelFlag == 1)
            .In(c => c.Pid, ids)
            .CountAsync(cancellationToken);
        if (childCompanyCount > 0)
        {
            return new ReturnVo<string>(ReturnCode.FAIL, "该公司还存在子公司，请确认！");
        }

        var departmentCount = await _companyRepository.Db.Queryable<Department>()
            .Where(d => d.DelFlag == 1)
            .In(d => d.CompanyId, ids)
            .CountAsync(cancellationToken);
        if (departmentCount > 0)
        {
            return new ReturnVo<string>(ReturnCode.FAIL, "该公司还存在部门数据，请确认！");
        }

        var personalCount = await _companyRepository.Db.Queryable<Personal>()
            .Where(p => p.DelFlag == 1)
            .In(p => p.CompanyId, ids)
            .CountAsync(cancellationToken);
        if (personalCount > 0)
        {
            return new ReturnVo<string>(ReturnCode.FAIL, "该公司还存在人员数据，请确认！");
        }

        await _companyRepository.Db.Updateable<Company>()
            .SetColumns(c => c.DelFlag == 0)
            .Where(c => ids.Contains(c.Id))
            .ExecuteCommandAsync(cancellationToken);
        return new ReturnVo<string>(ReturnCode.SUCCESS, "OK");
    }

}
