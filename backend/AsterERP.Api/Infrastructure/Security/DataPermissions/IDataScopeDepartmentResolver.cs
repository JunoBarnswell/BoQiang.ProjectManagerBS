namespace AsterERP.Api.Infrastructure.Security.DataPermissions;

public interface IDataScopeDepartmentResolver
{
    Task<IReadOnlyList<string>> ResolveDepartmentAndChildrenAsync(
        string? rootDeptId,
        CancellationToken cancellationToken = default);
}
