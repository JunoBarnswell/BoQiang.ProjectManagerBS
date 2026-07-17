using AsterERP.Workflow.Approval.Api.Models.Org;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Core.Repositories.Org;
using AsterERP.Workflow.Tools.Common;
using AsterERP.Workflow.Tools.Vos;
using SqlSugar;
using Volo.Abp.Guids;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Approval.Core.Services.Org;

public class PositionSeqService : IPositionSeqService
{
    private readonly IPositionSeqRepository _repository;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guidGenerator;

    public PositionSeqService(IPositionSeqRepository repository, IClock clock, IGuidGenerator guidGenerator)
    {
        _repository = repository;
        _clock = clock;
        _guidGenerator = guidGenerator;
    }

    public async Task SaveOrUpdateAsync(PositionSeq positionSeq, User loginUser, CancellationToken cancellationToken = default)
    {
        var exists = !string.IsNullOrWhiteSpace(positionSeq.Id)
            && await _repository.GetByIdAsync(positionSeq.Id, cancellationToken) != null;

        if (exists)
        {
            positionSeq.UpdateTime = _clock.Now;
            positionSeq.Updator = loginUser.UserNo;
            positionSeq.Keyword ??= string.Empty;
        }
        else
        {
            positionSeq.Id = string.IsNullOrWhiteSpace(positionSeq.Id) ? _guidGenerator.Create().ToString("N") : positionSeq.Id;
            positionSeq.CreateTime = _clock.Now;
            positionSeq.UpdateTime = positionSeq.CreateTime;
            positionSeq.Creator = string.IsNullOrWhiteSpace(positionSeq.Creator) ? loginUser.UserNo : positionSeq.Creator;
            positionSeq.Updator ??= string.Empty;
            positionSeq.Keyword ??= string.Empty;
        }

        if (!exists)
        {
            await _repository.InsertAsync(positionSeq, cancellationToken);
        }
        else
        {
            await _repository.UpdateAsync(positionSeq, cancellationToken);
        }
    }

    public async Task<ReturnVo<string>> DeleteByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return new ReturnVo<string>(ReturnCode.SUCCESS, "OK");
        }

        var childSeqCount = await _repository.Db.Queryable<PositionSeq>()
            .Where(p => p.DelFlag == 1 && p.Pid == id)
            .CountAsync(cancellationToken);
        if (childSeqCount > 0)
        {
            return new ReturnVo<string>(ReturnCode.FAIL, "该岗位序列还存在子序列，请确认！");
        }

        var positionCount = await _repository.Db.Queryable<PositionInfo>()
            .Where(p => p.DelFlag == 1 && p.PositionSeqId == id)
            .CountAsync(cancellationToken);
        if (positionCount > 0)
        {
            return new ReturnVo<string>(ReturnCode.FAIL, "该岗位序列还存在岗位数据，请确认！");
        }

        await _repository.Db.Updateable<PositionSeq>()
            .SetColumns(p => p.DelFlag == 0)
            .Where(p => p.Id == id)
            .ExecuteCommandAsync(cancellationToken);
        return new ReturnVo<string>(ReturnCode.SUCCESS, "OK");
    }

    public async Task<List<PositionSeq>> GetActivePositionSeqsAsync(CancellationToken cancellationToken = default)
    {
        return await _repository.Db.Queryable<PositionSeq>()
            .Where(p => p.DelFlag == 1)
            .OrderBy(p => p.OrderNo)
            .ToListAsync(cancellationToken);
    }
}
