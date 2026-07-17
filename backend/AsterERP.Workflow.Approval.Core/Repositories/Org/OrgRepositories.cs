using AsterERP.Workflow.Api.Shared;
using AsterERP.Workflow.Approval.Api.Models.Org;
using SqlSugar;

namespace AsterERP.Workflow.Approval.Core.Repositories.Org;

public class CompanyRepository : SqlSugarRepository<Company>, ICompanyRepository
{
    public CompanyRepository(ISqlSugarClient db) : base(db)
    {
    }
}

public class DepartmentRepository : SqlSugarRepository<Department>, IDepartmentRepository
{
    public DepartmentRepository(ISqlSugarClient db) : base(db)
    {
    }

    public async Task<List<Department>> GetDepartmentsAsync(Department department, CancellationToken cancellationToken = default)
    {
        var query = Db.Queryable<Department>()
            .Where(d => d.DelFlag == 1)
            .WhereIF(!string.IsNullOrWhiteSpace(department.Keyword), d => d.Name!.Contains(department.Keyword!) || d.Code!.Contains(department.Keyword!))
            .WhereIF(!string.IsNullOrWhiteSpace(department.Name), d => d.Name == department.Name)
            .WhereIF(!string.IsNullOrWhiteSpace(department.Code), d => d.Code == department.Code)
            .WhereIF(!string.IsNullOrWhiteSpace(department.CompanyId), d => d.CompanyId == department.CompanyId)
            .WhereIF(!string.IsNullOrWhiteSpace(department.Pid), d => d.Pid == department.Pid);

        if (department.CompanyIds != null && department.CompanyIds.Count > 0)
        {
            query = query.In(d => d.CompanyId, department.CompanyIds);
        }

        return await query.OrderBy(d => d.OrderNo).ToListAsync(cancellationToken);
    }

    public async Task<RefAsync<Page<Department>>> GetPagerModelAsync(Department department, int pageNum, int pageSize, CancellationToken cancellationToken = default)
    {
        RefAsync<int> total = new();
        var content = await Db.Queryable<Department>()
            .Where(d => d.DelFlag == 1)
            .WhereIF(!string.IsNullOrWhiteSpace(department.Keyword), d => d.Name!.Contains(department.Keyword!) || d.Code!.Contains(department.Keyword!))
            .WhereIF(!string.IsNullOrWhiteSpace(department.CompanyId), d => d.CompanyId == department.CompanyId)
            .WhereIF(!string.IsNullOrWhiteSpace(department.Pid), d => d.Pid == department.Pid)
            .OrderBy(d => d.OrderNo)
            .ToPageListAsync(pageNum, pageSize, total, cancellationToken);

        return BuildPage(content, total.Value);
    }

    private static RefAsync<Page<T>> BuildPage<T>(List<T> content, int totalElements) where T : class, new()
    {
        return new RefAsync<Page<T>>
        {
            Value = new Page<T>
            {
                Content = content,
                TotalElements = totalElements
            }
        };
    }
}

public class JobGradeRepository : SqlSugarRepository<JobGrade>, IJobGradeRepository
{
    public JobGradeRepository(ISqlSugarClient db) : base(db)
    {
    }
}

public class JobGradeTypeRepository : SqlSugarRepository<JobGradeType>, IJobGradeTypeRepository
{
    public JobGradeTypeRepository(ISqlSugarClient db) : base(db)
    {
    }
}

public class PersonalRepository : SqlSugarRepository<Personal>, IPersonalRepository
{
    public PersonalRepository(ISqlSugarClient db) : base(db)
    {
    }

