using AsterERP.Workflow.Approval.Api.Models.Org;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Api.ViewModels.Org;
using AsterERP.Workflow.Approval.Core.Repositories.Org;
using AsterERP.Workflow.Tools.Common;
using AsterERP.Workflow.Tools.Pager;
using AsterERP.Workflow.Tools.Vos;
using SqlSugar;
using Volo.Abp.Guids;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Approval.Core.Services.Org;

public class PositionInfoService : IPositionInfoService
{
    private readonly IPositionInfoRepository _positionInfoRepository;
    private readonly IPositionSeqService _positionSeqService;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guidGenerator;

    public PositionInfoService(IPositionInfoRepository positionInfoRepository, IPositionSeqService positionSeqService, IClock clock, IGuidGenerator guidGenerator)
    {
        _positionInfoRepository = positionInfoRepository;
        _positionSeqService = positionSeqService;
        _clock = clock;
        _guidGenerator = guidGenerator;
    }

    public async Task<List<OrgTreeVo>> GetPositionTreeAsync(CancellationToken cancellationToken = default)
    {
        var orgTreeVos = new List<OrgTreeVo>();
        var positionSeqs = await _positionSeqService.GetActivePositionSeqsAsync(cancellationToken);
        var positionSeqMap = positionSeqs.ToDictionary(p => p.Id, p => p);

        foreach (var seq in positionSeqs)
        {
            orgTreeVos.Add(new OrgTreeVo { Id = seq.Id, Pid = seq.Pid, Code = seq.Code, Name = seq.Name, SourceType = "1" });
        }

        var positionInfos = await _positionInfoRepository.Db.Queryable<PositionInfo>()
            .Where(p => p.DelFlag == 1)
            .OrderBy(p => p.OrderNo)
            .ToListAsync(cancellationToken);

        foreach (var info in positionInfos)
        {
            var vo = new OrgTreeVo { Id = info.Id, Code = info.Code, Name = info.Name, SourceType = "2" };
            if (!string.IsNullOrWhiteSpace(info.PositionSeqId) && positionSeqMap.TryGetValue(info.PositionSeqId, out var seq))
            {
                vo.Pid = seq.Id;
            }
            orgTreeVos.Add(vo);
        }
        return orgTreeVos;
    }

    public async Task SaveOrUpdateAsync(PositionInfo positionInfo, User loginUser, CancellationToken cancellationToken = default)
    {
        var exists = !string.IsNullOrWhiteSpace(positionInfo.Id)
            && await _positionInfoRepository.GetByIdAsync(positionInfo.Id, cancellationToken) != null;

        if (exists)
        {
            positionInfo.UpdateTime = _clock.Now;
            positionInfo.Updator = loginUser.UserNo;
            positionInfo.Keyword ??= string.Empty;
        }
        else
        {
            positionInfo.Id = string.IsNullOrWhiteSpace(positionInfo.Id) ? _guidGenerator.Create().ToString("N") : positionInfo.Id;
            positionInfo.CreateTime = _clock.Now;
            positionInfo.UpdateTime = positionInfo.CreateTime;
            positionInfo.Creator = string.IsNullOrWhiteSpace(positionInfo.Creator) ? loginUser.UserNo : positionInfo.Creator;
            positionInfo.Updator ??= string.Empty;
            positionInfo.Keyword ??= string.Empty;
        }

        if (!exists)
        {
            await _positionInfoRepository.InsertAsync(positionInfo, cancellationToken);
        }
        else
        {
            await _positionInfoRepository.UpdateAsync(positionInfo, cancellationToken);
        }
    }

