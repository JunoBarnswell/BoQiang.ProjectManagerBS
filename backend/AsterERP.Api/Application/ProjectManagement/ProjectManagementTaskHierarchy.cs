using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// 任务树的唯一解析入口。删除、恢复与移动命令共享同一子树定义，避免各自遍历导致边界不一致。
/// </summary>
public sealed class ProjectManagementTaskHierarchy
{
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
        var children = tasks.GroupBy(item => item.ParentTaskId ?? string.Empty)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var queue = new Queue<ProjectManagementTaskEntity>([root]);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var subtree = new List<ProjectManagementTaskEntity>();
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current.Id))
                throw new ValidationException("任务树存在循环，不能执行删除或恢复");
            subtree.Add(current);
            if (!children.TryGetValue(current.Id, out var directChildren)) continue;
            foreach (var child in directChildren) queue.Enqueue(child);
        }
        return subtree;
    }
}
