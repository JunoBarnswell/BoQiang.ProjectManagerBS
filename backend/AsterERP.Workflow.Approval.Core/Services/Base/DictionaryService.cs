using AsterERP.Workflow.Approval.Api.Models.Base;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Core.Repositories.Base;
using AsterERP.Workflow.Tools.Common;
using AsterERP.Workflow.Tools.Pager;
using AsterERP.Workflow.Tools.Vos;
using SqlSugar;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Approval.Core.Services.Base;

public class DictionaryService : IDictionaryService
{
    private readonly IDictionaryRepository _dictionaryRepository;
    private readonly IClock _clock;

    public DictionaryService(IDictionaryRepository dictionaryRepository, IClock clock)
    {
        _dictionaryRepository = dictionaryRepository;
        _clock = clock;
    }

    public async Task<PagerModel<Dictionary>> GetPagerModelByWrapperAsync(Dictionary dictionary, int pageNum, int pageSize, CancellationToken cancellationToken = default)
    {
        RefAsync<int> total = new();
        var query = _dictionaryRepository.Db.Queryable<Dictionary>()
            .Where(d => d.DicTypeId == dictionary.DicTypeId && d.DelFlag == 1);
        if (!string.IsNullOrWhiteSpace(dictionary.Keyword))
        {
            query = query.Where(d => d.Code.Contains(dictionary.Keyword) || d.Cname.Contains(dictionary.Keyword));
        }
        var list = await query.ToPageListAsync(pageNum, pageSize, total, cancellationToken);
        return new PagerModel<Dictionary>(total.Value, list);
    }

    public async Task SaveOrUpdateAsync(Dictionary dictionary, User loginUser, CancellationToken cancellationToken = default)
    {
        var exists = !string.IsNullOrWhiteSpace(dictionary.Id)
            && await _dictionaryRepository.GetByIdAsync(dictionary.Id, cancellationToken) != null;

        if (exists)
        {
            dictionary.UpdateTime = _clock.Now;
            dictionary.Updator = loginUser.UserNo;
            dictionary.Keyword ??= string.Empty;
        }
        else
        {
            dictionary.CreateTime = _clock.Now;
            dictionary.UpdateTime = dictionary.CreateTime;
            dictionary.Creator = string.IsNullOrWhiteSpace(dictionary.Creator) ? loginUser.UserNo : dictionary.Creator;
            dictionary.Updator ??= string.Empty;
            dictionary.Keyword ??= string.Empty;
        }

        if (!exists)
        {
            await _dictionaryRepository.InsertAsync(dictionary, cancellationToken);
        }
        else
        {
            await _dictionaryRepository.UpdateAsync(dictionary, cancellationToken);
        }
    }

    public async Task<ReturnVo<string>> DeleteByIdsAsync(string[] ids, CancellationToken cancellationToken = default)
    {
        if (ids == null || ids.Length == 0)
        {
            return new ReturnVo<string>(ReturnCode.SUCCESS, "OK");
        }

        var dicItemCount = await _dictionaryRepository.Db.Queryable<DicItem>()
            .Where(d => d.DelFlag == 1)
            .In(d => d.MainId, ids)
            .CountAsync(cancellationToken);
        if (dicItemCount > 0)
        {
            return new ReturnVo<string>(ReturnCode.FAIL, "存在字典项数据，请确认");
        }

        await _dictionaryRepository.Db.Updateable<Dictionary>()
            .SetColumns(d => d.DelFlag == 0)
            .Where(d => ids.Contains(d.Id))
            .ExecuteCommandAsync(cancellationToken);
        return new ReturnVo<string>(ReturnCode.SUCCESS, "OK");
    }
}
