using AsterERP.Workflow.Approval.Api.Models.Org;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Core.Repositories.Org;
using AsterERP.Workflow.Tools.Common;
using AsterERP.Workflow.Tools.Pager;
using AsterERP.Workflow.Tools.Vos;
using SqlSugar;
using Volo.Abp.Guids;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Approval.Core.Services.Org;

public class PersonalService : IPersonalService
{
    private readonly IPersonalRepository _personalRepository;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guidGenerator;

    public PersonalService(IPersonalRepository personalRepository, IClock clock, IGuidGenerator guidGenerator)
    {
        _personalRepository = personalRepository;
        _clock = clock;
        _guidGenerator = guidGenerator;
    }

    public async Task<ReturnVo<Personal>> ImportPersonalsAsync(List<Personal> personals, User loginUser, CancellationToken cancellationToken = default)
    {
        var returnVo = new ReturnVo<Personal>(ReturnCode.SUCCESS, "OK");
        if (personals == null || personals.Count == 0)
        {
            return returnVo;
        }

        var codes = personals
            .Where(p => !string.IsNullOrWhiteSpace(p.Code))
            .Select(p => p.Code!)
            .Distinct()
            .ToList();
        var deptCodes = personals
            .Where(p => !string.IsNullOrWhiteSpace(p.DeptCode))
            .Select(p => p.DeptCode!)
            .Distinct()
            .ToList();
        var companyCodes = personals
            .Where(p => !string.IsNullOrWhiteSpace(p.CompanyCode))
            .Select(p => p.CompanyCode!)
            .Distinct()
            .ToList();

        var db = _personalRepository.Db;

        var oldDbPersonals = codes.Count > 0
            ? await db.Queryable<Personal>()
                .In(p => p.Code, codes)
                .Where(p => p.DelFlag == 1)
                .ToListAsync(cancellationToken)
            : new List<Personal>();

        var companyList = companyCodes.Count > 0
            ? await db.Queryable<Company>()
                .In(c => c.Code, companyCodes)
                .Where(c => c.DelFlag == 1)
                .ToListAsync(cancellationToken)
            : new List<Company>();

        var departmentList = deptCodes.Count > 0
            ? await db.Queryable<Department>()
                .In(d => d.Code, deptCodes)
                .Where(d => d.DelFlag == 1)
                .ToListAsync(cancellationToken)
            : new List<Department>();

        var companyMap = companyList
            .Where(c => !string.IsNullOrWhiteSpace(c.Code))
            .ToDictionary(c => c.Code!, c => c);
        var departmentMap = departmentList
            .Where(d => !string.IsNullOrWhiteSpace(d.Code))
            .ToDictionary(d => d.Code!, d => d);
        var oldDbPersonalMap = oldDbPersonals
            .Where(p => !string.IsNullOrWhiteSpace(p.Code))
            .ToDictionary(p => p.Code!, p => p);

        var inserts = new List<Personal>();
        var updates = new List<Personal>();
        var now = _clock.Now;

        foreach (var personal in personals)
        {
            if (!string.IsNullOrWhiteSpace(personal.CompanyCode) &&
                companyMap.TryGetValue(personal.CompanyCode, out var company))
            {
                personal.CompanyId = company.Id;
                personal.CompanyName = company.Cname;
            }

            if (!string.IsNullOrWhiteSpace(personal.DeptCode) &&
                departmentMap.TryGetValue(personal.DeptCode, out var department))
            {
                personal.DeptId = department.Id;
                personal.DeptName = department.Name;
            }

            if (string.IsNullOrWhiteSpace(personal.Code) || !oldDbPersonalMap.ContainsKey(personal.Code))
            {
                personal.Id = _guidGenerator.Create().ToString("N");
                personal.CreateTime = now;
                personal.Creator = loginUser.UserNo;
                personal.Keyword ??= string.Empty;
                personal.DelFlag ??= 1;
                inserts.Add(personal);
            }
            else
            {
                var oldPersonal = oldDbPersonalMap[personal.Code!];
                ConvertPersonal(oldPersonal, personal);
                oldPersonal.UpdateTime = now;
                oldPersonal.Updator = loginUser.UserNo;
                updates.Add(oldPersonal);
            }
        }

        if (inserts.Count > 0)
        {
            await db.Insertable(inserts).ExecuteCommandAsync(cancellationToken);
        }

        if (updates.Count > 0)
        {
            await db.Updateable(updates).ExecuteCommandAsync(cancellationToken);
        }

        return returnVo;
    }

