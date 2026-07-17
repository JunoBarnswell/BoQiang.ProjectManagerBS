using AsterERP.Workflow.Approval.Api.Models.Org;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Api.ViewModels.Org;
using AsterERP.Workflow.Approval.Core.Repositories.Org;
using AsterERP.Workflow.Tools.Common;
using AsterERP.Workflow.Tools.Vos;
using SqlSugar;
using Volo.Abp.Guids;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Approval.Core.Services.Org;

public class JobGradeService : IJobGradeService
{
    private readonly IJobGradeRepository _jobGradeRepository;
    private readonly IJobGradeTypeService _jobGradeTypeService;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guidGenerator;

    public JobGradeService(IJobGradeRepository jobGradeRepository, IJobGradeTypeService jobGradeTypeService, IClock clock, IGuidGenerator guidGenerator)
    {
        _jobGradeRepository = jobGradeRepository;
        _jobGradeTypeService = jobGradeTypeService;
        _clock = clock;
        _guidGenerator = guidGenerator;
    }

    public async Task<List<OrgTreeVo>> GetJobGradeTreeAsync(CancellationToken cancellationToken = default)
    {
        var orgTreeVos = new List<OrgTreeVo>();

        var jobGradeTypes = await _jobGradeTypeService.GetActiveJobGradeTypesAsync(cancellationToken);
        var jobGradeTypeMap = jobGradeTypes.ToDictionary(j => j.Id, j => j);

        foreach (var jobGradeType in jobGradeTypes)
        {
            orgTreeVos.Add(new OrgTreeVo { Id = jobGradeType.Id, Code = jobGradeType.Code, Name = jobGradeType.Name, SourceType = "1" });
        }

        var jobGrades = await _jobGradeRepository.Db.Queryable<JobGrade>()
            .Where(j => j.DelFlag == 1)
            .OrderBy(j => j.OrderNo)
            .ToListAsync(cancellationToken);

        foreach (var jobGrade in jobGrades)
        {
            var vo = new OrgTreeVo { Id = jobGrade.Id, Code = jobGrade.Code, Name = jobGrade.Name, SourceType = "2" };
            if (jobGradeTypeMap.TryGetValue(jobGrade.TypeId, out var type))
            {
                vo.Pid = type.Id;
            }
            orgTreeVos.Add(vo);
        }
        return orgTreeVos;
    }

    public async Task<ReturnVo<string>> DeleteByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return new ReturnVo<string>(ReturnCode.SUCCESS, "OK");
        }

        var jobGrade = await _jobGradeRepository.GetByIdAsync(id, cancellationToken);
        if (jobGrade == null)
        {
            return new ReturnVo<string>(ReturnCode.SUCCESS, "OK");
        }

        var personalCount = await _jobGradeRepository.Db.Queryable<Personal>()
            .Where(p => p.DelFlag == 1 && p.JobGradeCode == jobGrade.Code)
            .CountAsync(cancellationToken);
        if (personalCount > 0)
        {
            return new ReturnVo<string>(ReturnCode.FAIL, "该职级还存在人员数据，请确认！");
        }

        await _jobGradeRepository.Db.Updateable<JobGrade>()
            .SetColumns(j => j.DelFlag == 0)
            .Where(j => j.Id == id)
            .ExecuteCommandAsync(cancellationToken);
        return new ReturnVo<string>(ReturnCode.SUCCESS, "OK");
    }

    public async Task SaveOrUpdateAsync(JobGrade jobGrade, User loginUser, CancellationToken cancellationToken = default)
    {
        var exists = !string.IsNullOrWhiteSpace(jobGrade.Id)
            && await _jobGradeRepository.GetByIdAsync(jobGrade.Id, cancellationToken) != null;

        if (exists)
        {
            jobGrade.UpdateTime = _clock.Now;
            jobGrade.Updator = loginUser.UserNo;
            jobGrade.Keyword ??= string.Empty;
            await _jobGradeRepository.UpdateAsync(jobGrade, cancellationToken);
        }
        else
        {
            jobGrade.Id = string.IsNullOrWhiteSpace(jobGrade.Id) ? _guidGenerator.Create().ToString("N") : jobGrade.Id;
            jobGrade.CreateTime = _clock.Now;
            jobGrade.UpdateTime = jobGrade.CreateTime;
            jobGrade.Creator = loginUser.UserNo;
            jobGrade.Updator ??= string.Empty;
            jobGrade.Keyword ??= string.Empty;
            await _jobGradeRepository.InsertAsync(jobGrade, cancellationToken);
        }
    }

    public async Task BatchSaveOrUpdateJobGradeTypeAndJobGradeAsync(JobGradeType jobGradeType, List<JobGrade> jobGrades, User loginUser, CancellationToken cancellationToken = default)
    {
        await _jobGradeTypeService.SaveOrUpdateAsync(jobGradeType, loginUser, cancellationToken);
        if (jobGrades == null || jobGrades.Count == 0)
        {
            return;
        }

        var candidateIds = jobGrades
            .Where(jobGrade => !string.IsNullOrWhiteSpace(jobGrade.Id))
            .Select(jobGrade => jobGrade.Id!)
            .Distinct()
            .ToList();
        var existingIds = candidateIds.Count == 0
            ? new HashSet<string>(StringComparer.Ordinal)
            : (await _jobGradeRepository.Db.Queryable<JobGrade>()
                    .Where(jobGrade => candidateIds.Contains(jobGrade.Id))
                    .Select(jobGrade => jobGrade.Id)
                    .ToListAsync(cancellationToken))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!)
                .ToHashSet(StringComparer.Ordinal);

        var inserts = new List<JobGrade>();
        var updates = new List<JobGrade>();
        foreach (var jobGrade in jobGrades)
        {
            jobGrade.TypeCode = jobGradeType.Code;
            jobGrade.TypeId = jobGradeType.Id;
            if (!string.IsNullOrWhiteSpace(jobGrade.Id) && existingIds.Contains(jobGrade.Id))
            {
                jobGrade.UpdateTime = _clock.Now;
                jobGrade.Updator = loginUser.UserNo;
                jobGrade.Keyword ??= string.Empty;
                updates.Add(jobGrade);
            }
            else
            {
                jobGrade.Id = string.IsNullOrWhiteSpace(jobGrade.Id) ? _guidGenerator.Create().ToString("N") : jobGrade.Id;
                jobGrade.CreateTime = jobGrade.CreateTime ?? _clock.Now;
                jobGrade.UpdateTime = _clock.Now;
                jobGrade.Creator = string.IsNullOrWhiteSpace(jobGrade.Creator) ? loginUser.UserNo : jobGrade.Creator;
                jobGrade.Updator ??= string.Empty;
                jobGrade.Keyword ??= string.Empty;
                inserts.Add(jobGrade);
            }
        }

        if (updates.Count > 0)
        {
            await _jobGradeRepository.Db.Updateable(updates).ExecuteCommandAsync(cancellationToken);
        }

        if (inserts.Count > 0)
        {
            await _jobGradeRepository.Db.Insertable(inserts).ExecuteCommandAsync(cancellationToken);
        }
    }
}
