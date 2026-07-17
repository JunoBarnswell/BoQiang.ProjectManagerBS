using AsterERP.Api.Infrastructure.Publishing;
using AsterERP.Api.Modules.Platform;
using AsterERP.Contracts.Platform;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.Extensions.Options;
using SqlSugar;
using Volo.Abp.BackgroundJobs;

namespace AsterERP.Api.Application.Platform.ApplicationPublishing;

public sealed class PlatformApplicationPublishService(
    ISqlSugarClient db,
    PlatformAccessGuard accessGuard,
    IBackgroundJobManager backgroundJobManager,
    ApplicationPublishLockRegistry lockRegistry,
    ApplicationPublishPathGuard pathGuard,
    ApplicationPublishPackageWriter packageWriter,
    IOptions<ApplicationPublishOptions> options) : IPlatformApplicationPublishService
{
    public async Task<ApplicationPublishTaskResponse> PublishAsync(
        string appId,
        ApplicationPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        accessGuard.EnsurePlatformAdmin();
        var app = await GetAppAsync(appId, cancellationToken);
        var tenantId = RequireTenantId(request.TenantId);
        await EnsureTenantApplicationAsync(tenantId, app.AppCode, cancellationToken);
        var profile = await GetProfileAsync(app.AppCode, cancellationToken);
        var backendHost = NormalizeBackendHost(NormalizeOptional(request.BackendHost) ?? profile?.BackendHost);
        var backendPort = NormalizeBackendPort(request.BackendPort ?? profile?.BackendPort);
        var frontendBasePath = NormalizeFrontendBasePath(NormalizeOptional(request.FrontendBasePath) ?? profile?.FrontendBasePath, app.AppCode);
        var frontendApiBaseUrl = NormalizeFrontendApiBaseUrl(NormalizeOptional(request.FrontendApiBaseUrl) ?? profile?.FrontendApiBaseUrl);
        var task = new SystemApplicationPublishTaskEntity
        {
            AppId = app.Id,
            AppCode = app.AppCode,
            TenantId = tenantId,
            Version = NormalizeOptional(request.Version) ?? app.Version,
            Status = ApplicationPublishConstants.StatusPending,
            Stage = ApplicationPublishConstants.StageQueued,
            ProgressPercent = 0,
            TraceId = Guid.NewGuid().ToString("N"),
            RuntimeIdentifier = profile?.RuntimeIdentifier ?? options.Value.RuntimeIdentifier,
            SelfContained = profile?.SelfContained ?? options.Value.SelfContained,
            IncludeFrontend = request.IncludeFrontend && (profile?.IncludeFrontend ?? true),
            IncludeBackend = request.IncludeBackend && (profile?.IncludeBackend ?? true),
            CleanOutput = request.CleanOutput,
            BackendHost = backendHost,
            BackendPort = backendPort,
            FrontendBasePath = frontendBasePath,
            FrontendApiBaseUrl = frontendApiBaseUrl,
            Remark = NormalizeOptional(request.Remark)
        };

        using (var publishLock = await lockRegistry.TryAcquireAsync(app.AppCode, cancellationToken))
        {
            if (publishLock is null)
            {
                throw new ValidationException("当前应用已有发布任务正在执行，请等待任务结束后再发布");
            }

            db.Ado.BeginTran();
            try
            {
                var activeExists = await db.Queryable<SystemApplicationPublishTaskEntity>()
                    .Where(item =>
                        item.AppCode == app.AppCode &&
                        !item.IsDeleted &&
                        (item.Status == ApplicationPublishConstants.StatusPending ||
                         item.Status == ApplicationPublishConstants.StatusRunning))
                    .AnyAsync(cancellationToken);
                if (activeExists)
                {
                    throw new ValidationException("当前应用已有发布任务正在执行，请等待任务结束后再发布");
                }

                await db.Insertable(task).ExecuteCommandAsync(cancellationToken);
                await InsertLogAsync(task, "Info", ApplicationPublishConstants.StageQueued, "发布任务已创建", cancellationToken);
                db.Ado.CommitTran();
            }
            catch
            {
                db.Ado.RollbackTran();
                throw;
            }
        }

        try
        {
            await backgroundJobManager.EnqueueAsync(new PlatformApplicationPublishJobArgs(task.Id));
        }
        catch (Exception ex)
        {
            task.Status = ApplicationPublishConstants.StatusFailed;
            task.Stage = ApplicationPublishConstants.StageQueued;
            task.ProgressPercent = 100;
            task.ErrorMessage = ex.Message;
            task.FinishedAt = DateTime.UtcNow;
            task.UpdatedTime = task.FinishedAt;
            await db.Updateable(task).ExecuteCommandAsync(cancellationToken);
            await InsertLogAsync(task, "Error", ApplicationPublishConstants.StageQueued, $"发布任务入队失败：{ex.Message}", cancellationToken);
            throw;
        }

        return MapTask(task, app.AppName);
    }

    public async Task<GridPageResult<ApplicationPublishTaskResponse>> GetTasksAsync(
        string appId,
        GridQuery gridQuery,
        CancellationToken cancellationToken = default)
    {
        accessGuard.EnsurePlatformAdmin();
        var app = await GetAppAsync(appId, cancellationToken);
        var total = new RefAsync<int>();
        var tasks = await db.Queryable<SystemApplicationPublishTaskEntity>()
            .Where(item => item.AppId == app.Id && !item.IsDeleted)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(gridQuery.PageIndex, gridQuery.PageSize, total);

        return new GridPageResult<ApplicationPublishTaskResponse>
        {
            Total = total.Value,
            Items = tasks.Select(task => MapTask(task, app.AppName)).ToList()
        };
    }

    public async Task<ApplicationPublishTaskResponse> GetTaskAsync(
        string taskId,
        CancellationToken cancellationToken = default)
    {
        accessGuard.EnsurePlatformAdmin();
        var task = await GetTaskEntityAsync(taskId, cancellationToken);
        var app = await GetAppAsync(task.AppId, cancellationToken);
        return MapTask(task, app.AppName);
    }

    public async Task<GridPageResult<ApplicationPublishLogResponse>> GetLogsAsync(
        string taskId,
        GridQuery gridQuery,
        CancellationToken cancellationToken = default)
    {
        accessGuard.EnsurePlatformAdmin();
        _ = await GetTaskEntityAsync(taskId, cancellationToken);
        var total = new RefAsync<int>();
        var logs = await db.Queryable<SystemApplicationPublishLogEntity>()
            .Where(item => item.TaskId == taskId && !item.IsDeleted)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(gridQuery.PageIndex, gridQuery.PageSize, total);

        return new GridPageResult<ApplicationPublishLogResponse>
        {
            Total = total.Value,
            Items = logs.Select(MapLog).ToList()
        };
    }

    public async Task<GridPageResult<ApplicationPublishArtifactResponse>> GetArtifactsAsync(
        string appId,
        GridQuery gridQuery,
        CancellationToken cancellationToken = default)
    {
        accessGuard.EnsurePlatformAdmin();
        var app = await GetAppAsync(appId, cancellationToken);
        var taskIds = await db.Queryable<SystemApplicationPublishTaskEntity>()
            .Where(item => item.AppId == app.Id && !item.IsDeleted)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        var total = new RefAsync<int>();
        List<SystemApplicationPublishArtifactEntity> artifacts;
        if (taskIds.Count == 0)
        {
            artifacts = [];
        }
        else
        {
            artifacts = await db.Queryable<SystemApplicationPublishArtifactEntity>()
                .Where(item => taskIds.Contains(item.TaskId) && !item.IsDeleted)
                .OrderBy(item => item.CreatedTime, OrderByType.Desc)
                .ToPageListAsync(gridQuery.PageIndex, gridQuery.PageSize, total);
        }

        return new GridPageResult<ApplicationPublishArtifactResponse>
        {
            Total = total.Value,
            Items = artifacts.Select(MapArtifact).ToList()
        };
    }

    public async Task<ApplicationPublishArtifactResponse> PackageTaskAsync(
        string taskId,
        CancellationToken cancellationToken = default)
    {
        accessGuard.EnsurePlatformAdmin();
        var task = await GetTaskEntityAsync(taskId, cancellationToken);
        if (task.Status == ApplicationPublishConstants.StatusPending ||
            task.Status == ApplicationPublishConstants.StatusRunning)
        {
            throw new ValidationException("发布任务仍在执行，不能重新打包产物");
        }

        if (task.Status != ApplicationPublishConstants.StatusSucceeded)
        {
            throw new ValidationException("只有发布成功的任务可以重新打包产物");
        }

        var outputRoot = pathGuard.ResolveOutputRoot();
        var taskRoot = pathGuard.ResolveTaskRoot(task.AppCode, task.Id);
        taskRoot = pathGuard.EnsureInsideRoot(taskRoot, outputRoot);
        EnsurePackageSourceExists(taskRoot);

        var artifactsRoot = pathGuard.EnsureInsideRoot(Path.Combine(taskRoot, "artifacts"), outputRoot);
        Directory.CreateDirectory(artifactsRoot);
        var artifactPath = pathGuard.EnsureInsideRoot(
            Path.Combine(artifactsRoot, $"{task.AppCode}-{task.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}.zip"),
            outputRoot);

        var package = await packageWriter.ZipPackageAsync(taskRoot, artifactPath, cancellationToken);
        var artifact = new SystemApplicationPublishArtifactEntity
        {
            TaskId = task.Id,
            FileName = Path.GetFileName(artifactPath),
            ContentType = "application/zip",
            SizeBytes = package.SizeBytes,
            Sha256 = package.Sha256,
            StoredPath = package.ArtifactPath
        };

        db.Ado.BeginTran();
        try
        {
            await db.Insertable(artifact).ExecuteCommandAsync(cancellationToken);
            task.ArtifactPath = package.ArtifactPath;
            task.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(task)
                .UpdateColumns(item => new { item.ArtifactPath, item.UpdatedTime })
                .ExecuteCommandAsync(cancellationToken);
            await InsertLogAsync(task, "Info", ApplicationPublishConstants.StagePackage, $"发布产物已重新打包：{artifact.FileName}", cancellationToken);
            db.Ado.CommitTran();
        }
        catch
        {
            db.Ado.RollbackTran();
            throw;
        }

        return MapArtifact(artifact);
    }

    public async Task<(ApplicationPublishArtifactResponse Metadata, Stream Stream)> DownloadArtifactAsync(
        string artifactId,
        CancellationToken cancellationToken = default)
    {
        accessGuard.EnsurePlatformAdmin();
        var artifact = await GetArtifactAsync(artifactId, cancellationToken);
        var artifactPath = ResolveArtifactPath(artifact);
        if (!File.Exists(artifactPath))
        {
            throw new NotFoundException("发布产物文件不存在", ErrorCodes.FileNotFound);
        }

        Stream stream = new FileStream(artifactPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return (MapArtifact(artifact), stream);
    }

    public async Task DeleteArtifactAsync(
        string artifactId,
        CancellationToken cancellationToken = default)
    {
        accessGuard.EnsurePlatformAdmin();
        var artifact = await GetArtifactAsync(artifactId, cancellationToken);
        var artifactPath = ResolveArtifactPath(artifact);
        if (File.Exists(artifactPath))
        {
            File.Delete(artifactPath);
        }

        artifact.IsDeleted = true;
        artifact.DeletedTime = DateTime.UtcNow;
        artifact.UpdatedTime = artifact.DeletedTime;
        await db.Updateable(artifact)
            .UpdateColumns(item => new { item.IsDeleted, item.DeletedTime, item.UpdatedTime })
            .ExecuteCommandAsync(cancellationToken);
    }

    private async Task<SystemApplicationEntity> GetAppAsync(string appId, CancellationToken cancellationToken)
    {
        return (await db.Queryable<SystemApplicationEntity>()
            .Where(item => item.Id == appId && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new NotFoundException("应用不存在", ErrorCodes.PlatformResourceNotFound);
    }

    private async Task<SystemApplicationPublishTaskEntity> GetTaskEntityAsync(
        string taskId,
        CancellationToken cancellationToken)
    {
        return (await db.Queryable<SystemApplicationPublishTaskEntity>()
            .Where(item => item.Id == taskId && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new NotFoundException("发布任务不存在", ErrorCodes.PlatformResourceNotFound);
    }

    private async Task<SystemApplicationPublishArtifactEntity> GetArtifactAsync(
        string artifactId,
        CancellationToken cancellationToken)
    {
        return (await db.Queryable<SystemApplicationPublishArtifactEntity>()
            .Where(item => item.Id == artifactId && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new NotFoundException("发布产物不存在", ErrorCodes.FileNotFound);
    }

    private async Task<SystemApplicationPublishProfileEntity?> GetProfileAsync(
        string appCode,
        CancellationToken cancellationToken)
    {
        return (await db.Queryable<SystemApplicationPublishProfileEntity>()
            .Where(item => item.AppCode == appCode && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
    }

    private async Task EnsureTenantApplicationAsync(
        string tenantId,
        string appCode,
        CancellationToken cancellationToken)
    {
        var tenantApp = (await db.Queryable<SystemTenantAppEntity>()
                .Where(item =>
                    item.TenantId == tenantId &&
                    item.AppCode == appCode &&
                    !item.IsDeleted &&
                    item.Status == "Enabled")
                .Take(1)
                .ToListAsync(cancellationToken))
            .FirstOrDefault();

        if (tenantApp is null || (tenantApp.ExpiredAt.HasValue && tenantApp.ExpiredAt.Value <= DateTime.UtcNow))
        {
            throw new ValidationException(
                $"目标租户未启用应用 {appCode}，发布任务必须限定在一个有效的 TenantId + AppCode 工作区");
        }
    }

    private static string RequireTenantId(string? value)
    {
        var tenantId = NormalizeOptional(value);
        if (tenantId is null)
        {
            throw new ValidationException("发布任务必须明确指定 TenantId，不允许聚合所有租户的数据");
        }

        return tenantId;
    }

    private async Task InsertLogAsync(
        SystemApplicationPublishTaskEntity task,
        string level,
        string stage,
        string message,
        CancellationToken cancellationToken)
    {
        await db.Insertable(new SystemApplicationPublishLogEntity
        {
            TaskId = task.Id,
            Level = level,
            Stage = stage,
            Message = Truncate(message, 1800),
            TraceId = task.TraceId
        }).ExecuteCommandAsync(cancellationToken);
    }

    private ApplicationPublishTaskResponse MapTask(
        SystemApplicationPublishTaskEntity task,
        string appName) =>
        new(
            task.Id,
            task.AppId,
            task.AppCode,
            appName,
            task.TenantId,
            task.Version,
            task.Status,
            task.Stage,
            task.ProgressPercent,
            task.StartedAt,
            task.FinishedAt,
            task.DurationMs,
            ToPublishRelativePath(task.SourceProjectPath),
            ToPublishRelativePath(task.ReleasePath),
            ToPublishRelativePath(task.ArtifactPath),
            task.BackendHost,
            task.BackendPort,
            task.FrontendBasePath,
            task.FrontendApiBaseUrl,
            task.ErrorMessage,
            task.TraceId,
            task.CreatedTime,
            task.Remark);

    private static ApplicationPublishLogResponse MapLog(SystemApplicationPublishLogEntity log) =>
        new(log.Id, log.TaskId, log.Level, log.Stage, log.Message, log.TraceId, log.CreatedTime);

    private ApplicationPublishArtifactResponse MapArtifact(SystemApplicationPublishArtifactEntity artifact) =>
        new(
            artifact.Id,
            artifact.TaskId,
            artifact.FileName,
            artifact.ContentType,
            artifact.SizeBytes,
            artifact.Sha256,
            ToPublishRelativePath(artifact.StoredPath) ?? artifact.FileName,
            artifact.CreatedTime,
            artifact.ExpiresAt,
            $"/api/platform/application-publish-artifacts/{artifact.Id}/download");

    private string ResolveArtifactPath(SystemApplicationPublishArtifactEntity artifact)
    {
        var outputRoot = pathGuard.ResolveOutputRoot();
        var storedPath = Path.IsPathRooted(artifact.StoredPath)
            ? artifact.StoredPath
            : Path.Combine(outputRoot, artifact.StoredPath);
        return pathGuard.EnsureInsideRoot(storedPath, outputRoot);
    }

    private string? ToPublishRelativePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var outputRoot = pathGuard.ResolveOutputRoot();
        var fullPath = Path.IsPathRooted(path)
            ? path
            : Path.Combine(outputRoot, path);
        var safePath = pathGuard.EnsureInsideRoot(fullPath, outputRoot);
        return Path.GetRelativePath(outputRoot, safePath).Replace('\\', '/');
    }

    private static void EnsurePackageSourceExists(string taskRoot)
    {
        var requiredDirectories = new[]
        {
            Path.Combine(taskRoot, "source"),
            Path.Combine(taskRoot, "release"),
            Path.Combine(taskRoot, "manifest")
        };
        var missing = requiredDirectories
            .Where(path => !Directory.Exists(path))
            .Select(Path.GetFileName)
            .ToList();
        if (missing.Count > 0)
        {
            throw new ValidationException($"发布任务缺少可打包目录：{string.Join(", ", missing)}");
        }
    }

    private static string NormalizeBackendHost(string? value)
    {
        var normalized = NormalizeOptional(value) ?? "127.0.0.1";
        if (normalized.Contains("://", StringComparison.Ordinal) ||
            normalized.Contains('/', StringComparison.Ordinal) ||
            normalized.Contains('\\', StringComparison.Ordinal) ||
            normalized.Contains(':', StringComparison.Ordinal) ||
            normalized.Contains('?', StringComparison.Ordinal) ||
            normalized.Contains('#', StringComparison.Ordinal))
        {
            throw new ValidationException("后端监听地址只允许填写主机名或 IP，不包含协议、端口或路径");
        }

        if (Uri.CheckHostName(normalized) == UriHostNameType.Unknown)
        {
            throw new ValidationException("后端监听地址不是有效主机名或 IP");
        }

        return normalized;
    }

    private static int NormalizeBackendPort(int? value)
    {
        var port = value ?? 5000;
        if (port is < 1 or > 65535)
        {
            throw new ValidationException("后端监听端口必须在 1 到 65535 之间");
        }

        return port;
    }

    private static string NormalizeFrontendBasePath(string? value, string appCode)
    {
        var normalized = NormalizeOptional(value) ?? $"/{appCode}";
        normalized = normalized.Replace('\\', '/').Trim();
        if (!normalized.StartsWith("/", StringComparison.Ordinal))
        {
            normalized = "/" + normalized;
        }

        normalized = normalized.TrimEnd('/');
        if (normalized.Length == 0 || normalized == "/")
        {
            throw new ValidationException("前端访问路径必须是应用级子路径，不能使用站点根路径");
        }

        if (normalized.Contains("..", StringComparison.Ordinal) ||
            normalized.Contains('?', StringComparison.Ordinal) ||
            normalized.Contains('#', StringComparison.Ordinal))
        {
            throw new ValidationException("前端访问路径不能包含路径穿越、查询参数或片段");
        }

        if (IsReservedFrontendBasePath(normalized))
        {
            throw new ValidationException("前端访问路径不能使用 API 或 SignalR 保留路径");
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 1 || segments.Any(segment => segment.Length > 64 || !segment.All(IsSafePathSegmentChar)))
        {
            throw new ValidationException("前端访问路径必须是单段子路径，仅允许字母、数字、下划线和中划线");
        }

        return "/" + string.Join('/', segments);
    }

    private static string NormalizeFrontendApiBaseUrl(string? value)
    {
        var normalized = NormalizeOptional(value) ?? "/api";
        normalized = normalized.Replace('\\', '/').Trim();
        if (normalized.Contains('#', StringComparison.Ordinal))
        {
            throw new ValidationException("前端 API 地址不能包含片段");
        }

        if (Uri.TryCreate(normalized, UriKind.Absolute, out var absoluteUri))
        {
            if (absoluteUri.Scheme != Uri.UriSchemeHttp && absoluteUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new ValidationException("前端 API 地址只允许 http 或 https");
            }

            return normalized.TrimEnd('/');
        }

        if (!normalized.StartsWith("/", StringComparison.Ordinal) ||
            normalized.StartsWith("//", StringComparison.Ordinal) ||
            normalized.Contains("..", StringComparison.Ordinal) ||
            normalized.Contains('?', StringComparison.Ordinal))
        {
            throw new ValidationException("前端 API 地址必须是绝对 URL 或以 / 开头的站内路径");
        }

        return normalized.Length == 1 ? normalized : normalized.TrimEnd('/');
    }

    private static bool IsReservedFrontendBasePath(string path) =>
        path.Equals("/api", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("/hubs", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/hubs/", StringComparison.OrdinalIgnoreCase);

    private static bool IsSafePathSegmentChar(char value) =>
        char.IsAsciiLetterOrDigit(value) || value is '-' or '_';

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
