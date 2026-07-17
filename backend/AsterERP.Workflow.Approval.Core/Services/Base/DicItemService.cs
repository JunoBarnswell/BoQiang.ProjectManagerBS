using AsterERP.Workflow.Approval.Api.Models.Base;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Core.Repositories.Base;
using AsterERP.Workflow.Tools.Common;
using AsterERP.Workflow.Tools.Pager;
using AsterERP.Workflow.Tools.Vos;
using SqlSugar;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Approval.Core.Services.Base;

public class DicItemService : IDicItemService
{
    private readonly IDicItemRepository _repository;
    private readonly IClock _clock;

    public DicItemService(IDicItemRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<List<DicItem>> GetDicItemsByMainIdAsync(string mainId, CancellationToken cancellationToken = default)
    {
        return await _repository.Db.Queryable<DicItem>()
            .Where(d => d.MainId == mainId && d.DelFlag == 1)
            .OrderBy(d => d.OrderNo)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagerModel<DicItem>> GetPagerModelByWrapperAsync(DicItem dicItem, int pageNum, int pageSize, CancellationToken cancellationToken = default)
    {
        RefAsync<int> total = new();
        var query = _repository.Db.Queryable<DicItem>()
            .Where(d => d.MainId == dicItem.MainId && d.DelFlag == 1);
        if (!string.IsNullOrWhiteSpace(dicItem.Keyword))
        {
            query = query.Where(d => d.Code.Contains(dicItem.Keyword) || d.Cname.Contains(dicItem.Keyword) || d.Ename.Contains(dicItem.Keyword));
        }
        var list = await query.OrderBy(d => d.OrderNo)
            .ToPageListAsync(pageNum, pageSize, total, cancellationToken);
        return new PagerModel<DicItem>(total.Value, list);
    }

    public async Task SaveOrUpdateAsync(DicItem dicItem, User loginUser, CancellationToken cancellationToken = default)
    {
        var exists = !string.IsNullOrWhiteSpace(dicItem.Id)
            && await _repository.GetByIdAsync(dicItem.Id, cancellationToken) != null;

        if (exists)
        {
            dicItem.UpdateTime = _clock.Now;
            dicItem.Updator = loginUser.UserNo;
            dicItem.Keyword ??= string.Empty;
        }
        else
        {
            dicItem.CreateTime = _clock.Now;
            dicItem.UpdateTime = dicItem.CreateTime;
            dicItem.Creator = string.IsNullOrWhiteSpace(dicItem.Creator) ? loginUser.UserNo : dicItem.Creator;
            dicItem.Updator ??= string.Empty;
            dicItem.Keyword ??= string.Empty;
        }

        if (!exists)
        {
            await _repository.InsertAsync(dicItem, cancellationToken);
        }
        else
        {
            await _repository.UpdateAsync(dicItem, cancellationToken);
        }
    }

    public async Task<ReturnVo<string>> DeleteByIdsAsync(string[] ids, CancellationToken cancellationToken = default)
    {
        if (ids == null || ids.Length == 0)
        {
            return new ReturnVo<string>(ReturnCode.SUCCESS, "OK");
        }

        await _repository.Db.Updateable<DicItem>()
            .SetColumns(d => d.DelFlag == 0)
            .Where(d => ids.Contains(d.Id))
            .ExecuteCommandAsync(cancellationToken);
        return new ReturnVo<string>(ReturnCode.SUCCESS, "OK");
    }
}
