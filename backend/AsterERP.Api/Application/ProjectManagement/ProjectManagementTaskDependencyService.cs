using System.Collections.Concurrent;
using System.Diagnostics;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// 任务依赖聚合：所有边写入都在项目级临界区和数据库事务内完成，确保同一进程的并发写入
/// 不会绕过环检测。跨进程写入仍由 SQLite 的单写者事务和唯一索引保护重复边。
/// </summary>
public sealed class ProjectManagementTaskDependencyService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    ProjectManagementAccessPolicy? accessPolicy = null,
    IProjectManagementActivityWriter? activityWriter = null,
    ProjectManagementWipCoordinator? wipCoordinator = null) : IProjectManagementTaskDependencyService, IProjectManagementTaskTemplateDependencyCommandService
{
    private const string FinishToStart = "FinishToStart";
    private const string ForcedStartReasonPrefix = "已强制开始：";
    private const int MaxBatchSize = 200;
    private const int MaxDependenciesPerProject = 20_000;
    private const int MaxForceStartReasonLength = 500;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> ProjectWriteLocks = new(StringComparer.Ordinal);

    public async Task<IReadOnlyList<ProjectManagementTaskDependencyResponse>> QueryAsync(string projectId, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        await Policy().EnsureCanViewProjectAsync(projectId, cancellationToken);
        var items = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskDependencyEntity>()
            .Where(item => item.ProjectId == projectId && !item.IsDeleted)
            .OrderBy(item => item.CreatedTime)
            .Take(MaxDependenciesPerProject + 1)
            .ToListAsync(cancellationToken);
        EnsureGraphSize(items.Count);
        return items.Select(Map).ToList();
    }

    public async Task<ProjectManagementTaskDependencyResponse> CreateAsync(string projectId, ProjectManagementTaskDependencyUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var items = await CreateInternalAsync(projectId, [request], cancellationToken);
        return items[0];
    }

    public async Task<IReadOnlyList<ProjectManagementTaskDependencyResponse>> CreateBatchAsync(string projectId, ProjectManagementTaskDependencyBatchCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Dependencies is null || request.Dependencies.Count == 0 || request.Dependencies.Count > MaxBatchSize)
            throw new ValidationException($"依赖批量导入数量必须在 1 到 {MaxBatchSize} 之间");
        return await CreateInternalAsync(projectId, request.Dependencies, cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectManagementTaskDependencyResponse>> CreateBatchInTransactionAsync(ProjectManagementTaskTemplateDependencyCapability capability, string projectId, ProjectManagementTaskDependencyBatchCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (!ReferenceEquals(capability, ProjectManagementTaskTemplateDependencyCapability.Instance)) throw new InvalidOperationException("任务模板依赖命令缺少内部 capability");
        if (request.Dependencies is null || request.Dependencies.Count == 0 || request.Dependencies.Count > MaxBatchSize) throw new ValidationException($"依赖批量导入数量必须在 1 到 {MaxBatchSize} 之间");
        await EnsureProjectAsync(projectId, cancellationToken);
        await Policy().EnsureCanManageDependenciesAsync(projectId, cancellationToken);
        var candidates = await NormalizeCandidatesAsync(projectId, request.Dependencies, cancellationToken);
        var existing = await LoadActiveDependenciesAsync(projectId, cancellationToken);
        EnsureGraphSize(existing.Count + candidates.Count);
        ValidateNewEdges(existing, candidates, cancellationToken);
        var now = DateTime.UtcNow;
        var userId = RequireUserId();
        var created = candidates.Select(item => new ProjectManagementTaskDependencyEntity
        {
            TenantId = RequireTenantId(), AppCode = RequireAppCode(), ProjectId = projectId, PredecessorTaskId = item.PredecessorTaskId, SuccessorTaskId = item.SuccessorTaskId,
            DependencyType = item.DependencyType, LagMinutes = item.LagMinutes, VersionNo = 1, CreatedBy = userId, CreatedTime = now
        }).ToList();
        await databaseAccessor.GetCurrentDb().Insertable(created).ExecuteCommandAsync(cancellationToken);
        await RefreshBlockedStatesCoreAsync(projectId, cancellationToken);
        foreach (var entity in created) await WriteAuditAsync(entity, "task.dependency.created", $"创建任务依赖 {entity.PredecessorTaskId} -> {entity.SuccessorTaskId}", cancellationToken);
        return created.Select(Map).ToList();
    }

    public async Task DeleteAsync(string projectId, string id, long versionNo, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        await Policy().EnsureCanManageDependenciesAsync(projectId, cancellationToken);
        await using var lease = await EnterProjectWriteAsync(projectId, cancellationToken);
        var db = databaseAccessor.GetCurrentDb();
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            var entity = (await db.Queryable<ProjectManagementTaskDependencyEntity>()
                .Where(item => item.Id == id && item.ProjectId == projectId && !item.IsDeleted)
                .Take(1)
                .ToListAsync(cancellationToken)).FirstOrDefault()
                ?? throw new NotFoundException("任务依赖不存在", ErrorCodes.PlatformResourceNotFound);
            EnsureVersion(entity.VersionNo, versionNo);
            var now = DateTime.UtcNow;
            entity.IsDeleted = true;
            entity.DeletedBy = RequireUserId();
            entity.DeletedTime = now;
            entity.UpdatedBy = entity.DeletedBy;
            entity.UpdatedTime = now;
            entity.VersionNo++;
            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
            await RefreshBlockedStatesCoreAsync(projectId, cancellationToken);
            await WriteAuditAsync(entity, "task.dependency.deleted", $"移除任务依赖 {entity.PredecessorTaskId} -> {entity.SuccessorTaskId}", cancellationToken);
        });
    }

    public async Task<ProjectManagementTaskDependencyForceStartResponse> ForceStartAsync(string projectId, string taskId, ProjectManagementTaskDependencyForceStartRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        // 强制开始不是普通任务编辑：必须是 Owner/Manager，Lead 不能越过前置依赖。
        await Policy().EnsureCanManageProjectAsync(projectId, cancellationToken);
        var reason = NormalizeForceStartReason(request.Reason);
        await using var wipLease = await WipCoordinator.EnterAsync(RequireTenantId(), RequireAppCode(), projectId, cancellationToken);
        await using var lease = await EnterProjectWriteAsync(projectId, cancellationToken);
        var db = databaseAccessor.GetCurrentDb();
        ProjectManagementTaskEntity? task = null;
        var blockerCount = 0;
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            task = await EnsureTaskAsync(projectId, taskId, cancellationToken);
            EnsureVersion(task.VersionNo, request.VersionNo, "任务已被其他用户修改，请刷新后重试");
            if (task.Status is ProjectManagementDomainRules.TaskDone or ProjectManagementDomainRules.TaskCancelled)
                throw new ValidationException("已完成或已取消的任务不能强制开始");
            var snapshot = await LoadDependencySnapshotAsync(projectId, cancellationToken);
            blockerCount = CountBlockers(task.Id, snapshot.Dependencies, snapshot.StatusByTaskId, out _);
            if (blockerCount == 0) throw new ValidationException("任务当前没有未完成前置依赖，不需要强制开始");
            var wipOverride = await EnsureWipAsync(db, projectId, task.Status != ProjectManagementDomainRules.TaskInProgress, request.OverrideWip, request.OverrideWipReason, cancellationToken);
            var now = DateTime.UtcNow;
            task.Status = ProjectManagementDomainRules.TaskInProgress;
            task.BlockedReason = ForcedStartReasonPrefix + reason;
            task.ActualStartAt ??= now;
            task.VersionNo++;
            task.UpdatedBy = RequireUserId();
            task.UpdatedTime = now;
            await db.Updateable(task).ExecuteCommandAsync(cancellationToken);
            await WriteTaskAuditAsync(task, "task.dependency.force-started", $"强制开始被前置任务阻塞的任务：{reason}", cancellationToken);
            await WriteWipOverrideAuditAsync(task, wipOverride, cancellationToken);
        });
        return new ProjectManagementTaskDependencyForceStartResponse(task!.Id, task.ProjectId, task.Status, blockerCount, reason, task.VersionNo);
    }

    public async Task RefreshBlockedStatesAsync(string projectId, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        await using var lease = await EnterProjectWriteAsync(projectId, cancellationToken);
        await RefreshBlockedStatesCoreAsync(projectId, cancellationToken);
    }

    /// <summary>
    /// 供回收站的永久删除流程调用。软删除不调用该方法，依赖行会保留以呈现“前置任务已删除”的异常关系；
    /// 只有物理删除任务时才移除关联边并写项目审计。
    /// </summary>
    public async Task<int> PurgeForTasksAsync(string projectId, IReadOnlyCollection<string> taskIds, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectId)) throw new ValidationException("项目不能为空");
        var normalizedIds = taskIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.Ordinal).ToList();
        if (normalizedIds.Count == 0) return 0;
        await using var lease = await EnterProjectWriteAsync(projectId, cancellationToken);
        var db = databaseAccessor.GetCurrentDb();
        var removed = 0;
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            removed = await PurgeForTasksCoreAsync(db, projectId, normalizedIds, cancellationToken);
        });
        return removed;
    }

    /// <summary>
    /// 回收站物理删除任务的统一事务入口：先审计并删除依赖边，再写任务物理删除审计，最后删除任务行。
    /// 项目活动记录会保留到项目自身物理删除时再统一清理，避免普通任务 purge 静默丢失审计。
    /// </summary>
    public async Task<int> PurgeDeletedTasksAsync(string projectId, IReadOnlyCollection<string> taskIds, CancellationToken cancellationToken = default)
    {
        var normalizedIds = taskIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.Ordinal).ToList();
        if (string.IsNullOrWhiteSpace(projectId) || normalizedIds.Count == 0) throw new ValidationException("永久删除任务参数无效");
        await using var lease = await EnterProjectWriteAsync(projectId, cancellationToken);
        var db = databaseAccessor.GetCurrentDb();
        var tasks = await db.Queryable<ProjectManagementTaskEntity>()
            .Where(item => item.ProjectId == projectId && normalizedIds.Contains(item.Id) && item.IsDeleted)
            .ToListAsync(cancellationToken);
        if (tasks.Count != normalizedIds.Count) throw new ValidationException("存在未删除或不属于当前项目的任务，不能永久删除");
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            await PurgeForTasksCoreAsync(db, projectId, normalizedIds, cancellationToken);
            foreach (var task in tasks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await WriteTaskAuditAsync(task, "task.purged", $"永久删除任务 {task.Title}", cancellationToken);
            }
            await db.Deleteable<ProjectManagementTaskEntity>()
                .Where(item => item.ProjectId == projectId && normalizedIds.Contains(item.Id) && item.IsDeleted)
                .ExecuteCommandAsync(cancellationToken);
        });
        return tasks.Count;
    }

    private async Task<IReadOnlyList<ProjectManagementTaskDependencyResponse>> CreateInternalAsync(string projectId, IReadOnlyList<ProjectManagementTaskDependencyUpsertRequest> requests, CancellationToken cancellationToken)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        await Policy().EnsureCanManageDependenciesAsync(projectId, cancellationToken);
        await using var lease = await EnterProjectWriteAsync(projectId, cancellationToken);
        var db = databaseAccessor.GetCurrentDb();
        var created = new List<ProjectManagementTaskDependencyEntity>(requests.Count);
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            var candidates = await NormalizeCandidatesAsync(projectId, requests, cancellationToken);
            var existing = await LoadActiveDependenciesAsync(projectId, cancellationToken);
            EnsureGraphSize(existing.Count + candidates.Count);
            ValidateNewEdges(existing, candidates, cancellationToken);
            var now = DateTime.UtcNow;
            var userId = RequireUserId();
            created.AddRange(candidates.Select(item => new ProjectManagementTaskDependencyEntity
            {
                TenantId = RequireTenantId(),
                AppCode = RequireAppCode(),
                ProjectId = projectId,
                PredecessorTaskId = item.PredecessorTaskId,
                SuccessorTaskId = item.SuccessorTaskId,
                DependencyType = item.DependencyType,
                LagMinutes = item.LagMinutes,
                VersionNo = 1,
                CreatedBy = userId,
                CreatedTime = now
            }));
            await db.Insertable(created).ExecuteCommandAsync(cancellationToken);
            await RefreshBlockedStatesCoreAsync(projectId, cancellationToken);
            foreach (var entity in created)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await WriteAuditAsync(entity, "task.dependency.created", $"创建任务依赖 {entity.PredecessorTaskId} -> {entity.SuccessorTaskId}", cancellationToken);
            }
        });
        return created.Select(Map).ToList();
    }

    private async Task<int> PurgeForTasksCoreAsync(SqlSugar.ISqlSugarClient db, string projectId, IReadOnlyCollection<string> taskIds, CancellationToken cancellationToken)
    {
        var rows = await db.Queryable<ProjectManagementTaskDependencyEntity>()
            .Where(item => item.ProjectId == projectId && (taskIds.Contains(item.PredecessorTaskId) || taskIds.Contains(item.SuccessorTaskId)))
            .ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await WriteAuditAsync(row, "task.dependency.purged", $"永久删除任务时移除依赖 {row.PredecessorTaskId} -> {row.SuccessorTaskId}", cancellationToken);
        }
        if (rows.Count > 0)
            await db.Deleteable<ProjectManagementTaskDependencyEntity>()
                .Where(item => item.ProjectId == projectId && (taskIds.Contains(item.PredecessorTaskId) || taskIds.Contains(item.SuccessorTaskId)))
                .ExecuteCommandAsync(cancellationToken);
        return rows.Count;
    }

    private async Task RefreshBlockedStatesCoreAsync(string projectId, CancellationToken cancellationToken)
    {
        var db = databaseAccessor.GetCurrentDb();
        var snapshot = await LoadDependencySnapshotAsync(projectId, cancellationToken);
        var changed = new List<ProjectManagementTaskEntity>();
        var now = DateTime.UtcNow;
        var userId = RequireUserId();
        foreach (var task in snapshot.Tasks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (task.Status is ProjectManagementDomainRules.TaskDone or ProjectManagementDomainRules.TaskCancelled) continue;
            var blockers = CountBlockers(task.Id, snapshot.Dependencies, snapshot.StatusByTaskId, out var missingPredecessorCount);
            if (blockers > 0)
            {
                if (task.Status == ProjectManagementDomainRules.TaskInProgress && IsForcedStart(task.BlockedReason)) continue;
                if (task.Status is not (ProjectManagementDomainRules.TaskBlocked or ProjectManagementDomainRules.TaskTodo or ProjectManagementDomainRules.TaskInProgress)) continue;
                var reason = BuildBlockedReason(blockers, missingPredecessorCount);
                if (task.Status == ProjectManagementDomainRules.TaskBlocked && string.Equals(task.BlockedReason, reason, StringComparison.Ordinal)) continue;
                task.Status = ProjectManagementDomainRules.TaskBlocked;
                task.BlockedReason = reason;
                MarkChanged(task, userId, now);
                changed.Add(task);
                continue;
            }
            if (task.Status == ProjectManagementDomainRules.TaskBlocked && IsDependencyBlockedReason(task.BlockedReason))
            {
                task.Status = ProjectManagementDomainRules.TaskTodo;
                task.BlockedReason = null;
                MarkChanged(task, userId, now);
                changed.Add(task);
            }
        }
        if (changed.Count > 0) await db.Updateable(changed).ExecuteCommandAsync(cancellationToken);
    }

    private async Task<DependencySnapshot> LoadDependencySnapshotAsync(string projectId, CancellationToken cancellationToken)
    {
        var db = databaseAccessor.GetCurrentDb();
        var tasks = await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.ProjectId == projectId && !item.IsDeleted).ToListAsync(cancellationToken);
        var dependencies = await LoadActiveDependenciesAsync(projectId, cancellationToken);
        return new DependencySnapshot(tasks, dependencies, tasks.ToDictionary(item => item.Id, item => item.Status, StringComparer.Ordinal));
    }

    private async Task<List<ProjectManagementTaskDependencyEntity>> LoadActiveDependenciesAsync(string projectId, CancellationToken cancellationToken)
    {
        var rows = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskDependencyEntity>()
            .Where(item => item.ProjectId == projectId && !item.IsDeleted)
            .Take(MaxDependenciesPerProject + 1)
            .ToListAsync(cancellationToken);
        EnsureGraphSize(rows.Count);
        return rows;
    }

    private async Task<List<DependencyCandidate>> NormalizeCandidatesAsync(string projectId, IReadOnlyList<ProjectManagementTaskDependencyUpsertRequest> requests, CancellationToken cancellationToken)
    {
        var candidates = new List<DependencyCandidate>(requests.Count);
        var pairs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var request in requests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var predecessor = await EnsureTaskAsync(projectId, request.PredecessorTaskId, cancellationToken);
            var successor = await EnsureTaskAsync(projectId, request.SuccessorTaskId, cancellationToken);
            if (predecessor.Id == successor.Id) throw new ValidationException("任务不能依赖自身");
            var dependencyType = NormalizeDependencyType(request.DependencyType);
            if (request.LagMinutes < 0) throw new ValidationException("依赖滞后时间不能为负数");
            var pair = predecessor.Id + "\u001f" + successor.Id;
            if (!pairs.Add(pair)) throw new ValidationException("批量任务依赖存在重复项");
            candidates.Add(new DependencyCandidate(predecessor.Id, successor.Id, dependencyType, request.LagMinutes));
        }
        return candidates;
    }

    private static void ValidateNewEdges(IReadOnlyCollection<ProjectManagementTaskDependencyEntity> existing, IReadOnlyCollection<DependencyCandidate> candidates, CancellationToken cancellationToken)
    {
        var graph = existing.GroupBy(item => item.PredecessorTaskId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.SuccessorTaskId).ToList(), StringComparer.Ordinal);
        var existingPairs = existing.Select(item => item.PredecessorTaskId + "\u001f" + item.SuccessorTaskId).ToHashSet(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pair = candidate.PredecessorTaskId + "\u001f" + candidate.SuccessorTaskId;
            if (!existingPairs.Add(pair)) throw new ValidationException("任务依赖已存在");
            var path = FindPath(graph, candidate.SuccessorTaskId, candidate.PredecessorTaskId, cancellationToken);
            if (path is not null)
            {
                path.Add(candidate.SuccessorTaskId);
                throw new ValidationException($"任务依赖不能形成循环：{string.Join(" -> ", path)}");
            }
            if (!graph.TryGetValue(candidate.PredecessorTaskId, out var successors))
            {
                successors = [];
                graph[candidate.PredecessorTaskId] = successors;
            }
            successors.Add(candidate.SuccessorTaskId);
        }
    }

    private static List<string>? FindPath(IReadOnlyDictionary<string, List<string>> graph, string start, string target, CancellationToken cancellationToken)
    {
        var queue = new Queue<string>([start]);
        var visited = new HashSet<string>(StringComparer.Ordinal) { start };
        var parent = new Dictionary<string, string>(StringComparer.Ordinal);
        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = queue.Dequeue();
            if (string.Equals(current, target, StringComparison.Ordinal))
            {
                var path = new List<string> { current };
                while (parent.TryGetValue(path[^1], out var previous)) path.Add(previous);
                path.Reverse();
                return path;
            }
            if (!graph.TryGetValue(current, out var successors)) continue;
            foreach (var successor in successors)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!visited.Add(successor)) continue;
                parent[successor] = current;
                queue.Enqueue(successor);
            }
        }
        return null;
    }

    private static int CountBlockers(string taskId, IReadOnlyCollection<ProjectManagementTaskDependencyEntity> dependencies, IReadOnlyDictionary<string, string> statusByTaskId, out int missingPredecessorCount)
    {
        missingPredecessorCount = 0;
        var blockers = 0;
        foreach (var dependency in dependencies)
        {
            if (!string.Equals(dependency.SuccessorTaskId, taskId, StringComparison.Ordinal)) continue;
            if (!statusByTaskId.TryGetValue(dependency.PredecessorTaskId, out var status))
            {
                missingPredecessorCount++;
                blockers++;
            }
            else if (!string.Equals(status, ProjectManagementDomainRules.TaskDone, StringComparison.Ordinal)) blockers++;
        }
        return blockers;
    }

    private async Task EnsureProjectAsync(string projectId, CancellationToken cancellationToken)
    {
        RequireTenantId();
        RequireAppCode();
        if (string.IsNullOrWhiteSpace(projectId) || !await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectEntity>()
                .Where(item => item.Id == projectId && !item.IsDeleted)
                .AnyAsync(cancellationToken))
            throw new NotFoundException("项目不存在", ErrorCodes.PlatformResourceNotFound);
    }

    private async Task<ProjectManagementTaskEntity> EnsureTaskAsync(string projectId, string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ValidationException("依赖任务不能为空");
        var task = (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>()
            .Where(item => item.Id == id.Trim() && item.ProjectId == projectId && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken)).FirstOrDefault();
        return task ?? throw new ValidationException("依赖任务不存在或不属于当前项目");
    }

    private async Task WriteAuditAsync(ProjectManagementTaskDependencyEntity entity, string activityType, string summary, CancellationToken cancellationToken)
    {
        if (activityWriter is null) return;
        await activityWriter.AppendAsync(new ProjectManagementActivityEvent(RequireTenantId(), RequireAppCode(), "TaskDependency", entity.Id, activityType, summary,
            Activity.Current?.Id ?? Guid.NewGuid().ToString("N"), RequireUserId(), entity.ProjectId), cancellationToken);
    }

    private async Task WriteTaskAuditAsync(ProjectManagementTaskEntity task, string activityType, string summary, CancellationToken cancellationToken)
    {
        if (activityWriter is null) return;
        await activityWriter.AppendAsync(new ProjectManagementActivityEvent(RequireTenantId(), RequireAppCode(), "Task", task.Id, activityType, summary,
            Activity.Current?.Id ?? Guid.NewGuid().ToString("N"), RequireUserId(), task.ProjectId), cancellationToken);
    }

    private async ValueTask<ProjectWriteLease> EnterProjectWriteAsync(string projectId, CancellationToken cancellationToken)
    {
        var key = RequireTenantId() + ":" + RequireAppCode() + ":" + projectId.Trim();
        var gate = ProjectWriteLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        return new ProjectWriteLease(gate);
    }

    private ProjectManagementAccessPolicy Policy() => accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser);
    private ProjectManagementWipCoordinator WipCoordinator => wipCoordinator ?? new ProjectManagementWipCoordinator();
    private async Task<ProjectManagementWipOverrideDecision?> EnsureWipAsync(ISqlSugarClient db, string projectId, bool entersInProgress, bool overrideWip, string? overrideReason, CancellationToken cancellationToken)
    {
        if (!entersInProgress) return null;
        var project = (await db.Queryable<ProjectManagementProjectEntity>().Where(item => item.Id == projectId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault()
            ?? throw new NotFoundException("项目不存在", ErrorCodes.PlatformResourceNotFound);
        if (!project.WipLimit.HasValue) return null;
        var count = await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.ProjectId == projectId && !item.IsDeleted && item.Status == ProjectManagementDomainRules.TaskInProgress).CountAsync(cancellationToken);
        if (count < project.WipLimit.Value) return null;
        if (!overrideWip) throw new ValidationException("项目 WIP 上限已达到，需要 WIP 强制绕过权限");
        if (!currentUser.HasAsterErpPermission(PermissionCodes.ProjectManagementTaskOverrideWip)) throw new ValidationException("没有 WIP 强制绕过权限", ErrorCodes.PermissionDenied);
        return new ProjectManagementWipOverrideDecision(project.WipLimit.Value, count, NormalizeWipOverrideReason(overrideReason));
    }
    private async Task WriteWipOverrideAuditAsync(ProjectManagementTaskEntity task, ProjectManagementWipOverrideDecision? decision, CancellationToken cancellationToken)
    {
        if (decision is null || activityWriter is null) return;
        await activityWriter.AppendAsync(new ProjectManagementActivityEvent(RequireTenantId(), RequireAppCode(), "Task", task.Id, "task.wip-overridden",
            $"依赖强制开始时超过 WIP 上限：{decision.Reason}", Activity.Current?.Id ?? Guid.NewGuid().ToString("N"), RequireUserId(), task.ProjectId,
            Source: "Governance", FieldChanges:
            [new ProjectManagementActivityFieldChange("WipLimit", "WIP 上限", decision.Limit.ToString(), decision.Limit.ToString()),
             new ProjectManagementActivityFieldChange("InProgressCount", "开始前进行中任务数", decision.InProgressCount.ToString(), (decision.InProgressCount + 1).ToString()),
             new ProjectManagementActivityFieldChange("OverrideReason", "强制原因", null, decision.Reason)]), cancellationToken);
    }
    private static void EnsureGraphSize(int count) { if (count > MaxDependenciesPerProject) throw new ValidationException($"项目任务依赖数量不能超过 {MaxDependenciesPerProject}"); }
    private static string NormalizeDependencyType(string? value)
    {
        var type = ProjectManagementDomainRules.RequireDependencyType(value ?? string.Empty);
        return string.Equals(type, FinishToStart, StringComparison.Ordinal)
            ? type
            : throw new ValidationException("当前仅支持 FinishToStart 任务依赖");
    }
    private static string NormalizeForceStartReason(string? value)
    {
        var reason = value?.Trim() ?? string.Empty;
        if (reason.Length == 0 || reason.Length > MaxForceStartReasonLength) throw new ValidationException($"强制开始原因必须在 1 到 {MaxForceStartReasonLength} 个字符之间");
        return reason;
    }
    private static string NormalizeWipOverrideReason(string? value)
    {
        var reason = value?.Trim() ?? string.Empty;
        if (reason.Length is < 1 or > 500) throw new ValidationException("WIP 强制绕过原因必须在 1 到 500 个字符之间");
        return reason;
    }
    private static string BuildBlockedReason(int blockerCount, int missingPredecessorCount) => missingPredecessorCount == 0
        ? $"存在 {blockerCount} 个未完成前置任务"
        : $"存在 {blockerCount} 个未完成前置任务，其中 {missingPredecessorCount} 个前置任务已删除";
    private static bool IsDependencyBlockedReason(string? value) => value?.StartsWith("存在 ", StringComparison.Ordinal) == true && value.Contains("前置任务", StringComparison.Ordinal);
    private static bool IsForcedStart(string? value) => value?.StartsWith(ForcedStartReasonPrefix, StringComparison.Ordinal) == true;
    private static void MarkChanged(ProjectManagementTaskEntity task, string userId, DateTime now) { task.VersionNo++; task.UpdatedBy = userId; task.UpdatedTime = now; }
    private static void EnsureVersion(long actual, long expected, string message = "任务依赖已被其他用户修改，请刷新后重试") { if (expected <= 0 || actual != expected) throw new ValidationException(message, ErrorCodes.ApplicationDevelopmentPageRevisionConflict); }
    private string RequireTenantId() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户");
    private string RequireAppCode() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用");
    private string RequireUserId() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户");
    private static ProjectManagementTaskDependencyResponse Map(ProjectManagementTaskDependencyEntity entity) => new(entity.Id, entity.ProjectId, entity.PredecessorTaskId, entity.SuccessorTaskId, entity.DependencyType, entity.LagMinutes, entity.VersionNo);

    private sealed record DependencyCandidate(string PredecessorTaskId, string SuccessorTaskId, string DependencyType, int LagMinutes);
    private sealed record ProjectManagementWipOverrideDecision(int Limit, int InProgressCount, string Reason);
    private sealed record DependencySnapshot(IReadOnlyList<ProjectManagementTaskEntity> Tasks, IReadOnlyList<ProjectManagementTaskDependencyEntity> Dependencies, IReadOnlyDictionary<string, string> StatusByTaskId);
    private sealed class ProjectWriteLease(SemaphoreSlim gate) : IAsyncDisposable { public ValueTask DisposeAsync() { gate.Release(); return ValueTask.CompletedTask; } }
}
