using AsterERP.Workflow.Approval.Api.Models.Base;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Core.Repositories.Base;
using AsterERP.Workflow.Tools.Common;
using AsterERP.Workflow.Tools.Pager;
using AsterERP.Workflow.Tools.Vos;
using SqlSugar;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Approval.Core.Services.Base;

public class SystemConfigService : ISystemConfigService
{
    private readonly ISystemConfigRepository _repository;
    private readonly IClock _clock;

    public SystemConfigService(ISystemConfigRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<PagerModel<SystemConfig>> GetPagerModelByWrapperAsync(SystemConfig systemConfig, int pageNum, int pageSize, CancellationToken cancellationToken = default)
    {
        RefAsync<int> total = new();
        var query = _repository.Db.Queryable<SystemConfig>()
            .Where(s => s.DelFlag == 1);
        if (!string.IsNullOrWhiteSpace(systemConfig.Keyword))
        {
            query = query.Where(s => s.ConfigKey.Contains(systemConfig.Keyword) || s.ConfigName.Contains(systemConfig.Keyword));
        }
        var list = await query.OrderBy(s => s.ConfigOrder)
            .ToPageListAsync(pageNum, pageSize, total, cancellationToken);
        return new PagerModel<SystemConfig>(total.Value, list);
    }

    public async Task SaveOrUpdateAsync(SystemConfig systemConfig, User loginUser, CancellationToken cancellationToken = default)
    {
        var exists = !string.IsNullOrWhiteSpace(systemConfig.Id)
            && await _repository.GetByIdAsync(systemConfig.Id, cancellationToken) != null;

        systemConfig.Image ??= Array.Empty<byte>();

        if (exists)
        {
            systemConfig.UpdateTime = _clock.Now;
            systemConfig.Updator = loginUser.UserNo;
            systemConfig.Keyword ??= string.Empty;
        }
        else
        {
            systemConfig.CreateTime = _clock.Now;
            systemConfig.UpdateTime = systemConfig.CreateTime;
            systemConfig.Creator = string.IsNullOrWhiteSpace(systemConfig.Creator) ? loginUser.UserNo : systemConfig.Creator;
            systemConfig.Updator ??= string.Empty;
            systemConfig.Keyword ??= string.Empty;
        }

        if (!exists)
        {
            await _repository.InsertAsync(systemConfig, cancellationToken);
        }
        else
        {
            await _repository.UpdateAsync(systemConfig, cancellationToken);
        }
    }

    public async Task<ReturnVo<string>> DeleteByIdsAsync(string[] ids, CancellationToken cancellationToken = default)
    {
        await _repository.Db.Updateable<SystemConfig>()
            .SetColumns(s => s.DelFlag == 0)
            .Where(s => ids.Contains(s.Id))
            .ExecuteCommandAsync(cancellationToken);
        return new ReturnVo<string>(ReturnCode.SUCCESS, "OK");
    }
}
