using AsterERP.Api.Infrastructure.Abp.Settings;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Settings;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// 任务树的唯一语义入口。任何创建、更新、移动和删除策略都必须通过此类校验父子归属、
/// 环路、层级上限和子树深度，避免不同命令产生不一致的树结构。
/// </summary>
public sealed class ProjectManagementTaskHierarchy(ISettingProvider? settingProvider = null)
{
    public const string CascadeDeleteMode = "Cascade";
    public const string PromoteChildrenDeleteMode = "PromoteChildren";

    public async Task<ProjectManagementTaskPlacement> ResolvePlacementAsync(
        ISqlSugarClient db,
        string projectId,
        string? parentTaskId,
        string? movingTaskId,
        CancellationToken cancellationToken = default)
    {
        var tasks = await LoadActiveTasksAsync(db, projectId, cancellationToken);
        var taskById = tasks.ToDictionary(item => item.Id, StringComparer.Ordinal);
        EnsureNoOrphans(tasks, taskById);

        var normalizedParentId = NormalizeOptional(parentTaskId);
        if (movingTaskId is not null && string.Equals(normalizedParentId, movingTaskId, StringComparison.Ordinal))
            throw new ValidationException("任务不能成为自己的父任务");

        ProjectManagementTaskEntity? parent = null;
        if (normalizedParentId is not null && !taskById.TryGetValue(normalizedParentId, out parent))
            throw new ValidationException("父任务不存在或不属于当前项目");

        ProjectManagementTaskEntity? root = null;
        IReadOnlyList<ProjectManagementTaskEntity> subtree = [];
        if (movingTaskId is not null)
        {
            if (!taskById.TryGetValue(movingTaskId, out root))
                throw new ValidationException("待移动任务不存在或不属于当前项目");

            subtree = BuildSubtree(tasks, root.Id);
            if (parent is not null)
                EnsureNoCycle(parent.Id, root.Id, taskById);
        }

        var rootDepth = parent is null ? 0 : parent.Depth + 1;
        var maximumDepth = await GetMaximumDepthAsync(cancellationToken);
        var deepestRelativeDepth = root is null ? 0 : subtree.Max(item => item.Depth - root.Depth);
        if (rootDepth + deepestRelativeDepth >= maximumDepth)
            throw new ValidationException($"任务层级不能超过 {maximumDepth} 层");

        return new ProjectManagementTaskPlacement(parent, root, subtree, rootDepth, maximumDepth);
    }

    public async Task<IReadOnlyList<ProjectManagementTaskEntity>> LoadSubtreeAsync(
        ISqlSugarClient db,
        string projectId,
        string rootTaskId,
        CancellationToken cancellationToken = default)
    {
        var tasks = await db.Queryable<ProjectManagementTaskEntity>()
            .Where(item => item.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        var root = tasks.FirstOrDefault(item => item.Id == rootTaskId)
            ?? throw new ValidationException("任务树根节点不存在或不属于当前项目");
        return BuildSubtree(tasks, root.Id);
    }

    public async Task UpdateDescendantDepthsAsync(
        ISqlSugarClient db,
        ProjectManagementTaskPlacement placement,
        DateTime now,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (placement.Root is null) return;
        foreach (var descendant in placement.Subtree.Where(item => item.Id != placement.Root.Id))
        {
            var expectedVersion = descendant.VersionNo;
            descendant.Depth = placement.RootDepth + (descendant.Depth - placement.Root.Depth);
            descendant.VersionNo++;
            descendant.UpdatedBy = userId;
            descendant.UpdatedTime = now;
            await EnsureExactlyOneRowAsync(
                db.Updateable(descendant)
                    .UpdateColumns(item => new { item.Depth, item.VersionNo, item.UpdatedBy, item.UpdatedTime })
                    .Where(item => item.Id == descendant.Id && item.VersionNo == expectedVersion),
                cancellationToken);
        }
    }

    public async Task PromoteChildrenAsync(
        ISqlSugarClient db,
        ProjectManagementTaskEntity deletedTask,
        IReadOnlyList<ProjectManagementTaskEntity> activeSubtree,
        DateTime now,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var directChildren = activeSubtree
            .Where(item => string.Equals(item.ParentTaskId, deletedTask.Id, StringComparison.Ordinal))
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.CreatedTime)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToList();
        if (directChildren.Count == 0) return;

        var existingSiblings = await db.Queryable<ProjectManagementTaskEntity>()
            .Where(item => item.ProjectId == deletedTask.ProjectId && !item.IsDeleted && item.Id != deletedTask.Id &&
                (deletedTask.ParentTaskId == null ? item.ParentTaskId == null : item.ParentTaskId == deletedTask.ParentTaskId))
            .OrderBy(item => item.SortOrder, OrderByType.Desc)
            .Take(1)
            .ToListAsync(cancellationToken);
        var nextSortOrder = existingSiblings.Count == 0 ? 1024 : existingSiblings[0].SortOrder + 1024;
        if (nextSortOrder < 0 || directChildren.Count > (int.MaxValue - nextSortOrder) / 1024)
            throw new ValidationException("同级任务排序空间已满，请先重平衡");

        var depthDelta = -1;
        foreach (var task in activeSubtree.Where(item => item.Id != deletedTask.Id))
        {
            var expectedVersion = task.VersionNo;
            task.Depth += depthDelta;
            if (string.Equals(task.ParentTaskId, deletedTask.Id, StringComparison.Ordinal))
            {
                task.ParentTaskId = deletedTask.ParentTaskId;
                task.SortOrder = nextSortOrder;
                nextSortOrder += 1024;
            }
            task.VersionNo++;
            task.UpdatedBy = userId;
            task.UpdatedTime = now;
            await EnsureExactlyOneRowAsync(
                db.Updateable(task)
                    .UpdateColumns(item => new { item.ParentTaskId, item.SortOrder, item.Depth, item.VersionNo, item.UpdatedBy, item.UpdatedTime })
                    .Where(item => item.Id == task.Id && item.VersionNo == expectedVersion),
                cancellationToken);
        }
    }

