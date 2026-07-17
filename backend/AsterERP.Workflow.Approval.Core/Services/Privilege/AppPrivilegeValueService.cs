using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Core.Repositories.Privilege;
using Volo.Abp.Guids;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Approval.Core.Services.Privilege;

public class AppPrivilegeValueService : IAppPrivilegeValueService
{
    private readonly IAppPrivilegeValueRepository _repository;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guidGenerator;

    public AppPrivilegeValueService(IAppPrivilegeValueRepository repository, IClock clock, IGuidGenerator guidGenerator)
    {
        _repository = repository;
        _clock = clock;
        _guidGenerator = guidGenerator;
    }

    public async Task<List<AppPrivilegeValue>> GetAppPrivilegeValuesAsync(CancellationToken cancellationToken = default)
    {
        return await _repository.Db.Queryable<AppPrivilegeValue>()
            .Where(a => a.DelFlag == 1)
            .OrderBy(a => a.OrderNo)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveOrUpdateAsync(AppPrivilegeValue appPrivilegeValue, CancellationToken cancellationToken = default)
    {
        var exists = !string.IsNullOrWhiteSpace(appPrivilegeValue.Id)
            && await _repository.GetByIdAsync(appPrivilegeValue.Id, cancellationToken) != null;

        if (exists)
        {
            appPrivilegeValue.UpdateTime = _clock.Now;
            appPrivilegeValue.Updator ??= string.Empty;
            appPrivilegeValue.Keyword ??= string.Empty;
            await _repository.UpdateAsync(appPrivilegeValue, cancellationToken);
        }
        else
        {
            appPrivilegeValue.Id = string.IsNullOrWhiteSpace(appPrivilegeValue.Id) ? _guidGenerator.Create().ToString("N") : appPrivilegeValue.Id;
            appPrivilegeValue.CreateTime = _clock.Now;
            appPrivilegeValue.UpdateTime = appPrivilegeValue.CreateTime;
            appPrivilegeValue.Creator ??= string.Empty;
            appPrivilegeValue.Updator ??= string.Empty;
            appPrivilegeValue.Keyword ??= string.Empty;
            await _repository.InsertAsync(appPrivilegeValue, cancellationToken);
        }
    }

    public async Task DeleteByIdsAsync(List<string> appIds, CancellationToken cancellationToken = default)
    {
        if (appIds != null && appIds.Count > 0)
        {
            await _repository.Db.Deleteable<AppPrivilegeValue>()
                .In(appIds)
                .ExecuteCommandAsync(cancellationToken);
        }
    }

}