    private static void ConvertPersonal(Personal oldPersonal, Personal newPersonal)
    {
        if (oldPersonal == null || newPersonal == null)
        {
            return;
        }

        oldPersonal.Code = newPersonal.Code;
        oldPersonal.Email = newPersonal.Email;
        oldPersonal.Fax = newPersonal.Fax;
        oldPersonal.JobGradeCode = newPersonal.JobGradeCode;
        oldPersonal.JobGradeName = newPersonal.JobGradeName;
        oldPersonal.LeaderCode = newPersonal.LeaderCode;
        oldPersonal.LeaderName = newPersonal.LeaderName;
        oldPersonal.Mobile = newPersonal.Mobile;
        oldPersonal.Name = newPersonal.Name;
        oldPersonal.PositionCode = newPersonal.PositionCode;
        oldPersonal.PositionName = newPersonal.PositionName;
        oldPersonal.Sex = newPersonal.Sex;
    }

    public async Task<PagerModel<Personal>> GetPagerModelByWrapperAsync(Personal personal, int pageNum, int pageSize, CancellationToken cancellationToken = default)
    {
        return await GetPagerModelByWrapperAsync(personal, pageNum, pageSize, false, cancellationToken);
    }

    public async Task<PagerModel<Personal>> GetPagerModelByWrapperAsync(Personal personal, int pageNum, int pageSize, bool showRoles, CancellationToken cancellationToken = default)
    {
        RefAsync<int> total = new();
        var list = await _personalRepository.Db.Queryable<Personal>()
            .WhereIF(!string.IsNullOrWhiteSpace(personal.Keyword), p => p.Name.Contains(personal.Keyword) || p.Code.Contains(personal.Keyword) || p.Mobile.Contains(personal.Keyword))
            .Where(p => p.DelFlag == 1)
            .ToPageListAsync(pageNum, pageSize, total, cancellationToken);

        if (showRoles && list.Count > 0)
        {
            var personalIds = list
                .Where(p => !string.IsNullOrWhiteSpace(p.Id))
                .Select(p => p.Id!)
                .Distinct()
                .ToList();
            if (personalIds.Count > 0)
            {
                var personalRoles = await _personalRepository.Db.Queryable<PersonalRole>()
                    .In(pr => pr.PersonalId, personalIds)
                    .ToListAsync(cancellationToken);
                var roleIds = personalRoles
                    .Where(pr => !string.IsNullOrWhiteSpace(pr.RoleId))
                    .Select(pr => pr.RoleId!)
                    .Distinct()
                    .ToList();
                if (roleIds.Count > 0)
                {
                    var roles = await _personalRepository.Db.Queryable<Role>()
                        .In(r => r.Id, roleIds)
                        .Where(r => r.DelFlag == 1)
                        .ToListAsync(cancellationToken);
                    var roleMap = roles.ToDictionary(r => r.Id!, r => r);
                    var personalRoleMap = personalRoles
                        .Where(pr => !string.IsNullOrWhiteSpace(pr.PersonalId) && !string.IsNullOrWhiteSpace(pr.RoleId))
                        .GroupBy(pr => pr.PersonalId!)
                        .ToDictionary(
                            group => group.Key,
                            group => group
                                .Where(pr => roleMap.ContainsKey(pr.RoleId!))
                                .Select(pr =>
                                {
                                    var source = roleMap[pr.RoleId!];
                                    return new Role
                                    {
                                        Id = source.Id,
                                        CompanyId = source.CompanyId,
                                        PositionId = source.PositionId,
                                        Name = source.Name,
                                        Type = source.Type,
                                        Sn = source.Sn,
                                        Note = source.Note,
                                        OrderNo = source.OrderNo,
                                        CreateTime = source.CreateTime,
                                        Creator = source.Creator,
                                        UpdateTime = source.UpdateTime,
                                        Updator = source.Updator,
                                        DelFlag = source.DelFlag,
                                        CompanyName = source.CompanyName,
                                        PersonalId = group.Key
                                    };
                                })
                                .ToList());

                    foreach (var item in list)
                    {
                        if (!string.IsNullOrWhiteSpace(item.Id) &&
                            personalRoleMap.TryGetValue(item.Id, out var personalRolesForPersonal) &&
                            personalRolesForPersonal.Count > 0)
                        {
                            item.Roles = personalRolesForPersonal;
                        }
                    }
                }
            }
        }
        return new PagerModel<Personal>(total.Value, list);
    }