    public static string RequireDeleteMode(string? mode)
    {
        var normalized = NormalizeOptional(mode) ?? CascadeDeleteMode;
        return normalized switch
        {
            CascadeDeleteMode => CascadeDeleteMode,
            PromoteChildrenDeleteMode => PromoteChildrenDeleteMode,
            _ => throw new ValidationException("任务删除策略不受支持")
        };
    }

    private async Task<int> GetMaximumDepthAsync(CancellationToken cancellationToken)
    {
        var configured = settingProvider is null
            ? null
            : await settingProvider.GetOrNullAsync(AsterErpSettingNames.ProjectManagementTaskHierarchyMaxDepth);
        return int.TryParse(configured, out var value)
            ? Math.Clamp(value, 1, ProjectManagementDomainRules.DefaultTaskHierarchyMaxDepth)
            : ProjectManagementDomainRules.DefaultTaskHierarchyMaxDepth;
    }

    private static async Task<List<ProjectManagementTaskEntity>> LoadActiveTasksAsync(ISqlSugarClient db, string projectId, CancellationToken cancellationToken) =>
        await db.Queryable<ProjectManagementTaskEntity>()
            .Where(item => item.ProjectId == projectId && !item.IsDeleted)
            .ToListAsync(cancellationToken);

    private static IReadOnlyList<ProjectManagementTaskEntity> BuildSubtree(IReadOnlyList<ProjectManagementTaskEntity> tasks, string rootTaskId)
    {
        var children = tasks.GroupBy(item => item.ParentTaskId ?? string.Empty)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var root = tasks.FirstOrDefault(item => item.Id == rootTaskId)
            ?? throw new ValidationException("任务树根节点不存在或不属于当前项目");
        var queue = new Queue<ProjectManagementTaskEntity>([root]);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var subtree = new List<ProjectManagementTaskEntity>();
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current.Id))
                throw new ValidationException($"任务树存在循环：{rootTaskId} -> {current.Id}");
            subtree.Add(current);
            if (!children.TryGetValue(current.Id, out var directChildren)) continue;
            foreach (var child in directChildren) queue.Enqueue(child);
        }
        return subtree;
    }

    private static void EnsureNoOrphans(IReadOnlyList<ProjectManagementTaskEntity> tasks, IReadOnlyDictionary<string, ProjectManagementTaskEntity> taskById)
    {
        var orphan = tasks.FirstOrDefault(item => item.ParentTaskId is not null && !taskById.ContainsKey(item.ParentTaskId));
        if (orphan is not null)
            throw new ValidationException($"任务树存在孤儿节点：{orphan.Id} 的父任务 {orphan.ParentTaskId} 不存在");
    }

    private static void EnsureNoCycle(string parentTaskId, string rootTaskId, IReadOnlyDictionary<string, ProjectManagementTaskEntity> taskById)
    {
        var path = new List<string> { rootTaskId };
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var cursor = parentTaskId;
        while (!string.IsNullOrWhiteSpace(cursor))
        {
            path.Add(cursor);
            if (!visited.Add(cursor))
                throw new ValidationException($"任务树已存在循环：{string.Join(" -> ", path)}");
            if (string.Equals(cursor, rootTaskId, StringComparison.Ordinal))
            {
                path.Add(rootTaskId);
                throw new ValidationException($"任务不能移动到自己的子孙节点下：{string.Join(" -> ", path)}");
            }
            cursor = taskById.TryGetValue(cursor, out var task) ? task.ParentTaskId : null;
        }
    }

    private static async Task EnsureExactlyOneRowAsync(IUpdateable<ProjectManagementTaskEntity> update, CancellationToken cancellationToken)
    {
        if (await update.ExecuteCommandAsync(cancellationToken) != 1)
            throw new ValidationException("任务层级已被其他用户调整，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict);
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record ProjectManagementTaskPlacement(
    ProjectManagementTaskEntity? Parent,
    ProjectManagementTaskEntity? Root,
    IReadOnlyList<ProjectManagementTaskEntity> Subtree,
    int RootDepth,
    int MaximumDepth);
