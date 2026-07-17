using AsterERP.Workflow.Approval.Api.Models.Org;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Api.ViewModels.Org;
using AsterERP.Workflow.Approval.Core.Repositories.Org;
using AsterERP.Workflow.Tools.Common;
using AsterERP.Workflow.Tools.Pager;
using AsterERP.Workflow.Tools.Vos;
using SqlSugar;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Approval.Core.Services.Org;

public class DepartmentService : IDepartmentService
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ICompanyService _companyService;
    private readonly IClock _clock;

    public DepartmentService(IDepartmentRepository departmentRepository, ICompanyService companyService, IClock clock)
    {
        _departmentRepository = departmentRepository;
        _companyService = companyService;
        _clock = clock;
    }

    public async Task<ReturnVo<List<Department>>> ImportDepartmentAsync(List<Department> departments, User loginUser, CancellationToken cancellationToken = default)
    {
        var returnVo = new ReturnVo<List<Department>>(ReturnCode.SUCCESS, "OK");
        if (departments != null && departments.Count > 0)
        {
            foreach (var dept in departments)
            {
                await SaveOrUpdateAsync(dept, loginUser, cancellationToken);
            }
        }
        return returnVo;
    }

    public async Task SaveOrUpdateAsync(Department department, User loginUser, CancellationToken cancellationToken = default)
    {
        var exists = !string.IsNullOrWhiteSpace(department.Id)
            && await _departmentRepository.GetByIdAsync(department.Id, cancellationToken) != null;

        if (exists)
        {
            department.UpdateTime = _clock.Now;
            department.Updator = loginUser.UserNo;
        }
        else
        {
            department.CreateTime = _clock.Now;
            department.UpdateTime = department.CreateTime;
            department.Creator = loginUser.UserNo;
            department.Updator ??= string.Empty;
            department.Keyword ??= string.Empty;
        }
        if (!exists)
        {
            await _departmentRepository.InsertAsync(department, cancellationToken);
        }
        else
        {
            await _departmentRepository.UpdateAsync(department, cancellationToken);
        }
    }

    public async Task<ReturnVo<string>> DeleteByIdsAsync(List<string> ids, CancellationToken cancellationToken = default)
    {
        if (ids == null || ids.Count == 0)
        {
            return new ReturnVo<string>(ReturnCode.SUCCESS, "OK");
        }

        var childDepartmentCount = await _departmentRepository.Db.Queryable<Department>()
            .Where(d => d.DelFlag == 1)
            .In(d => d.Pid, ids)
            .CountAsync(cancellationToken);
        if (childDepartmentCount > 0)
        {
            return new ReturnVo<string>(ReturnCode.FAIL, "该部门还存在子部门，请确认！");
        }

        var personalCount = await _departmentRepository.Db.Queryable<Personal>()
            .Where(p => p.DelFlag == 1)
            .In(p => p.DeptId, ids)
            .CountAsync(cancellationToken);
        if (personalCount > 0)
        {
            return new ReturnVo<string>(ReturnCode.FAIL, "该部门还存在人员数据，请确认！");
        }

        await _departmentRepository.Db.Updateable<Department>()
            .SetColumns(d => d.DelFlag == 0)
            .Where(d => ids.Contains(d.Id))
            .ExecuteCommandAsync(cancellationToken);
        return new ReturnVo<string>(ReturnCode.SUCCESS, "OK");
    }

    public async Task<PagerModel<Department>> GetPagerModelByWrapperAsync(Department department, int pageNum, int pageSize, CancellationToken cancellationToken = default)
    {
        RefAsync<int> total = new();
        var list = await _departmentRepository.Db.Queryable<Department>()
            .WhereIF(!string.IsNullOrWhiteSpace(department.Keyword), d => d.Name.Contains(department.Keyword) || d.Code.Contains(department.Keyword))
            .Where(d => d.DelFlag == 1)
            .OrderBy(d => d.OrderNo)
            .ToPageListAsync(pageNum, pageSize, total, cancellationToken);
        return new PagerModel<Department>(total.Value, list);
    }

    public async Task<List<OrgTreeVo>> GetOrgTreeAsync(CancellationToken cancellationToken = default)
    {
        var companies = await _companyService.GetCompaniesAsync(new Company(), cancellationToken);
        var departments = await _departmentRepository.Db.Queryable<Department>()
            .Where(d => d.DelFlag == 1)
            .OrderBy(d => d.OrderNo)
            .ToListAsync(cancellationToken);
        var tree = new List<OrgTreeVo>();

        foreach (var company in companies)
        {
            tree.Add(new OrgTreeVo { Id = company.Id, Pid = company.Pid, Code = company.Code, Name = company.Cname, SourceType = "1" });
        }
        foreach (var dept in departments)
        {
            tree.Add(new OrgTreeVo { Id = dept.Id, Pid = dept.Pid, Code = dept.Code, Name = dept.Name, SourceType = "2" });
        }
        return tree;
    }

    public async Task<List<OrgTreeVo>> GetDepartmentTreeAsync(string companyId, string deptName, CancellationToken cancellationToken = default)
    {
        var departments = await _departmentRepository.Db.Queryable<Department>()
            .WhereIF(!string.IsNullOrWhiteSpace(companyId), d => d.CompanyId == companyId)
            .WhereIF(!string.IsNullOrWhiteSpace(deptName), d => d.Name.Contains(deptName))
            .Where(d => d.DelFlag == 1)
            .OrderBy(d => d.OrderNo)
            .ToListAsync(cancellationToken);

        return departments.Select(d => new OrgTreeVo
        {
            Id = d.Id,
            Pid = d.Pid,
            Code = d.Code,
            Name = d.Name,
            SourceType = "2"
        }).ToList();
    }
}
