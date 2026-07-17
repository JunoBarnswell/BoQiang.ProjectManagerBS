using AsterERP.Workflow.Approval.Api.Models.Base;
using AsterERP.Workflow.Approval.Api.Models.Workflow;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Core.Repositories.Base;
using AsterERP.Workflow.Tools.Common;
using AsterERP.Workflow.Tools.Pager;
using AsterERP.Workflow.Tools.Vos;
using SqlSugar;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Approval.Core.Services.Base;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IClock _clock;

    public CategoryService(ICategoryRepository categoryRepository, IClock clock)
    {
        _categoryRepository = categoryRepository;
        _clock = clock;
    }

    public async Task<List<Category>> GetCategoriesAsync(Category category, CancellationToken cancellationToken = default)
    {
        var query = _categoryRepository.Db.Queryable<Category>();
        if (!string.IsNullOrWhiteSpace(category.Keyword))
        {
            query = query.Where(c => c.Name.Contains(category.Keyword) || c.Code.Contains(category.Keyword));
        }
        if (category.FrontShow != null)
        {
            query = query.Where(c => c.FrontShow == category.FrontShow);
        }
        return await query.Where(c => c.DelFlag == 1)
            .OrderByDescending(c => c.OrderNo)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagerModel<Category>> GetPagerModelByWrapperAsync(Category category, int pageNum, int pageSize, CancellationToken cancellationToken = default)
    {
        RefAsync<int> total = new();
        var list = await _categoryRepository.Db.Queryable<Category>()
            .WhereIF(!string.IsNullOrWhiteSpace(category.Keyword), c => c.Code.Contains(category.Keyword) || c.Name.Contains(category.Keyword))
            .Where(c => c.DelFlag == 1)
            .OrderBy(c => c.OrderNo)
            .ToPageListAsync(pageNum, pageSize, total, cancellationToken);
        return new PagerModel<Category>(total.Value, list);
    }

    public async Task SaveOrUpdateAsync(Category category, User loginUser, CancellationToken cancellationToken = default)
    {
        var exists = !string.IsNullOrWhiteSpace(category.Id)
            && await _categoryRepository.GetByIdAsync(category.Id, cancellationToken) != null;

        if (exists)
        {
            category.UpdateTime = _clock.Now;
            category.Updator = loginUser.UserNo;
            category.Keyword ??= string.Empty;
        }
        else
        {
            category.CreateTime = _clock.Now;
            category.UpdateTime = category.CreateTime;
            category.Creator = string.IsNullOrWhiteSpace(category.Creator) ? loginUser.UserNo : category.Creator;
            category.Updator ??= string.Empty;
            category.Keyword ??= string.Empty;
        }

        if (!exists)
        {
            await _categoryRepository.InsertAsync(category, cancellationToken);
        }
        else
        {
            await _categoryRepository.UpdateAsync(category, cancellationToken);
        }
    }

    public async Task<ReturnVo<string>> DeleteByIdsAsync(List<string> ids, CancellationToken cancellationToken = default)
    {
        var returnVo = new ReturnVo<string>(ReturnCode.SUCCESS, "OK");
        if (ids == null || ids.Count == 0)
        {
            return returnVo;
        }

        var categories = await _categoryRepository.Db.Queryable<Category>()
            .Where(c => c.DelFlag == 1)
            .In(c => c.Id, ids)
            .ToListAsync(cancellationToken);
        if (categories.Count == 0)
        {
            return returnVo;
        }

        var categoryCodes = categories
            .Where(category => !string.IsNullOrWhiteSpace(category.Code))
            .Select(category => category.Code!)
            .Distinct()
            .ToList();
        if (categoryCodes.Count > 0)
        {
            var modelCount = await _categoryRepository.Db.Queryable<ModelInfo>()
                .Where(m => m.DelFlag == 1)
                .In(m => m.CategoryCode, categoryCodes)
                .CountAsync(cancellationToken);
            if (modelCount > 0)
            {
                return new ReturnVo<string>(ReturnCode.FAIL, "该分类还存在流程模板，请确认！");
            }
        }

        var childCount = await _categoryRepository.Db.Queryable<Category>()
            .Where(c => c.DelFlag == 1)
            .In(c => c.Pid, ids)
            .CountAsync(cancellationToken);
        if (childCount > 0)
        {
            return new ReturnVo<string>(ReturnCode.FAIL, "该分类还存在子分类，请确认！");
        }

        await _categoryRepository.Db.Updateable<Category>()
            .SetColumns(c => c.DelFlag == 0)
            .Where(c => ids.Contains(c.Id))
            .ExecuteCommandAsync(cancellationToken);

        return returnVo;
    }
}
