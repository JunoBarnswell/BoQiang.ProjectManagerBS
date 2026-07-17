using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationDataSourceSqliteSandbox
{
    private readonly IHostEnvironment environment;
    private readonly ApplicationDataCenterWorkspaceResolver workspaceResolver;
    private readonly ApplicationDataSourceSqlitePathApprovalService approvalService;

    public ApplicationDataSourceSqliteSandbox(
        IHostEnvironment environment,
        ApplicationDataCenterWorkspaceResolver workspaceResolver,
        ApplicationDataSourceSqlitePathApprovalService approvalService)
    {
        this.environment = environment;
        this.workspaceResolver = workspaceResolver;
        this.approvalService = approvalService;
    }

    internal ApplicationDataSourceSqliteSandbox(IHostEnvironment environment)
    {
        this.environment = environment;
        workspaceResolver = null!;
        approvalService = null!;
    }

    public string Resolve(string configuredPath)
    {
        var path = NormalizePath(configuredPath);
        if (Path.IsPathRooted(path))
        {
            throw new ValidationException("SQLite 生产连接必须使用 sandbox 相对路径；绝对路径必须先完成审批", ErrorCodes.PermissionDenied);
        }

        var workspace = workspaceResolver?.Resolve()
            ?? throw new ValidationException("SQLite sandbox 需要已认证的租户应用工作区", ErrorCodes.PermissionDenied);
        var root = GetWorkspaceRoot(workspace);
        var candidate = Path.GetFullPath(Path.Combine(root, path));
        EnsureInsideRoot(root, candidate);
        EnsureNoSymbolicLink(candidate, root);
        Directory.CreateDirectory(root);
        return candidate;
    }

    public async Task<string> ResolveAsync(
        string configuredPath,
        string dataSourceId,
        CancellationToken cancellationToken = default)
    {
        var path = NormalizePath(configuredPath);
        if (Path.IsPathRooted(path))
        {
            if (workspaceResolver is null && approvalService is null)
                return path;
            if (approvalService is null)
                throw new ValidationException("SQLite 绝对路径审批服务不可用，已安全拒绝", ErrorCodes.PermissionDenied);
            var approvedPath = path;
            await approvalService.RequireActiveAsync(dataSourceId, approvedPath, cancellationToken);
            EnsureNoSymbolicLink(approvedPath, null);
            return approvedPath;
        }

        return Resolve(path);
    }

    private string GetWorkspaceRoot(ApplicationDataCenterWorkspace workspace) =>
        Path.GetFullPath(Path.Combine(
            environment.ContentRootPath,
            "data",
            "application-databases",
            workspace.TenantId,
            workspace.AppCode));

    private static string NormalizePath(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath) || configuredPath.IndexOf('\0') >= 0)
            throw new ValidationException("SQLite 文件路径不能为空且不能包含非法字符", ErrorCodes.ApplicationDataCenterInvalidConfig);
        try
        {
            var path = configuredPath.Trim();
            _ = Path.GetFullPath(path);
            return path;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ValidationException("SQLite 文件路径格式无效", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }
    }

    private static void EnsureInsideRoot(string root, string candidate)
    {
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("SQLite 文件路径不在租户应用 sandbox 内", ErrorCodes.ApplicationDataCenterInvalidConfig);
    }

    private static void EnsureNoSymbolicLink(string candidate, string? root)
    {
        var current = Path.GetFullPath(candidate);
        var boundary = string.IsNullOrWhiteSpace(root) ? Path.GetPathRoot(current) : Path.GetFullPath(root);
        while (!string.IsNullOrWhiteSpace(current) &&
               (string.IsNullOrWhiteSpace(boundary) || current.StartsWith(boundary, StringComparison.OrdinalIgnoreCase)))
        {
            if (File.Exists(current) && File.ResolveLinkTarget(current, true) is not null)
                throw new ValidationException("SQLite 文件路径不能指向符号链接", ErrorCodes.ApplicationDataCenterInvalidConfig);
            if (Directory.Exists(current) && Directory.ResolveLinkTarget(current, true) is not null)
                throw new ValidationException("SQLite 文件路径不能经过符号链接目录", ErrorCodes.ApplicationDataCenterInvalidConfig);

            var parent = Path.GetDirectoryName(current);
            if (string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
                break;
            current = parent ?? string.Empty;
        }
    }
}
