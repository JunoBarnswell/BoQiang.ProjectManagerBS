using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using System.Linq.Expressions;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementHomeQueryService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    ProjectManagementHomeHealthProjector healthProjector,
    IProjectManagementDisplayProjectionService? displayProjection = null) : IProjectManagementHomeQueryService
{
    public async Task<ProjectManagementHomeProjectsResponse> QueryProjectsAsync(ProjectManagementHomeQuery query, CancellationToken cancellationToken = default)
    {
        var pageIndex = Math.Max(query.PageIndex, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var projectQuery = await BuildProjectQueryAsync(query, cancellationToken);
        var total = new RefAsync<int>();
        var page = await ApplySort(projectQuery, query.SortBy, query.SortDirection)
            .ToPageListAsync(pageIndex, pageSize, total, cancellationToken);
        var items = await ProjectItemsAsync(page, cancellationToken);
        return new(items, total.Value, pageIndex, pageSize, Sequence(page));
    }

    public async Task<ProjectManagementHomeSummaryResponse> QuerySummaryAsync(ProjectManagementHomeQuery query, CancellationToken cancellationToken = default)
    {
        var projectQuery = await BuildProjectQueryAsync(query with { PageIndex = 1, PageSize = 1 }, cancellationToken);
        var now = DateTime.UtcNow;
        var total = await projectQuery.CountAsync(cancellationToken);
        var sequence = await projectQuery.MaxAsync(project => (long?)project.VersionNo, cancellationToken) ?? 0;
        var unassignedCount = await projectQuery
            .Where(project => string.IsNullOrEmpty(project.OwnerUserId))
            .CountAsync(cancellationToken);

        var healthKeys = new[]
        {
            ProjectManagementHomeHealthProjector.Completed,
            ProjectManagementHomeHealthProjector.UpdateMissing,
            ProjectManagementHomeHealthProjector.AtRisk,
            ProjectManagementHomeHealthProjector.OffTrack,
            ProjectManagementHomeHealthProjector.OnTrack,
            ProjectManagementHomeHealthProjector.NoUpdateExpected,
        };
        var health = new List<ProjectManagementHomeHealthSummary>(healthKeys.Length);
        foreach (var key in healthKeys)
        {
            var count = await ApplyHealthFilter(projectQuery, key, now).CountAsync(cancellationToken);
            health.Add(new ProjectManagementHomeHealthSummary(key, count));
        }

        var statusRows = await projectQuery
            .GroupBy(project => project.Status)
            .Select(group => new { Key = group.Status, Count = SqlFunc.AggregateCount(group.Id) })
            .ToListAsync(cancellationToken);
        var status = statusRows
            .OrderBy(row => row.Key, StringComparer.Ordinal)
            .Select(row => new ProjectManagementHomeStatusSummary(row.Key, row.Count))
            .ToArray();

        var leadRows = await projectQuery
            .Where(project => !string.IsNullOrEmpty(project.OwnerUserId))
            .GroupBy(project => project.OwnerUserId)
            .Select(group => new { UserId = group.OwnerUserId, Count = SqlFunc.AggregateCount(group.Id) })
            .ToListAsync(cancellationToken);
        var leadDisplays = await DisplayProjection.ResolveAsync([], [], leadRows.Select(row => row.UserId), cancellationToken);
        var leads = leadRows
            .Select(row => new ProjectManagementHomeLeadSummary(row.UserId, leadDisplays.User(row.UserId) ?? "用户别名暂不可用", row.Count))
            .OrderByDescending(row => row.Count)
            .ThenBy(row => row.DisplayName, StringComparer.Ordinal)
            .ToArray();

        return new(health, leads, unassignedCount, sequence, total, status, now);
    }

    private async Task<ISugarQueryable<ProjectManagementProjectEntity>> BuildProjectQueryAsync(ProjectManagementHomeQuery query, CancellationToken cancellationToken)
    {
        RequirePlatformScope();
        var keyword = query.Keyword?.Trim();
        var parsedFilterRules = ProjectManagementHomeFilterParser.Parse(query.Filter);
        var filterRequestsArchived = parsedFilterRules.Any(rule => rule.Field.Equals("archived", StringComparison.OrdinalIgnoreCase) && rule.Values.Any(value => bool.TryParse(value, out var archived) && archived));
        var projectQuery = databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementProjectEntity>()
            .Where(project => query.IncludeArchived || filterRequestsArchived || project.Status != ProjectManagementDomainRules.ProjectArchived);
        if (string.Equals(query.View, "my-projects", StringComparison.OrdinalIgnoreCase))
        {
            var currentUserId = currentUser.GetAsterErpUserId()?.Trim();
            if (string.IsNullOrWhiteSpace(currentUserId)) throw new ValidationException("当前会话缺少用户", AsterERP.Shared.ErrorCodes.PermissionDenied);
            projectQuery = projectQuery.Where(project => project.OwnerUserId == currentUserId || SqlFunc.Subqueryable<ProjectManagementProjectMemberEntity>()
                .Where(member => member.ProjectId == project.Id && member.UserId == currentUserId && member.IsActive && !member.IsDeleted).Any());
        }
        if (string.Equals(query.View, "due-this-week", StringComparison.OrdinalIgnoreCase))
        {
            var today = DateTime.UtcNow.Date;
            var endOfWeek = today.AddDays(7);
            projectQuery = projectQuery.Where(project => project.DueDate >= today && project.DueDate < endOfWeek && project.Status != ProjectManagementDomainRules.ProjectCompleted && project.Status != ProjectManagementDomainRules.ProjectCanceled);
        }
        if (string.Equals(query.View, "at-risk", StringComparison.OrdinalIgnoreCase))
            projectQuery = ApplyRiskView(projectQuery, DateTime.UtcNow);
        if (!string.IsNullOrWhiteSpace(keyword))
            projectQuery = projectQuery.Where(project => project.ProjectCode.Contains(keyword) || project.ProjectName.Contains(keyword));
        if (!string.IsNullOrWhiteSpace(query.Status)) projectQuery = projectQuery.Where(project => project.Status == query.Status);
        if (!string.IsNullOrWhiteSpace(query.Priority)) projectQuery = projectQuery.Where(project => project.Priority == query.Priority);
        if (query.TargetDateFrom is not null) projectQuery = projectQuery.Where(project => project.DueDate >= query.TargetDateFrom);
        if (query.TargetDateTo is not null) projectQuery = projectQuery.Where(project => project.DueDate <= query.TargetDateTo);
        if (!string.IsNullOrWhiteSpace(query.LeadUserId))
        {
            var ownerIds = await DisplayProjection.FindUserIdsAsync(query.LeadUserId, cancellationToken);
            projectQuery = ownerIds.Count == 0
                ? projectQuery.Where(project => project.OwnerUserId == query.LeadUserId)
                : projectQuery.Where(project => project.OwnerUserId == query.LeadUserId || ownerIds.Contains(project.OwnerUserId));
        }
        var projectIds = NormalizeIds(query.ProjectIds);
        if (projectIds.Count > 0) projectQuery = projectQuery.Where(project => projectIds.Contains(project.Id));
        var filterRules = await NormalizeFilterRulesAsync(parsedFilterRules, cancellationToken);
        projectQuery = ApplyFilterRules(projectQuery, filterRules);
        if (!string.IsNullOrWhiteSpace(query.Health)) projectQuery = ApplyHealthFilter(projectQuery, query.Health, DateTime.UtcNow);
        return projectQuery;
    }

    private async Task<IReadOnlyList<ProjectManagementHomeFilterRule>> NormalizeFilterRulesAsync(
        IReadOnlyList<ProjectManagementHomeFilterRule> rules,
        CancellationToken cancellationToken)
    {
        if (displayProjection is null || rules.Count == 0) return rules;
        var normalized = new List<ProjectManagementHomeFilterRule>(rules.Count);
        foreach (var rule in rules)
        {
            if (!rule.Field.Equals("lead", StringComparison.OrdinalIgnoreCase) && !rule.Field.Equals("members", StringComparison.OrdinalIgnoreCase))
            {
                normalized.Add(rule);
                continue;
            }

            var ids = new HashSet<string>(rule.Values, StringComparer.OrdinalIgnoreCase);
            foreach (var value in rule.Values)
                foreach (var userId in await displayProjection.FindUserIdsAsync(value, cancellationToken))
                    ids.Add(userId);
            normalized.Add(rule with { Values = ids.ToArray() });
        }
        return normalized;
    }

    private async Task<IReadOnlyList<ProjectManagementHomeProjectItem>> ProjectItemsAsync(IReadOnlyList<ProjectManagementProjectEntity> projects, CancellationToken cancellationToken)
    {
        if (projects.Count == 0) return [];
        var ids = projects.Select(project => project.Id).ToArray();
        var db = databaseAccessor.GetProjectManagementDb();
        var tasks = await db.Queryable<ProjectManagementTaskEntity>()
            .Where(task => ids.Contains(task.ProjectId) && !task.IsDeleted)
            .Select(task => new TaskProjection
            {
                ProjectId = task.ProjectId,
                Status = task.Status,
                DueDate = task.DueDate,
                BlockedReason = task.BlockedReason
            })
            .ToListAsync(cancellationToken);
        var milestones = await db.Queryable<ProjectManagementMilestoneEntity>()
            .Where(milestone => ids.Contains(milestone.ProjectId) && !milestone.IsDeleted)
            .OrderBy(milestone => milestone.DueDate, OrderByType.Asc)
            .Select(milestone => new MilestoneProjection
            {
                ProjectId = milestone.ProjectId,
                Id = milestone.Id,
                Name = milestone.MilestoneName
            })
            .ToListAsync(cancellationToken);
        var displays = await DisplayProjection.ResolveAsync([], [], projects.Select(project => project.OwnerUserId), cancellationToken);
        var tasksByProject = tasks.GroupBy(task => task.ProjectId).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var milestonesByProject = milestones.GroupBy(milestone => milestone.ProjectId).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var now = DateTime.UtcNow;
        return projects.Select(project =>
        {
            var projectTasks = tasksByProject.TryGetValue(project.Id, out var taskRows) ? taskRows : [];
            var open = projectTasks.Count(task => task.Status != ProjectManagementDomainRules.TaskDone && task.Status != ProjectManagementDomainRules.TaskCancelled);
            var blocked = projectTasks.Count(task => task.Status == ProjectManagementDomainRules.TaskBlocked || !string.IsNullOrWhiteSpace(task.BlockedReason));
            var health = healthProjector.Project(project, open, blocked, now);
            var milestone = milestonesByProject.TryGetValue(project.Id, out var milestoneRows) ? milestoneRows.FirstOrDefault() : null;
            var displayName = displays.User(project.OwnerUserId);
            return new ProjectManagementHomeProjectItem(
                project.Id, project.ProjectCode, project.ProjectName, project.Status, project.Priority, health,
                project.OwnerUserId, displayName, project.StartDate, project.DueDate, milestone?.Id, milestone?.Name,
                projectTasks.Length, open, projectTasks.Length - open, project.ProgressPercent, project.UpdatedTime, project.VersionNo);
        }).ToArray();
    }

    private static ISugarQueryable<ProjectManagementProjectEntity> ApplySort(ISugarQueryable<ProjectManagementProjectEntity> query, string sortBy, string direction)
    {
        var descending = !string.Equals(direction, "asc", StringComparison.OrdinalIgnoreCase);
        return sortBy.ToLowerInvariant() switch
        {
            "name" => descending ? query.OrderByDescending(row => row.ProjectName).OrderByDescending(row => row.UpdatedTime) : query.OrderBy(row => row.ProjectName).OrderBy(row => row.UpdatedTime),
            "targetdate" => descending ? query.OrderByDescending(row => row.DueDate).OrderByDescending(row => row.ProjectName) : query.OrderBy(row => row.DueDate).OrderBy(row => row.ProjectName),
            "priority" => descending ? query.OrderByDescending(row => row.Priority).OrderByDescending(row => row.ProjectName) : query.OrderBy(row => row.Priority).OrderBy(row => row.ProjectName),
            "lead" => descending ? query.OrderByDescending(row => row.OwnerUserId).OrderByDescending(row => row.ProjectName) : query.OrderBy(row => row.OwnerUserId).OrderBy(row => row.ProjectName),
            "issues" => descending
                ? query.OrderByDescending(row => SqlFunc.Subqueryable<ProjectManagementTaskEntity>().Where(task => task.ProjectId == row.Id && !task.IsDeleted).Count()).OrderByDescending(row => row.ProjectName)
                : query.OrderBy(row => SqlFunc.Subqueryable<ProjectManagementTaskEntity>().Where(task => task.ProjectId == row.Id && !task.IsDeleted).Count()).OrderBy(row => row.ProjectName),
            "status" => descending ? query.OrderByDescending(row => row.Status).OrderByDescending(row => row.ProjectName) : query.OrderBy(row => row.Status).OrderBy(row => row.ProjectName),
            _ => descending ? query.OrderByDescending(row => row.UpdatedTime).OrderByDescending(row => row.CreatedTime) : query.OrderBy(row => row.UpdatedTime).OrderBy(row => row.CreatedTime)
        };
    }

    private static ISugarQueryable<ProjectManagementProjectEntity> ApplyFilterRules(
        ISugarQueryable<ProjectManagementProjectEntity> query,
        IReadOnlyList<ProjectManagementHomeFilterRule> rules)
    {
        foreach (var group in rules.GroupBy(rule => rule.Field, StringComparer.OrdinalIgnoreCase))
        {
            var expression = new Expressionable<ProjectManagementProjectEntity>();
            foreach (var rule in group)
                expression.Or(CreateRuleExpression(rule));
            query = query.Where(expression.ToExpression());
        }
        return query;
    }

    private static Expression<Func<ProjectManagementProjectEntity, bool>> CreateRuleExpression(ProjectManagementHomeFilterRule rule)
    {
        return rule.Field.ToLowerInvariant() switch
        {
            "health" => CreateHealthExpression(rule),
            "status" => CreateStringExpression(project => project.Status, rule),
            "priority" => CreateStringExpression(project => project.Priority, rule),
            "lead" => CreateStringExpression(project => project.OwnerUserId, rule),
            "projectkey" => CreateStringExpression(project => project.ProjectCode, rule),
            "workspace" => CreateStringExpression(project => project.AppCode, rule),
            "members" => CreateMemberExpression(rule),
            "labels" => CreateLabelExpression(rule),
            "archived" => CreateArchivedExpression(rule),
            "startdate" => CreateDateExpression(project => project.StartDate, rule),
            "targetdate" => CreateDateExpression(project => project.DueDate, rule),
            "updated" => CreateDateExpression(project => project.UpdatedTime, rule),
            "created" => CreateDateExpression(project => project.CreatedTime, rule),
            "issuescount" => CreateIssueCountExpression(rule),
            _ => project => true,
        };
    }

    private static Expression<Func<ProjectManagementProjectEntity, bool>> CreateStringExpression(
        Expression<Func<ProjectManagementProjectEntity, string>> selector,
        ProjectManagementHomeFilterRule rule)
    {
        var parameter = selector.Parameters[0];
        var member = selector.Body;
        var empty = Expression.Call(typeof(string), nameof(string.IsNullOrEmpty), Type.EmptyTypes, member);
        var operation = rule.Operator.ToLowerInvariant();
        if (operation is "isempty" or "isnotempty")
        {
            Expression emptyBody = operation == "isempty" ? empty : Expression.Not(empty);
            return Expression.Lambda<Func<ProjectManagementProjectEntity, bool>>(emptyBody, parameter);
        }

        var values = rule.Values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (values.Length == 0) return Expression.Lambda<Func<ProjectManagementProjectEntity, bool>>(Expression.Constant(true), parameter);

        var negative = operation is "isnot" or "notin" or "notequals" or "notcontains";
        Expression? body = null;
        foreach (var value in values)
        {
            var constant = Expression.Constant(value);
            Expression comparison = operation is "contains" or "notcontains"
                ? Expression.Call(member, nameof(string.Contains), Type.EmptyTypes, constant)
                : Expression.Equal(member, constant);
            var current = negative ? Expression.Not(comparison) : comparison;
            body = body is null
                ? current
                : (negative ? Expression.AndAlso(body, current) : Expression.OrElse(body, current));
        }

        body ??= Expression.Constant(true);
        return Expression.Lambda<Func<ProjectManagementProjectEntity, bool>>(body, parameter);
    }

    private static Expression<Func<ProjectManagementProjectEntity, bool>> CreateMemberExpression(ProjectManagementHomeFilterRule rule)
    {
        var values = rule.Values;
        if (rule.Operator.Equals("isEmpty", StringComparison.OrdinalIgnoreCase)) return project => !SqlFunc.Subqueryable<ProjectManagementProjectMemberEntity>().Where(member => member.ProjectId == project.Id && !member.IsDeleted).Any();
        if (rule.Operator.Equals("isNotEmpty", StringComparison.OrdinalIgnoreCase)) return project => SqlFunc.Subqueryable<ProjectManagementProjectMemberEntity>().Where(member => member.ProjectId == project.Id && !member.IsDeleted).Any();
        var excluded = rule.Operator.Equals("notIn", StringComparison.OrdinalIgnoreCase) || rule.Operator.Equals("isNot", StringComparison.OrdinalIgnoreCase) || rule.Operator.Equals("notEquals", StringComparison.OrdinalIgnoreCase);
        return excluded
            ? project => !SqlFunc.Subqueryable<ProjectManagementProjectMemberEntity>().Where(member => member.ProjectId == project.Id && !member.IsDeleted && values.Contains(member.UserId)).Any()
            : project => SqlFunc.Subqueryable<ProjectManagementProjectMemberEntity>().Where(member => member.ProjectId == project.Id && !member.IsDeleted && values.Contains(member.UserId)).Any();
    }

    private static Expression<Func<ProjectManagementProjectEntity, bool>> CreateLabelExpression(ProjectManagementHomeFilterRule rule)
    {
        var values = rule.Values;
        if (rule.Operator.Equals("isEmpty", StringComparison.OrdinalIgnoreCase)) return project => !SqlFunc.Subqueryable<ProjectManagementLabelEntity>().Where(label => label.ProjectId == project.Id && !label.IsDeleted).Any();
        if (rule.Operator.Equals("isNotEmpty", StringComparison.OrdinalIgnoreCase)) return project => SqlFunc.Subqueryable<ProjectManagementLabelEntity>().Where(label => label.ProjectId == project.Id && !label.IsDeleted).Any();
        var excluded = rule.Operator.Equals("notIn", StringComparison.OrdinalIgnoreCase) || rule.Operator.Equals("isNot", StringComparison.OrdinalIgnoreCase) || rule.Operator.Equals("notEquals", StringComparison.OrdinalIgnoreCase);
        return excluded
            ? project => !SqlFunc.Subqueryable<ProjectManagementLabelEntity>().Where(label => label.ProjectId == project.Id && !label.IsDeleted && values.Contains(label.LabelName)).Any()
            : project => SqlFunc.Subqueryable<ProjectManagementLabelEntity>().Where(label => label.ProjectId == project.Id && !label.IsDeleted && values.Contains(label.LabelName)).Any();
    }

    private static Expression<Func<ProjectManagementProjectEntity, bool>> CreateArchivedExpression(ProjectManagementHomeFilterRule rule)
    {
        var archived = rule.Values.Any(value => bool.TryParse(value, out var parsed) && parsed);
        return rule.Operator.Equals("isNot", StringComparison.OrdinalIgnoreCase) || rule.Operator.Equals("notEquals", StringComparison.OrdinalIgnoreCase)
            ? project => archived ? project.Status != ProjectManagementDomainRules.ProjectArchived : project.Status == ProjectManagementDomainRules.ProjectArchived
            : project => archived ? project.Status == ProjectManagementDomainRules.ProjectArchived : project.Status != ProjectManagementDomainRules.ProjectArchived;
    }

    private static Expression<Func<ProjectManagementProjectEntity, bool>> CreateDateExpression(
        Expression<Func<ProjectManagementProjectEntity, DateTime?>> selector,
        ProjectManagementHomeFilterRule rule)
    {
        var parameter = selector.Parameters[0];
        var member = selector.Body;
        var op = rule.Operator.ToLowerInvariant();
        if (op is "isempty" or "isnotempty")
        {
            var isEmpty = Expression.Equal(member, Expression.Constant(null, typeof(DateTime?)));
            Expression emptyBody = op == "isempty" ? isEmpty : Expression.Not(isEmpty);
            return Expression.Lambda<Func<ProjectManagementProjectEntity, bool>>(emptyBody, parameter);
        }

        var now = DateTime.UtcNow;
        if (op is "today" or "thisweek" or "overdue")
        {
            var start = op == "today" ? now.Date : StartOfWeek(now.Date);
            var end = op == "today" ? start.AddDays(1) : start.AddDays(7);
            var lower = Expression.GreaterThanOrEqual(member, Expression.Constant(start, typeof(DateTime?)));
            var upper = Expression.LessThan(member, Expression.Constant(end, typeof(DateTime?)));
            var relative = op == "overdue" ? Expression.LessThan(member, Expression.Constant(now.Date, typeof(DateTime?))) : Expression.AndAlso(lower, upper);
            return Expression.Lambda<Func<ProjectManagementProjectEntity, bool>>(relative, parameter);
        }

        if (!DateTime.TryParse(rule.Values[0], out var parsed)) throw new ValidationException($"HOME 筛选日期无效：{rule.Field}");
        var value = Expression.Constant(parsed.ToUniversalTime(), typeof(DateTime?));
        Expression body = op == "between"
            ? CreateDateBetweenExpression(member, rule, parameter)
            : op switch
        {
            "before" or "lessthan" => Expression.LessThan(member, value),
            "beforeoron" or "lessequal" or "lessthanorequal" => Expression.LessThanOrEqual(member, value),
            "after" or "greaterthan" => Expression.GreaterThan(member, value),
            "afteroron" or "greaterequal" or "greaterthanorequal" => Expression.GreaterThanOrEqual(member, value),
            "notequals" => Expression.NotEqual(member, value),
            _ => Expression.Equal(member, value),
        };
        return Expression.Lambda<Func<ProjectManagementProjectEntity, bool>>(body, parameter);
    }

    private static Expression CreateDateBetweenExpression(Expression member, ProjectManagementHomeFilterRule rule, ParameterExpression parameter)
    {
        if (rule.Values.Count < 2 || !DateTime.TryParse(rule.Values[1], out var end))
            throw new ValidationException($"HOME 筛选日期范围无效：{rule.Field}");
        var start = DateTime.Parse(rule.Values[0]).ToUniversalTime();
        var lower = Expression.GreaterThanOrEqual(member, Expression.Constant(start, typeof(DateTime?)));
        var upper = Expression.LessThanOrEqual(member, Expression.Constant(end.ToUniversalTime(), typeof(DateTime?)));
        return Expression.AndAlso(lower, upper);
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        var offset = date.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)date.DayOfWeek - 1;
        return date.AddDays(-offset);
    }

    private static Expression<Func<ProjectManagementProjectEntity, bool>> CreateIssueCountExpression(ProjectManagementHomeFilterRule rule)
    {
        if (rule.Operator.Equals("isEmpty", StringComparison.OrdinalIgnoreCase)) return project => SqlFunc.Subqueryable<ProjectManagementTaskEntity>().Where(task => task.ProjectId == project.Id && !task.IsDeleted).Count() == 0;
        if (rule.Operator.Equals("isNotEmpty", StringComparison.OrdinalIgnoreCase)) return project => SqlFunc.Subqueryable<ProjectManagementTaskEntity>().Where(task => task.ProjectId == project.Id && !task.IsDeleted).Count() > 0;
        if (!int.TryParse(rule.Values[0], out var count)) throw new ValidationException("HOME Issues count 筛选必须是整数");
        return rule.Operator.ToLowerInvariant() switch
        {
            "between" when rule.Values.Count > 1 && int.TryParse(rule.Values[1], out var upper) => project => SqlFunc.Subqueryable<ProjectManagementTaskEntity>().Where(task => task.ProjectId == project.Id && !task.IsDeleted).Count() >= count && SqlFunc.Subqueryable<ProjectManagementTaskEntity>().Where(task => task.ProjectId == project.Id && !task.IsDeleted).Count() <= upper,
            "greaterthan" => project => SqlFunc.Subqueryable<ProjectManagementTaskEntity>().Where(task => task.ProjectId == project.Id && !task.IsDeleted).Count() > count,
            "greaterorequal" => project => SqlFunc.Subqueryable<ProjectManagementTaskEntity>().Where(task => task.ProjectId == project.Id && !task.IsDeleted).Count() >= count,
            "lessthan" => project => SqlFunc.Subqueryable<ProjectManagementTaskEntity>().Where(task => task.ProjectId == project.Id && !task.IsDeleted).Count() < count,
            "lessorequal" => project => SqlFunc.Subqueryable<ProjectManagementTaskEntity>().Where(task => task.ProjectId == project.Id && !task.IsDeleted).Count() <= count,
            "notequals" => project => SqlFunc.Subqueryable<ProjectManagementTaskEntity>().Where(task => task.ProjectId == project.Id && !task.IsDeleted).Count() != count,
            _ => project => SqlFunc.Subqueryable<ProjectManagementTaskEntity>().Where(task => task.ProjectId == project.Id && !task.IsDeleted).Count() == count,
        };
    }

    private static Expression<Func<ProjectManagementProjectEntity, bool>> CreateHealthExpression(ProjectManagementHomeFilterRule rule)
    {
        var now = DateTime.UtcNow;
        var expression = new Expressionable<ProjectManagementProjectEntity>();
        foreach (var value in rule.Values.Distinct(StringComparer.OrdinalIgnoreCase))
            expression.Or(CreateSingleHealthExpression(value, now));
        return expression.ToExpression();
    }

    private static Expression<Func<ProjectManagementProjectEntity, bool>> CreateSingleHealthExpression(string health, DateTime now)
    {
        return health switch
        {
            ProjectManagementHomeHealthProjector.Completed => project => project.Status == ProjectManagementDomainRules.ProjectCompleted,
            ProjectManagementHomeHealthProjector.NoUpdateExpected => project => project.Status == ProjectManagementDomainRules.ProjectPlanning || project.Status == ProjectManagementDomainRules.ProjectPaused,
            ProjectManagementHomeHealthProjector.OffTrack => project => project.Status != ProjectManagementDomainRules.ProjectCompleted && project.Status != ProjectManagementDomainRules.ProjectPlanning && project.Status != ProjectManagementDomainRules.ProjectPaused && project.DueDate < now && SqlFunc.Subqueryable<ProjectManagementTaskEntity>().Where(task => task.ProjectId == project.Id && !task.IsDeleted && task.Status != ProjectManagementDomainRules.TaskDone && task.Status != ProjectManagementDomainRules.TaskCancelled).Any(),
            ProjectManagementHomeHealthProjector.AtRisk => project => project.Status != ProjectManagementDomainRules.ProjectCompleted && project.Status != ProjectManagementDomainRules.ProjectPlanning && project.Status != ProjectManagementDomainRules.ProjectPaused && (SqlFunc.Subqueryable<ProjectManagementTaskEntity>().Where(task => task.ProjectId == project.Id && !task.IsDeleted && (task.Status == ProjectManagementDomainRules.TaskBlocked || (task.BlockedReason != null && task.BlockedReason != ""))).Any() || (project.DueDate <= now.AddDays(14) && project.ProgressPercent < 60)),
            ProjectManagementHomeHealthProjector.UpdateMissing => project => project.Status != ProjectManagementDomainRules.ProjectCompleted && project.Status != ProjectManagementDomainRules.ProjectPlanning && project.Status != ProjectManagementDomainRules.ProjectPaused && (project.UpdatedTime == null || project.UpdatedTime < now.AddDays(-14)),
            ProjectManagementHomeHealthProjector.OnTrack => project => project.Status != ProjectManagementDomainRules.ProjectCompleted && project.Status != ProjectManagementDomainRules.ProjectPlanning && project.Status != ProjectManagementDomainRules.ProjectPaused && (project.UpdatedTime != null && project.UpdatedTime >= now.AddDays(-14)) && !SqlFunc.Subqueryable<ProjectManagementTaskEntity>().Where(task => task.ProjectId == project.Id && !task.IsDeleted && (task.Status == ProjectManagementDomainRules.TaskBlocked || (task.BlockedReason != null && task.BlockedReason != ""))).Any() && !(project.DueDate <= now.AddDays(14) && project.ProgressPercent < 60),
            _ => project => false,
        };
    }

    private static ISugarQueryable<ProjectManagementProjectEntity> ApplyHealthFilter(ISugarQueryable<ProjectManagementProjectEntity> query, string health, DateTime now)
    {
        return query.Where(CreateSingleHealthExpression(health, now));
    }

    private static ISugarQueryable<ProjectManagementProjectEntity> ApplyRiskView(ISugarQueryable<ProjectManagementProjectEntity> query, DateTime now)
    {
        var stale = now.AddDays(-14);
        var dueSoon = now.AddDays(14);
        return query.Where(project =>
            (project.Status != ProjectManagementDomainRules.ProjectCompleted && (project.UpdatedTime == null || project.UpdatedTime < stale))
            || (project.DueDate != null && project.DueDate < now && SqlFunc.Subqueryable<ProjectManagementTaskEntity>().Where(task => task.ProjectId == project.Id && !task.IsDeleted && task.Status != ProjectManagementDomainRules.TaskDone && task.Status != ProjectManagementDomainRules.TaskCancelled).Any())
            || SqlFunc.Subqueryable<ProjectManagementTaskEntity>().Where(task => task.ProjectId == project.Id && !task.IsDeleted && (task.Status == ProjectManagementDomainRules.TaskBlocked || !string.IsNullOrWhiteSpace(task.BlockedReason))).Any()
            || (project.DueDate != null && project.DueDate <= dueSoon && project.ProgressPercent < 60));
    }

    private static IReadOnlyList<string> NormalizeIds(string? value) => string.IsNullOrWhiteSpace(value)
        ? []
        : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).Take(500).ToArray();

    private static long Sequence(IEnumerable<ProjectManagementProjectEntity> projects) => projects.Select(project => project.VersionNo).DefaultIfEmpty(0).Max();
    private IProjectManagementDisplayProjectionService DisplayProjection => displayProjection ?? new ProjectManagementDisplayProjectionService(databaseAccessor);
    private static void RequirePlatformScope(ICurrentUser currentUser) => ProjectManagementPlatformScope.RequireSystemWorkspace(currentUser);
    private void RequirePlatformScope() => RequirePlatformScope(currentUser);

    public sealed class TaskProjection
    {
        public string ProjectId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? DueDate { get; set; }
        public string? BlockedReason { get; set; }
    }

    public sealed class MilestoneProjection
    {
        public string ProjectId { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
