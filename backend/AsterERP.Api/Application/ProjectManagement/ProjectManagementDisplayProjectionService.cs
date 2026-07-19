using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Api.Modules.System.Users;
using SqlSugar;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementDisplayProjectionService(IWorkspaceDatabaseAccessor databaseAccessor) : IProjectManagementDisplayProjectionService
{
    public async Task<ProjectManagementDisplayProjection> ResolveAsync(
        IEnumerable<string?> projectIds,
        IEnumerable<ProjectManagementDisplayReference> references,
        IEnumerable<string?> userIds,
        CancellationToken cancellationToken = default)
    {
        var referenceList = references.Where(item => HasValue(item.AggregateId)).ToArray();
        var ids = projectIds
            .Concat(referenceList.Where(item => item.AggregateType.Equals("Project", StringComparison.OrdinalIgnoreCase)).Select(item => item.AggregateId))
            .Where(HasValue)
            .Select(value => Normalize(value!))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var userIdList = userIds.Where(HasValue).Select(value => Normalize(value!)).Distinct(StringComparer.Ordinal).ToArray();
        var projectDb = databaseAccessor.GetProjectManagementDb();
        var projects = ids.Length == 0 ? [] : await projectDb.Queryable<ProjectManagementProjectEntity>()
            .Where(item => ids.Contains(item.Id) && !item.IsDeleted)
            .Select(item => new { item.Id, item.ProjectCode, item.ProjectName })
            .ToListAsync(cancellationToken);
        var aggregateValues = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var project in projects) aggregateValues[ProjectManagementDisplayProjection.Key("Project", project.Id)] = ProjectLabel(project.ProjectCode, project.ProjectName);
        await AddMilestonesAsync(projectDb, referenceList, aggregateValues, cancellationToken);
        await AddTasksAsync(projectDb, referenceList, aggregateValues, cancellationToken);
        var users = userIdList.Length == 0 ? [] : await databaseAccessor.GetCurrentDb().Queryable<SystemUserEntity>()
            .Where(item => userIdList.Contains(item.Id) && !item.IsDeleted)
            .Select(item => new { item.Id, item.UserName, item.DisplayName })
            .ToListAsync(cancellationToken);
        return new ProjectManagementDisplayProjection(
            projects.ToDictionary(item => item.Id, item => ProjectLabel(item.ProjectCode, item.ProjectName), StringComparer.Ordinal),
            aggregateValues,
            users.ToDictionary(item => item.Id, item => string.IsNullOrWhiteSpace(item.DisplayName) ? item.UserName : item.DisplayName, StringComparer.Ordinal));
    }

    public async Task<IReadOnlyList<string>> FindUserIdsAsync(string keyword, CancellationToken cancellationToken = default)
    {
        var normalized = keyword.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return [];
        return await databaseAccessor.GetCurrentDb().Queryable<SystemUserEntity>()
            .Where(item => !item.IsDeleted && (item.UserName.Contains(normalized) || item.DisplayName.Contains(normalized)))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> FindProjectIdsAsync(string keyword, CancellationToken cancellationToken = default)
    {
        var normalized = keyword.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return [];
        return await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementProjectEntity>()
            .Where(item => !item.IsDeleted && (item.ProjectCode.Contains(normalized) || item.ProjectName.Contains(normalized)))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
    }

    private static async Task AddMilestonesAsync(ISqlSugarClient db, IReadOnlyList<ProjectManagementDisplayReference> references, IDictionary<string, string> values, CancellationToken cancellationToken)
    {
        var ids = references.Where(item => item.AggregateType.Equals("Milestone", StringComparison.OrdinalIgnoreCase)).Select(item => item.AggregateId).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0) return;
        var rows = await db.Queryable<ProjectManagementMilestoneEntity>().Where(item => ids.Contains(item.Id) && !item.IsDeleted).Select(item => new { item.Id, item.MilestoneName }).ToListAsync(cancellationToken);
        foreach (var item in rows) values[ProjectManagementDisplayProjection.Key("Milestone", item.Id)] = item.MilestoneName;
    }

    private static async Task AddTasksAsync(ISqlSugarClient db, IReadOnlyList<ProjectManagementDisplayReference> references, IDictionary<string, string> values, CancellationToken cancellationToken)
    {
        var ids = references.Where(item => item.AggregateType.Equals("Task", StringComparison.OrdinalIgnoreCase)).Select(item => item.AggregateId).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0) return;
        var rows = await db.Queryable<ProjectManagementTaskEntity>().Where(item => ids.Contains(item.Id) && !item.IsDeleted).Select(item => new { item.Id, item.TaskCode, item.Title }).ToListAsync(cancellationToken);
        foreach (var item in rows) values[ProjectManagementDisplayProjection.Key("Task", item.Id)] = ProjectLabel(item.TaskCode, item.Title);
    }

    private static bool HasValue(string? value) => !string.IsNullOrWhiteSpace(value);
    private static string Normalize(string value) => value.Trim();
    private static string ProjectLabel(string code, string name) => string.IsNullOrWhiteSpace(code) ? name : $"{code} · {name}";
}
