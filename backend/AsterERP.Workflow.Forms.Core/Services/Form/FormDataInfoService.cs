using AsterERP.Workflow.Approval.Api.Constants;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Forms.Api.Models.Form;
using AsterERP.Workflow.Forms.Core.Repositories.Form;
using AsterERP.Workflow.Tools.Pager;
using Volo.Abp.Timing;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace AsterERP.Workflow.Forms.Core.Services.Form;

public class FormDataInfoService : IFormDataInfoService
{
    private readonly IFormDataInfoRepository _repository;
    private readonly IClock _clock;
    private readonly ILogger<FormDataInfoService> _logger;

    public FormDataInfoService(
        IFormDataInfoRepository repository,
        IClock clock,
        ILogger<FormDataInfoService> logger)
    {
        _repository = repository;
        _clock = clock;
        _logger = logger;
    }

    public async Task<FormDataInfo?> GetFormDataInfoByModelKeyAndBusinessKeyAsync(string modelKey, string businessKey, CancellationToken cancellationToken = default)
    {
        var list = await _repository.QueryAsync(
            f => f.ModelKey == modelKey && f.BusinessKey == businessKey, cancellationToken);
        return list.FirstOrDefault();
    }

    public async Task<FormDataInfo?> GetFormDataInfoByProcessInstanceIdAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        var list = await _repository.QueryAsync(
            f => f.ProcessInstanceId == processInstanceId, cancellationToken);
        return list.FirstOrDefault();
    }

    public async Task<PagerModel<FormDataInfo>> GetPagerModelByWrapperAsync(FormDataInfo formDataInfo, int pageNum, int pageSize, CancellationToken cancellationToken = default)
    {
        RefAsync<int> total = new();
        var query = _repository.Db.Queryable<FormDataInfo>()
            .Where(f => f.DelFlag == WorkflowApprovalConstants.DelFlag1);

        if (!string.IsNullOrWhiteSpace(formDataInfo.Keyword))
        {
            query = query.Where(f => f.ModelKey.Contains(formDataInfo.Keyword) || f.BusinessKey.Contains(formDataInfo.Keyword));
        }

        var list = await query.ToPageListAsync(pageNum, pageSize, total, cancellationToken);
        return new PagerModel<FormDataInfo>(total.Value, list);
    }

    public async Task SaveOrUpdateAsync(FormDataInfo formDataInfo, User loginUser, CancellationToken cancellationToken = default)
    {
        var now = _clock.Now;
        if (!string.IsNullOrWhiteSpace(formDataInfo.Id))
        {
            formDataInfo.UpdateTime = now;
            formDataInfo.Updator = loginUser.UserNo;
        }
        else
        {
            formDataInfo.CreateTime = now;
            formDataInfo.Creator = loginUser.UserNo;
        }

        if (!string.IsNullOrWhiteSpace(formDataInfo.Id))
        {
            await _repository.UpdateAsync(formDataInfo, cancellationToken);
        }
        else
        {
            await _repository.InsertAsync(formDataInfo, cancellationToken);
        }
    }
}
