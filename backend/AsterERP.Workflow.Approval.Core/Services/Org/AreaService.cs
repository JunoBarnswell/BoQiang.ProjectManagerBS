using AsterERP.Workflow.Approval.Api.Models.Base;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Core.Repositories.Base;
using AsterERP.Workflow.Approval.Core.Services.Base;
using AsterERP.Workflow.Tools.Common;
using AsterERP.Workflow.Tools.Vos;
using SqlSugar;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Approval.Core.Services.Org;

public class AreaService : IAreaService
{
    private readonly IAreaRepository _areaRepository;
    private readonly IClock _clock;

    public AreaService(IAreaRepository areaRepository, IClock clock)
    {
        _areaRepository = areaRepository;
        _clock = clock;
    }

    public async Task SaveOrUpdateAsync(Area area, User loginUser, CancellationToken cancellationToken = default)
    {
        var exists = !string.IsNullOrWhiteSpace(area.Code)
            && await _areaRepository.GetByIdAsync(area.Code, cancellationToken) != null;

        if (exists)
        {
            area.UpdateTime = _clock.Now;
            area.Updator = loginUser.UserNo;
            area.Keyword ??= string.Empty;
        }
        else
        {
            area.CreateTime = _clock.Now;
            area.UpdateTime = area.CreateTime;
            area.Creator = string.IsNullOrWhiteSpace(area.Creator) ? loginUser.UserNo : area.Creator;
            area.Updator ??= string.Empty;
            area.Keyword ??= string.Empty;
        }

        if (!exists)
        {
            await _areaRepository.InsertAsync(area, cancellationToken);
        }
        else
        {
            await _areaRepository.UpdateAsync(area, cancellationToken);
        }
    }

    public async Task<ReturnVo<string>> DeleteByCodesAsync(string[] codes, CancellationToken cancellationToken = default)
    {
        if (codes == null || codes.Length == 0)
        {
            return new ReturnVo<string>(ReturnCode.SUCCESS, "OK");
        }

        var childCount = await _areaRepository.Db.Queryable<Area>()
            .Where(a => a.DelFlag == 1)
            .In(a => a.Pcode, codes)
            .CountAsync(cancellationToken);
        if (childCount > 0)
        {
            return new ReturnVo<string>(ReturnCode.FAIL, "该地区还存在子地区，请确认！");
        }

        await _areaRepository.Db.Updateable<Area>()
            .SetColumns(a => a.DelFlag == 0)
            .Where(a => codes.Contains(a.Code))
            .ExecuteCommandAsync(cancellationToken);
        return new ReturnVo<string>(ReturnCode.SUCCESS, "OK");
    }
}
