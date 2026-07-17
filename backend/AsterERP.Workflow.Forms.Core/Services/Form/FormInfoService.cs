using AsterERP.Workflow.Approval.Api.Constants;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Forms.Api.Models.Form;
using AsterERP.Workflow.Forms.Core.Repositories.Form;
using AsterERP.Workflow.Tools.Pager;
using Volo.Abp.Guids;
using Volo.Abp.Timing;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace AsterERP.Workflow.Forms.Core.Services.Form;

public class FormInfoService : IFormInfoService
{
    private readonly IFormInfoRepository _repository;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILogger<FormInfoService> _logger;

    public FormInfoService(
        IFormInfoRepository repository,
        IClock clock,
        IGuidGenerator guidGenerator,
        ILogger<FormInfoService> logger)
    {
        _repository = repository;
        _clock = clock;
        _guidGenerator = guidGenerator;
        _logger = logger;
    }

    public async Task<PagerModel<FormInfo>> GetPagerModelByWrapperAsync(FormInfo formInfo, int pageNum, int pageSize, CancellationToken cancellationToken = default)
    {
        RefAsync<int> total = new();
        var query = _repository.Db.Queryable<FormInfo>()
            .Where(f => f.DelFlag == WorkflowApprovalConstants.DelFlag1);

        if (!string.IsNullOrWhiteSpace(formInfo.Keyword))
        {
            query = query.Where(f => f.Code.Contains(formInfo.Keyword) || f.Name.Contains(formInfo.Keyword));
        }

        var list = await query.ToPageListAsync(pageNum, pageSize, total, cancellationToken);
        return new PagerModel<FormInfo>(total.Value, list);
    }

    public async Task SaveOrUpdateAsync(FormInfo formInfo, User loginUser, CancellationToken cancellationToken = default)
    {
        var now = _clock.Now;
        var exists = !string.IsNullOrWhiteSpace(formInfo.Id)
            && await _repository.GetByIdAsync(formInfo.Id, cancellationToken) != null;

        if (exists)
        {
            formInfo.UpdateTime = now;
            formInfo.Updator = loginUser.UserNo;
            formInfo.Keyword ??= string.Empty;
        }
        else
        {
            formInfo.Id ??= _guidGenerator.Create().ToString("N");
            formInfo.CreateTime = now;
            formInfo.UpdateTime = now;
            formInfo.Creator = string.IsNullOrWhiteSpace(formInfo.Creator) ? loginUser.UserNo : formInfo.Creator;
            formInfo.Updator ??= string.Empty;
            formInfo.Keyword ??= string.Empty;
        }

        if (exists)
        {
            await _repository.Db
                .Updateable(formInfo)
                .IgnoreNullColumns()
                .ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            await _repository.InsertAsync(formInfo, cancellationToken);
        }
    }

    public async Task<FormInfo?> GetModelInfoByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var list = await _repository.QueryAsync(f => f.Code == code, cancellationToken);
        return list.FirstOrDefault();
    }
}
