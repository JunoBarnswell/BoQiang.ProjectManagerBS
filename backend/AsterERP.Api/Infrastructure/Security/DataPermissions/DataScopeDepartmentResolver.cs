using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.System.Organizations;

namespace AsterERP.Api.Infrastructure.Security.DataPermissions;

public sealed class DataScopeDepartmentResolver(IWorkspaceDatabaseAccessor databaseAccessor) : IDataScopeDepartmentResolver
{
    public async Task<IReadOnlyList<string>> ResolveDepartmentAndChildrenAsync(
        string? rootDeptId,
        CancellationToken cancellationToken = default)
    {
        var normalizedRootDeptId = rootDeptId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedRootDeptId))
        {
            return [];
        }

        var departments = await databaseAccessor.GetCurrentDb().Queryable<SystemDepartmentEntity>()
            .Where(item => !item.IsDeleted)
            .ToListAsync(cancellationToken);

        var result = new List<string> { normalizedRootDeptId };
        var queue = new Queue<string>();
        queue.Enqueue(normalizedRootDeptId);

        while (queue.Count > 0)
        {
            var currentDeptId = queue.Dequeue();
            var children = departments
                .Where(item => string.Equals(item.ParentId, currentDeptId, StringComparison.OrdinalIgnoreCase))
                .Select(item => item.Id)
                .ToList();

            foreach (var child in children)
            {
                if (result.Contains(child, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(child);
                queue.Enqueue(child);
            }
        }

        return result;
    }
}
