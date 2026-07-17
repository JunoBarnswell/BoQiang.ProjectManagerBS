using AsterERP.Workflow.Approval.Api.Enums.Workflow.Runtime;
using AsterERP.Workflow.Approval.Api.Models.Workflow;
using AsterERP.Workflow.Approval.Core.Repositories.Workflow;
using Volo.Abp.Guids;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Approval.Core.Services.Workflow;

public class ExtendProcinstService : IExtendProcinstService
{
    private readonly IExtendProcinstRepository _extendProcinstRepository;
    private readonly IExtendHisprocinstService _extendHisprocinstService;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guidGenerator;

    public ExtendProcinstService(
        IExtendProcinstRepository extendProcinstRepository,
        IExtendHisprocinstService extendHisprocinstService,
        IClock clock,
        IGuidGenerator guidGenerator)
    {
        _extendProcinstRepository = extendProcinstRepository;
        _extendHisprocinstService = extendHisprocinstService;
        _clock = clock;
        _guidGenerator = guidGenerator;
    }

    public async Task DeleteExtendProcinstByProcessInstanceIdAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        await _extendProcinstRepository.Db.Deleteable<ExtendProcinst>()
            .Where(e => e.ProcessInstanceId == processInstanceId && e.DelFlag == 1)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task SaveExtendProcinstAndHisAsync(ExtendProcinst extendProcinst, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(extendProcinst.Id))
        {
            extendProcinst.Id = _guidGenerator.Create().ToString("N");
        }
        extendProcinst.CreateTime = _clock.Now;
        extendProcinst.UpdateTime = _clock.Now;
        await _extendProcinstRepository.InsertAsync(extendProcinst, cancellationToken);

        var extendHisprocinst = new ExtendHisprocinst
        {
            ProcessInstanceId = extendProcinst.ProcessInstanceId,
            ProcessDefinitionId = extendProcinst.ProcessDefinitionId,
            ProcessStatus = extendProcinst.ProcessStatus,
            ProcessName = extendProcinst.ProcessName,
            ModelKey = extendProcinst.ModelKey,
            BusinessKey = extendProcinst.BusinessKey,
            CurrentUserCode = extendProcinst.CurrentUserCode,
            TenantId = extendProcinst.TenantId,
            UserInfo = extendProcinst.UserInfo,
            FormData = extendProcinst.FormData,
            Creator = extendProcinst.Creator
        };
        await _extendHisprocinstService.UpdateAllStatusByProcessInstanceIdAsync(extendHisprocinst, cancellationToken);
    }

    public async Task UpdateStatusAsync(ProcessStatusEnum processStatus, string processInstanceId, CancellationToken cancellationToken = default)
    {
        await _extendProcinstRepository.Db.Updateable<ExtendProcinst>()
            .SetColumns(e => e.ProcessStatus == processStatus.ToString())
            .Where(e => e.ProcessInstanceId == processInstanceId && e.DelFlag == 1)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<ExtendProcinst?> FindExtendProcinstByProcessInstanceIdAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        return await _extendProcinstRepository.Db.Queryable<ExtendProcinst>()
            .FirstAsync(e => e.ProcessInstanceId == processInstanceId && e.DelFlag == 1, cancellationToken);
    }
}
