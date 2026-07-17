using AsterERP.Workflow.Approval.Api.Models.Org;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Core.Repositories.Org;
using AsterERP.Workflow.Tools.Common;
using AsterERP.Workflow.Tools.Vos;
using Volo.Abp.Guids;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Approval.Core.Services.Org;

public class JobGradeTypeService : IJobGradeTypeService
{
    private readonly IJobGradeTypeRepository _repository;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guidGenerator;

    public JobGradeTypeService(IJobGradeTypeRepository repository, IClock clock, IGuidGenerator guidGenerator)
    {
        _repository = repository;
        _clock = clock;
        _guidGenerator = guidGenerator;
    }

    public async Task SaveOrUpdateAsync(JobGradeType jobGradeType, User loginUser, CancellationToken cancellationToken = default)
    {
        var exists = !string.IsNullOrWhiteSpace(jobGradeType.Id)
            && await _repository.GetByIdAsync(jobGradeType.Id, cancellationToken) != null;

        if (exists)
        {
            jobGradeType.UpdateTime = _clock.Now;
            jobGradeType.Updator = loginUser.UserNo;
            jobGradeType.Keyword ??= string.Empty;
            await _repository.UpdateAsync(jobGradeType, cancellationToken);
        }
        else
        {
            jobGradeType.Id = string.IsNullOrWhiteSpace(jobGradeType.Id) ? _guidGenerator.Create().ToString("N") : jobGradeType.Id;
            jobGradeType.CreateTime = _clock.Now;
            jobGradeType.UpdateTime = jobGradeType.CreateTime;
            jobGradeType.Creator = loginUser.UserNo;
            jobGradeType.Updator ??= string.Empty;
            jobGradeType.Keyword ??= string.Empty;
            await _repository.InsertAsync(jobGradeType, cancellationToken);
        }
    }

    public async Task<ReturnVo<string>> DeleteByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return new ReturnVo<string>(ReturnCode.SUCCESS, "OK");
        }

        var jobGradeCount = await _repository.Db.Queryable<JobGrade>()
            .Where(j => j.DelFlag == 1 && j.TypeId == id)
            .CountAsync(cancellationToken);
        if (jobGradeCount > 0)
        {
            return new ReturnVo<string>(ReturnCode.FAIL, "该职级类型还存在职级数据，请确认！");
        }

        await _repository.Db.Updateable<JobGradeType>()
            .SetColumns(j => j.DelFlag == 0)
            .Where(j => j.Id == id)
            .ExecuteCommandAsync(cancellationToken);
        return new ReturnVo<string>(ReturnCode.SUCCESS, "OK");
    }

    public async Task<List<JobGradeType>> GetActiveJobGradeTypesAsync(CancellationToken cancellationToken = default)
    {
        return await _repository.Db.Queryable<JobGradeType>()
            .Where(j => j.DelFlag == 1)
            .OrderBy(j => j.OrderNo)
            .ToListAsync(cancellationToken);
    }
}
