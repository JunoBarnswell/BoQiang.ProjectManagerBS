using AsterERP.Workflow.Approval.Api.Enums.Workflow.Runtime;
using AsterERP.Workflow.Approval.Api.Models.Workflow;
using AsterERP.Workflow.Approval.Core.Repositories.Workflow;
using Volo.Abp.Guids;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Approval.Core.Services.Workflow;

public class ExtendHisprocinstService : IExtendHisprocinstService
{
    private readonly IExtendHisprocinstRepository _repository;
    private readonly IExtendProcinstRepository _extendProcinstRepository;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guidGenerator;

    public ExtendHisprocinstService(
        IExtendHisprocinstRepository repository,
        IExtendProcinstRepository extendProcinstRepository,
        IClock clock,
        IGuidGenerator guidGenerator)
    {
        _repository = repository;
        _extendProcinstRepository = extendProcinstRepository;
        _clock = clock;
        _guidGenerator = guidGenerator;
    }

    public async Task<ExtendHisprocinst?> FindExtendHisprocinstByProcessInstanceIdAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        return await _repository.Db.Queryable<ExtendHisprocinst>()
            .FirstAsync(e => e.ProcessInstanceId == processInstanceId && e.DelFlag == 1, cancellationToken);
    }

    public async Task UpdateAllStatusByProcessInstanceIdAsync(ExtendHisprocinst extendHisprocinst, CancellationToken cancellationToken = default)
    {
        var hisprocinst = await FindExtendHisprocinstByProcessInstanceIdAsync(extendHisprocinst.ProcessInstanceId, cancellationToken);
        if (hisprocinst == null)
        {
            PrepareNewHistory(extendHisprocinst);
            await _repository.InsertAsync(extendHisprocinst, cancellationToken);
        }
        else
        {
            await _repository.Db.Updateable<ExtendHisprocinst>()
                .SetColumns(e => e.ProcessStatus == extendHisprocinst.ProcessStatus)
                .Where(e => e.ProcessInstanceId == extendHisprocinst.ProcessInstanceId)
                .ExecuteCommandAsync(cancellationToken);
        }

        await _extendProcinstRepository.Db.Updateable<ExtendProcinst>()
            .SetColumns(e => e.ProcessStatus == extendHisprocinst.ProcessStatus)
            .Where(e => e.ProcessInstanceId == extendHisprocinst.ProcessInstanceId && e.DelFlag == 1)
            .ExecuteCommandAsync(cancellationToken);
    }

    private void PrepareNewHistory(ExtendHisprocinst extendHisprocinst)
    {
        var now = _clock.Now;
        extendHisprocinst.Id = string.IsNullOrWhiteSpace(extendHisprocinst.Id)
            ? _guidGenerator.Create().ToString("N")
            : extendHisprocinst.Id;
        extendHisprocinst.ProcessInstanceId ??= string.Empty;
        extendHisprocinst.ProcessDefinitionId ??= string.Empty;
        extendHisprocinst.ModelKey ??= string.Empty;
        extendHisprocinst.BusinessKey ??= string.Empty;
        extendHisprocinst.ProcessStatus ??= string.Empty;
        extendHisprocinst.ProcessName ??= string.Empty;
        extendHisprocinst.CurrentUserCode ??= string.Empty;
        extendHisprocinst.TenantId ??= string.Empty;
        extendHisprocinst.UserInfo ??= string.Empty;
        extendHisprocinst.FormData ??= string.Empty;
        extendHisprocinst.Creator ??= string.Empty;
        extendHisprocinst.Updator = string.IsNullOrWhiteSpace(extendHisprocinst.Updator)
            ? extendHisprocinst.Creator
            : extendHisprocinst.Updator;
        extendHisprocinst.CreateTime ??= now;
        extendHisprocinst.UpdateTime = now;
        extendHisprocinst.DelFlag ??= 1;
        extendHisprocinst.Keyword ??= string.Empty;
    }
}
