using AsterERP.Workflow.Approval.Api.Models.Org;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Api.ViewModels.Org;
using AsterERP.Workflow.Approval.Core.Repositories.Org;
using SqlSugar;
using Volo.Abp.Guids;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Approval.Core.Services.Org;

public class PersonalRoleService : IPersonalRoleService
{
    private readonly IPersonalRoleRepository _personalRoleRepository;
    private readonly IPersonalRepository _personalRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guidGenerator;

    public PersonalRoleService(
        IPersonalRoleRepository personalRoleRepository,
        IPersonalRepository personalRepository,
        IRoleRepository roleRepository,
        IClock clock,
        IGuidGenerator guidGenerator)
    {
        _personalRoleRepository = personalRoleRepository;
        _personalRepository = personalRepository;
        _roleRepository = roleRepository;
        _clock = clock;
        _guidGenerator = guidGenerator;
    }

    public async Task AddPersonalRolesByPersonalAsync(string personalId, List<Role> roles, User loginUser, CancellationToken cancellationToken = default)
    {
        var prs = new List<PersonalRole>();
        var personal = string.IsNullOrWhiteSpace(personalId)
            ? null
            : await _personalRepository.GetByIdAsync(personalId, cancellationToken);
        if (roles != null && roles.Count > 0 && personal != null)
        {
            foreach (var role in roles)
            {
                var pr = new PersonalRole
                {
                    Id = _guidGenerator.Create().ToString("N"),
                    PersonalId = personal.Id,
                    PersonalCode = personal.Code,
                    RoleId = role.Id,
                    EndDate = DateTime.MinValue,
                    CreateTime = _clock.Now,
                    Creator = loginUser.UserNo,
                    UpdateTime = _clock.Now,
                    Updator = loginUser.UserNo,
                    Keyword = string.Empty
                };
                prs.Add(pr);
            }
            await _personalRoleRepository.Db.Insertable(prs).ExecuteCommandAsync(cancellationToken);
        }
    }

    public async Task AddPersonalRolesByRoleAsync(string roleId, List<Personal> personals, User loginUser, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(roleId))
        {
            await _personalRoleRepository.Db.Deleteable<PersonalRole>()
                .Where(pr => pr.RoleId == roleId)
                .ExecuteCommandAsync(cancellationToken);
        }
        var prs = new List<PersonalRole>();
        if (personals != null && personals.Count > 0 && !string.IsNullOrWhiteSpace(roleId))
        {
            foreach (var personal in personals)
            {
                var pr = new PersonalRole
                {
                    Id = _guidGenerator.Create().ToString("N"),
                    PersonalId = personal.Id,
                    PersonalCode = personal.Code,
                    RoleId = roleId,
                    EndDate = DateTime.MinValue,
                    CreateTime = _clock.Now,
                    Creator = loginUser.UserNo,
                    UpdateTime = _clock.Now,
                    Updator = loginUser.UserNo,
                    Keyword = string.Empty
                };
                prs.Add(pr);
            }
            await _personalRoleRepository.Db.Insertable(prs).ExecuteCommandAsync(cancellationToken);
        }
    }

    public async Task<List<RolePersonalVo>> GetRolePersonalsAsync(PersonalRole personalRole, CancellationToken cancellationToken = default)
    {
        var query = _personalRoleRepository.Db.Queryable<PersonalRole, Personal>(
                (roleBinding, personal) => new JoinQueryInfos(
                    JoinType.Inner,
                    roleBinding.PersonalCode == personal.Code))
            .Where((roleBinding, personal) => roleBinding.DelFlag == 1 && personal.DelFlag == 1)
            .WhereIF(!string.IsNullOrWhiteSpace(personalRole.RoleId), (roleBinding, personal) => roleBinding.RoleId == personalRole.RoleId)
            .WhereIF(!string.IsNullOrWhiteSpace(personalRole.PersonalId), (roleBinding, personal) => roleBinding.PersonalId == personalRole.PersonalId)
            .WhereIF(!string.IsNullOrWhiteSpace(personalRole.PersonalCode), (roleBinding, personal) => roleBinding.PersonalCode == personalRole.PersonalCode);

        var roleName = string.IsNullOrWhiteSpace(personalRole.RoleId)
            ? null
            : await _roleRepository.Db.Queryable<Role>()
                .Where(role => role.DelFlag == 1 && role.Id == personalRole.RoleId)
                .Select(role => role.Name)
                .FirstAsync(cancellationToken);

        return await query
            .Select((roleBinding, personal) => new RolePersonalVo
            {
                Id = roleBinding.Id,
                PersonalId = personal.Id,
                RoleId = roleBinding.RoleId,
                RoleName = roleName,
                Code = personal.Code,
                Name = personal.Name,
                DeptId = personal.DeptId,
                DeptName = personal.DeptName,
                CompanyId = personal.CompanyId,
                CompanyName = personal.CompanyName
            })
            .ToListAsync(cancellationToken);
    }
}