    public async Task<List<Personal>> GetPersonalsByRoleSnsAsync(List<string> roleSns, CancellationToken cancellationToken = default)
    {
        if (roleSns == null || roleSns.Count == 0)
        {
            return new List<Personal>();
        }

        return await Db.Queryable<Personal>()
            .Where(p => p.DelFlag == 1)
            .Where(p => SqlFunc.Subqueryable<PersonalRole>()
                .Where(pr => pr.DelFlag == 1 && pr.PersonalId == p.Id)
                .Where(pr => SqlFunc.Subqueryable<Role>()
                    .Where(r => r.DelFlag == 1 && roleSns.Contains(r.Sn) && r.Id == pr.RoleId)
                    .Any())
                .Any())
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Personal>> GetPersonalsByRoleIdsAsync(List<string> roleIds, CancellationToken cancellationToken = default)
    {
        if (roleIds == null || roleIds.Count == 0)
        {
            return new List<Personal>();
        }

        return await Db.Queryable<Personal>()
            .Where(p => p.DelFlag == 1)
            .Where(p => SqlFunc.Subqueryable<PersonalRole>()
                .Where(pr => pr.DelFlag == 1 && roleIds.Contains(pr.RoleId) && pr.PersonalId == p.Id)
                .Any())
            .ToListAsync(cancellationToken);
    }

    public async Task<RefAsync<Page<Personal>>> GetPagerModelAsync(Personal personal, int pageNum, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = Db.Queryable<Personal>()
            .Where(p => p.DelFlag == 1)
            .WhereIF(!string.IsNullOrWhiteSpace(personal.Keyword), p => p.Name!.Contains(personal.Keyword!) || p.Code!.Contains(personal.Keyword!) || p.Mobile!.Contains(personal.Keyword!))
            .WhereIF(!string.IsNullOrWhiteSpace(personal.Name), p => p.Name!.Contains(personal.Name!))
            .WhereIF(!string.IsNullOrWhiteSpace(personal.Code), p => p.Code == personal.Code)
            .WhereIF(!string.IsNullOrWhiteSpace(personal.CompanyId), p => p.CompanyId == personal.CompanyId)
            .WhereIF(!string.IsNullOrWhiteSpace(personal.DeptId), p => p.DeptId == personal.DeptId)
            .WhereIF(!string.IsNullOrWhiteSpace(personal.PositionCode), p => p.PositionCode == personal.PositionCode)
            .WhereIF(!string.IsNullOrWhiteSpace(personal.JobGradeCode), p => p.JobGradeCode == personal.JobGradeCode);

        if (personal.CompanyIds != null && personal.CompanyIds.Count > 0)
        {
            query = query.In(p => p.CompanyId, personal.CompanyIds);
        }

        if (personal.DeptIds != null && personal.DeptIds.Count > 0)
        {
            query = query.In(p => p.DeptId, personal.DeptIds);
        }

        RefAsync<int> total = new();
        var content = await query
            .OrderBy(p => p.CreateTime, OrderByType.Desc)
            .ToPageListAsync(pageNum, pageSize, total, cancellationToken);

        return BuildPage(content, total.Value);
    }

    private static RefAsync<Page<T>> BuildPage<T>(List<T> content, int totalElements) where T : class, new()
    {
        return new RefAsync<Page<T>>
        {
            Value = new Page<T>
            {
                Content = content,
                TotalElements = totalElements
            }
        };
    }
}

public class PersonalRoleRepository : SqlSugarRepository<PersonalRole>, IPersonalRoleRepository
{
    public PersonalRoleRepository(ISqlSugarClient db) : base(db)
    {
    }
}

public class PositionInfoRepository : SqlSugarRepository<PositionInfo>, IPositionInfoRepository
{
    public PositionInfoRepository(ISqlSugarClient db) : base(db)
    {
    }

    public async Task<RefAsync<Page<PositionInfo>>> GetPagerModelAsync(PositionInfo positionInfo, int pageNum, int pageSize, CancellationToken cancellationToken = default)
    {
        RefAsync<int> total = new();
        var content = await Db.Queryable<PositionInfo>()
            .Where(p => p.DelFlag == 1)
            .WhereIF(!string.IsNullOrWhiteSpace(positionInfo.Keyword), p => p.Name!.Contains(positionInfo.Keyword!) || p.Code!.Contains(positionInfo.Keyword!))
            .WhereIF(!string.IsNullOrWhiteSpace(positionInfo.Name), p => p.Name!.Contains(positionInfo.Name!))
            .WhereIF(!string.IsNullOrWhiteSpace(positionInfo.Code), p => p.Code == positionInfo.Code)
            .WhereIF(!string.IsNullOrWhiteSpace(positionInfo.PositionSeqId), p => p.PositionSeqId == positionInfo.PositionSeqId)
            .WhereIF(!string.IsNullOrWhiteSpace(positionInfo.SuperiorCode), p => p.SuperiorCode == positionInfo.SuperiorCode)
            .OrderBy(p => p.OrderNo)
            .ToPageListAsync(pageNum, pageSize, total, cancellationToken);

        return new RefAsync<Page<PositionInfo>>
        {
            Value = new Page<PositionInfo>
            {
                Content = content,
                TotalElements = total.Value
            }
        };
    }
}

public class PositionSeqRepository : SqlSugarRepository<PositionSeq>, IPositionSeqRepository
{
    public PositionSeqRepository(ISqlSugarClient db) : base(db)
    {
    }
}

public class RolePositionPersonalRepository : SqlSugarRepository<RolePositionPersonal>, IRolePositionPersonalRepository
{
    public RolePositionPersonalRepository(ISqlSugarClient db) : base(db)
    {
    }

    public async Task<List<Personal>> GetPersonalByRoleIdAndPositionIdAsync(string roleId, string positionCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roleId) || string.IsNullOrWhiteSpace(positionCode))
        {
            return new List<Personal>();
        }

        return await Db.Queryable<Personal>()
            .Where(p => p.DelFlag == 1)
            .Where(p => SqlFunc.Subqueryable<RolePositionPersonal>()
                .Where(rpp =>
                    rpp.DelFlag == 1 &&
                    rpp.RoleId == roleId &&
                    rpp.PositionCode == positionCode &&
                    rpp.PersonalId == p.Id)
                .Any())
            .ToListAsync(cancellationToken);
    }
}

public class RoleRepository : SqlSugarRepository<Role>, IRoleRepository
{
    public RoleRepository(ISqlSugarClient db) : base(db)
    {
    }
}
