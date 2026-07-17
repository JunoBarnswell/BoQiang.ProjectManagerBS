using AsterERP.Workflow.Approval.Api.Exceptions;
using AsterERP.Workflow.Approval.Api.Models.Org;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using AsterERP.Workflow.Approval.Core.Repositories.Org;
using Volo.Abp.Guids;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Approval.Core.Services.Org;

public class RolePositionPersonalService : IRolePositionPersonalService
{
    private readonly IRolePositionPersonalRepository _repository;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guidGenerator;

    public RolePositionPersonalService(IRolePositionPersonalRepository repository, IClock clock, IGuidGenerator guidGenerator)
    {
        _repository = repository;
        _clock = clock;
        _guidGenerator = guidGenerator;
    }

    public async Task SaveOrUpdateBatchAsync(List<RolePositionPersonal> rolePositionPersonalList, User user, CancellationToken cancellationToken = default)
    {
        if (rolePositionPersonalList == null || rolePositionPersonalList.Count == 0)
        {
            return;
        }

        var candidateIds = rolePositionPersonalList
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .Select(item => item.Id!)
            .Distinct()
            .ToList();
        var existingIds = candidateIds.Count == 0
            ? new HashSet<string>(StringComparer.Ordinal)
            : (await _repository.Db.Queryable<RolePositionPersonal>()
                    .Where(item => candidateIds.Contains(item.Id))
                    .Select(item => item.Id)
                    .ToListAsync(cancellationToken))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!)
                .ToHashSet(StringComparer.Ordinal);

        var inserts = new List<RolePositionPersonal>();
        var updates = new List<RolePositionPersonal>();
        foreach (var item in rolePositionPersonalList)
        {
            if (!string.IsNullOrWhiteSpace(item.Id) && existingIds.Contains(item.Id))
            {
                item.UpdateTime = _clock.Now;
                item.Updator = user.UserNo;
                item.Keyword ??= string.Empty;
                updates.Add(item);
            }
            else
            {
                item.Id = string.IsNullOrWhiteSpace(item.Id) ? _guidGenerator.Create().ToString("N") : item.Id;
                item.CreateTime = item.CreateTime ?? _clock.Now;
                item.UpdateTime = _clock.Now;
                item.Creator = string.IsNullOrWhiteSpace(item.Creator) ? user.UserNo : item.Creator;
                item.Updator ??= string.Empty;
                item.Keyword ??= string.Empty;
                inserts.Add(item);
            }
        }

        if (updates.Count > 0)
        {
            await _repository.Db.Updateable(updates).ExecuteCommandAsync(cancellationToken);
        }

        if (inserts.Count > 0)
        {
            await _repository.Db.Insertable(inserts).ExecuteCommandAsync(cancellationToken);
        }
    }

    public async Task<List<Personal>> GetPersonalByRoleIdAndPositionCodeAsync(string roleId, string positionCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roleId))
        {
            throw new ValidationException("请输入角色id", ErrorCodes.ParameterInvalid);
        }
        return await _repository.GetPersonalByRoleIdAndPositionIdAsync(roleId, positionCode, cancellationToken);
    }
}