    public async Task<List<Personal>> GetPersonalsByCodesAsync(List<string> codes, CancellationToken cancellationToken = default)
    {
        if (codes == null || codes.Count == 0) return new List<Personal>();
        return await _personalRepository.Db.Queryable<Personal>()
            .In(p => p.Code, codes)
            .Where(p => p.DelFlag == 1)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Personal>> GetPersonalsByRoleIdsAsync(List<string> roleIds, CancellationToken cancellationToken = default)
    {
        if (roleIds == null || roleIds.Count == 0) return new List<Personal>();
        return await _personalRepository.GetPersonalsByRoleIdsAsync(roleIds, cancellationToken);
    }

    public async Task<List<Personal>> GetPersonalsByRoleSnsAsync(List<string> roleSns, CancellationToken cancellationToken = default)
    {
        if (roleSns == null || roleSns.Count == 0) return new List<Personal>();
        return await _personalRepository.GetPersonalsByRoleSnsAsync(roleSns, cancellationToken);
    }

    public async Task<Personal?> GetPersonalByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        return await _personalRepository.Db.Queryable<Personal>()
            .FirstAsync(p => p.Code == code && p.DelFlag == 1, cancellationToken);
    }

    public async Task DeleteByIdsAsync(List<string> ids, CancellationToken cancellationToken = default)
    {
        await _personalRepository.Db.Deleteable<PersonalRole>()
            .In(pr => pr.PersonalId, ids)
            .ExecuteCommandAsync(cancellationToken);
        await _personalRepository.Db.Updateable<Personal>()
            .SetColumns(p => p.DelFlag == 0)
            .Where(p => ids.Contains(p.Id))
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task SaveOrUpdateAsync(Personal personal, User loginUser, CancellationToken cancellationToken = default)
    {
        var exists = !string.IsNullOrWhiteSpace(personal.Id)
            && await _personalRepository.GetByIdAsync(personal.Id, cancellationToken) != null;

        if (exists)
        {
            personal.UpdateTime = _clock.Now;
            personal.Updator = loginUser.UserNo;
            personal.Keyword ??= string.Empty;
        }
        else
        {
            personal.Id = string.IsNullOrWhiteSpace(personal.Id) ? _guidGenerator.Create().ToString("N") : personal.Id;
            personal.CreateTime = _clock.Now;
            personal.UpdateTime = personal.CreateTime;
            personal.Creator = string.IsNullOrWhiteSpace(personal.Creator) ? loginUser.UserNo : personal.Creator;
            personal.Updator ??= string.Empty;
            personal.Keyword ??= string.Empty;
        }

        if (!exists)
        {
            await _personalRepository.InsertAsync(personal, cancellationToken);
        }
        else
        {
            await _personalRepository.UpdateAsync(personal, cancellationToken);
        }
    }

    public async Task<Personal?> GetPersonalByThirdUserIdAsync(string thirdUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(thirdUserId)) return null;
        return await _personalRepository.Db.Queryable<Personal>()
            .FirstAsync(p => p.ThirdUserId == thirdUserId && p.DelFlag == 1, cancellationToken);
    }
}
