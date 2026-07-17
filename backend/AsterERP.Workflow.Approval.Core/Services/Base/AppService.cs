using AsterERP.Workflow.Approval.Api.Models.Base;
using AsterERP.Workflow.Approval.Core.Repositories.Base;
using AsterERP.Workflow.Tools.Pager;
using SqlSugar;
using Volo.Abp.Guids;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Approval.Core.Services.Base;

public class AppService : IAppService
{
    private readonly IAppRepository _appRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IClock _clock;

    public AppService(IAppRepository appRepository, IClock clock, IGuidGenerator guidGenerator)
    {
        _appRepository = appRepository;
        _clock = clock;
        _guidGenerator = guidGenerator;
    }

    public async Task<List<App>> GetActiveAppsAsync(CancellationToken cancellationToken = default)
    {
        return await _appRepository.Db.Queryable<App>()
            .Where(a => a.Status == 1 && a.DelFlag == 1)
            .OrderBy(a => a.OrderNo)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagerModel<App>> GetPagerModelByWrapperAsync(App app, int pageNum, int pageSize, CancellationToken cancellationToken = default)
    {
        RefAsync<int> total = new();
        var query = _appRepository.Db.Queryable<App>()
            .WhereIF(!string.IsNullOrWhiteSpace(app.Keyword), a => a.Name.Contains(app.Keyword) || a.Sn.Contains(app.Keyword))
            .WhereIF(app.Status != null, a => a.Status == app.Status)
            .Where(a => a.DelFlag == 1)
            .OrderBy(a => a.OrderNo);
        var list = await query.ToPageListAsync(pageNum, pageSize, total, cancellationToken);
        return new PagerModel<App>(total.Value, list);
    }

    public async Task SaveOrUpdateAppAsync(App app, CancellationToken cancellationToken = default)
    {
        var existingApp = !string.IsNullOrWhiteSpace(app.Id)
            ? await _appRepository.GetByIdAsync(app.Id, cancellationToken)
            : null;
        var exists = existingApp != null;

        if (!exists)
        {
            app.SecretKey = string.IsNullOrWhiteSpace(app.SecretKey) ? CreateSecretKey() : app.SecretKey;
            app.CreateTime ??= _clock.Now;
            app.UpdateTime ??= app.CreateTime;
            app.Creator ??= string.Empty;
            app.Updator ??= string.Empty;
            app.Keyword ??= string.Empty;
            await _appRepository.InsertAsync(app, cancellationToken);
        }
        else
        {
            app.UpdateTime = _clock.Now;
            app.SecretKey = string.IsNullOrWhiteSpace(app.SecretKey) ? existingApp!.SecretKey : app.SecretKey;
            app.Updator ??= string.Empty;
            app.Keyword ??= string.Empty;
            await _appRepository.UpdateAsync(app, cancellationToken);
        }
    }

    public async Task<string> UpdateSecretKeyAsync(string appId, CancellationToken cancellationToken = default)
    {
        var secretKey = CreateSecretKey();
        await _appRepository.Db.Updateable<App>()
            .SetColumns(a => a.SecretKey == secretKey)
            .Where(a => a.Id == appId)
            .ExecuteCommandAsync(cancellationToken);
        return secretKey;
    }

    private string CreateSecretKey()
    {
        return _guidGenerator.Create().ToString("N") + _guidGenerator.Create().ToString("N");
    }
}
