using AsterERP.Workflow.Approval.Api.Models.Base;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Core.Repositories.Base;
using AsterERP.Workflow.Tools.Common;
using AsterERP.Workflow.Tools.Pager;
using AsterERP.Workflow.Tools.Vos;
using SqlSugar;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Approval.Core.Services.Base;

public class DicTypeService : IDicTypeService
{
    private readonly IDicTypeRepository _repository;
    private readonly IClock _clock;

    public DicTypeService(IDicTypeRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<List<DicType>> GetDicTypesAsync(DicType dicType, CancellationToken cancellationToken = default)
    {
        var query = _repository.Db.Queryable<DicType>();
        if (!string.IsNullOrWhiteSpace(dicType.Keyword))
        {
            query = query.Where(d => d.Name.Contains(dicType.Keyword) || d.Code.Contains(dicType.Keyword));
        }
        return await query.Where(d => d.DelFlag == 1)
            .OrderByDescending(d => d.OrderNo)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagerModel<DicType>> GetPagerModelByWrapperAsync(DicType dicType, int pageNum, int pageSize, CancellationToken cancellationToken = default)
    {
        RefAsync<int> total = new();
        var query = _repository.Db.Queryable<DicType>();
        if (!string.IsNullOrWhiteSpace(dicType.Keyword))
        {
            query = query.Where(d => d.Code.Contains(dicType.Keyword) || d.Name.Contains(dicType.Keyword));
        }
        var list = await query.Where(d => d.DelFlag == 1)
            .OrderBy(d => d.OrderNo)
            .ToPageListAsync(pageNum, pageSize, total, cancellationToken);
        return new PagerModel<DicType>(total.Value, list);
    }

    public async Task SaveOrUpdateAsync(DicType dicType, User loginUser, CancellationToken cancellationToken = default)
    {
        var exists = !string.IsNullOrWhiteSpace(dicType.Id)
            && await _repository.GetByIdAsync(dicType.Id, cancellationToken) != null;

        if (exists)
        {
            dicType.UpdateTime = _clock.Now;
            dicType.Updator = loginUser.UserNo;
            dicType.Keyword ??= string.Empty;
        }
        else
        {
            dicType.CreateTime = _clock.Now;
            dicType.UpdateTime = dicType.CreateTime;
            dicType.Creator = string.IsNullOrWhiteSpace(dicType.Creator) ? loginUser.UserNo : dicType.Creator;
            dicType.Updator ??= string.Empty;
            dicType.Keyword ??= string.Empty;
        }

        if (!exists)
        {
            await _repository.InsertAsync(dicType, cancellationToken);
        }
        else
        {
            await _repository.UpdateAsync(dicType, cancellationToken);
        }
    }

    public async Task<ReturnVo<string>> DeleteByIdsAsync(List<string> ids, CancellationToken cancellationToken = default)
    {
        if (ids == null || ids.Count == 0)
        {
            return new ReturnVo<string>(ReturnCode.SUCCESS, "OK");
        }

        var childCount = await _repository.Db.Queryable<DicType>()
            .In(d => d.Pid, ids)
            .Where(d => d.DelFlag == 1)
            .CountAsync(cancellationToken);
        if (childCount > 0)
        {
            return new ReturnVo<string>(ReturnCode.FAIL, "该分类还存在子分类，请确认！");
        }

        var dictionaryCount = await _repository.Db.Queryable<Dictionary>()
            .Where(d => d.DelFlag == 1)
            .In(d => d.DicTypeId, ids)
            .CountAsync(cancellationToken);
        if (dictionaryCount > 0)
        {
            return new ReturnVo<string>(ReturnCode.FAIL, "该分类还存在字典数据，请确认！");
        }

        await _repository.Db.Updateable<DicType>()
            .SetColumns(d => d.DelFlag == 0)
            .Where(d => ids.Contains(d.Id))
            .ExecuteCommandAsync(cancellationToken);
        return new ReturnVo<string>(ReturnCode.SUCCESS, "OK");
    }
}