    public async Task<ReturnVo<string>> DeleteByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return new ReturnVo<string>(ReturnCode.SUCCESS, "OK");
        }

        var positionInfo = await _positionInfoRepository.GetByIdAsync(id, cancellationToken);
        if (positionInfo == null)
        {
            return new ReturnVo<string>(ReturnCode.SUCCESS, "OK");
        }

        var childPositionCount = await _positionInfoRepository.Db.Queryable<PositionInfo>()
            .Where(p => p.DelFlag == 1 && p.SuperiorCode == positionInfo.Code)
            .CountAsync(cancellationToken);
        if (childPositionCount > 0)
        {
            return new ReturnVo<string>(ReturnCode.FAIL, "该岗位还存在下级岗位，请确认！");
        }

        var personalCount = await _positionInfoRepository.Db.Queryable<Personal>()
            .Where(p => p.DelFlag == 1 && p.PositionCode == positionInfo.Code)
            .CountAsync(cancellationToken);
        if (personalCount > 0)
        {
            return new ReturnVo<string>(ReturnCode.FAIL, "该岗位还存在人员数据，请确认！");
        }

        await _positionInfoRepository.Db.Updateable<PositionInfo>()
            .SetColumns(p => p.DelFlag == 0)
            .Where(p => p.Id == id)
            .ExecuteCommandAsync(cancellationToken);
        return new ReturnVo<string>(ReturnCode.SUCCESS, "OK");
    }

    public async Task<PagerModel<PositionInfo>> GetPagerModelByWrapperAsync(PositionInfo positionInfo, int pageNum, int pageSize, CancellationToken cancellationToken = default)
    {
        RefAsync<int> total = new();
        var list = await _positionInfoRepository.Db.Queryable<PositionInfo>()
            .Where(p => p.DelFlag == 1)
            .OrderBy(p => p.OrderNo)
            .ToPageListAsync(pageNum, pageSize, total, cancellationToken);
        return new PagerModel<PositionInfo>(total.Value, list);
    }

    public async Task BatchSaveOrUpdatePositionSeqAndPositionAsync(PositionSeq positionSeq, List<PositionInfo> positionInfos, User loginUser, CancellationToken cancellationToken = default)
    {
        await _positionSeqService.SaveOrUpdateAsync(positionSeq, loginUser, cancellationToken);
        if (positionInfos == null || positionInfos.Count == 0)
        {
            return;
        }

        var candidateIds = positionInfos
            .Where(info => !string.IsNullOrWhiteSpace(info.Id))
            .Select(info => info.Id!)
            .Distinct()
            .ToList();
        var existingIds = candidateIds.Count == 0
            ? new HashSet<string>(StringComparer.Ordinal)
            : (await _positionInfoRepository.Db.Queryable<PositionInfo>()
                    .Where(info => candidateIds.Contains(info.Id))
                    .Select(info => info.Id)
                    .ToListAsync(cancellationToken))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!)
                .ToHashSet(StringComparer.Ordinal);

        var inserts = new List<PositionInfo>();
        var updates = new List<PositionInfo>();
        foreach (var info in positionInfos)
        {
            info.PositionSeqCode = positionSeq.Code;
            info.PositionSeqId = positionSeq.Id;
            if (!string.IsNullOrWhiteSpace(info.Id) && existingIds.Contains(info.Id))
            {
                info.UpdateTime = _clock.Now;
                info.Updator = loginUser.UserNo;
                info.Keyword ??= string.Empty;
                updates.Add(info);
            }
            else
            {
                info.Id = string.IsNullOrWhiteSpace(info.Id) ? _guidGenerator.Create().ToString("N") : info.Id;
                info.CreateTime = info.CreateTime ?? _clock.Now;
                info.UpdateTime = _clock.Now;
                info.Creator = string.IsNullOrWhiteSpace(info.Creator) ? loginUser.UserNo : info.Creator;
                info.Updator ??= string.Empty;
                info.Keyword ??= string.Empty;
                inserts.Add(info);
            }
        }

        if (updates.Count > 0)
        {
            await _positionInfoRepository.Db.Updateable(updates).ExecuteCommandAsync(cancellationToken);
        }

        if (inserts.Count > 0)
        {
            await _positionInfoRepository.Db.Insertable(inserts).ExecuteCommandAsync(cancellationToken);
        }
    }
}
