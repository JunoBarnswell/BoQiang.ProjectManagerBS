using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Application.ApplicationDevelopmentCenter;
using AsterERP.Api.Application.ApplicationDevelopmentCenter.Runtime;
using AsterERP.Api.Infrastructure.Publishing;
using AsterERP.Api.Modules.ApplicationDevelopmentCenter;
using AsterERP.Api.Modules.Platform;
using AsterERP.Api.Modules.Runtime;
using AsterERP.Api.Modules.System.Menus;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.Extensions.Options;
using SqlSugar;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AsterERP.Api.Application.Platform.ApplicationPublishing;

public sealed class PlatformApplicationPublishRunner(
    ISqlSugarClient db,
    ApplicationPublishLockRegistry lockRegistry,
    ApplicationPublishPathGuard pathGuard,
    ApplicationPublishSourceCollector sourceCollector,
    ApplicationPublishModuleFileMapLoader moduleFileMapLoader,
    ApplicationPublishModuleClosureResolver moduleClosureResolver,
    ApplicationPublishPackageWriter packageWriter,
    ApplicationPublishFrontendTargetWriter frontendTargetWriter,
    ApplicationPublishLeakScanner leakScanner,
    IApplicationPublishProcessRunner processRunner,
    IOptions<ApplicationPublishOptions> options,
    ApplicationDevelopmentSchemaValidator schemaValidator)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task ExecuteAsync(string taskId)
    {
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        var task = await GetTaskAsync(taskId, cancellationToken);
        var startedAt = DateTime.UtcNow;
        IDisposable? publishLock = null;
        try
        {
            publishLock = await lockRegistry.TryAcquireAsync(task.AppCode, cancellationToken);
            if (publishLock is null)
            {
                await BlockAsync(task, "当前应用已有发布任务占用执行锁，本任务已阻断以避免覆盖产物", startedAt, cancellationToken);
                return;
            }

            await UpdateTaskAsync(task, ApplicationPublishConstants.StatusRunning, ApplicationPublishConstants.StageScanning, 5, null, cancellationToken);
            await LogAsync(task, "Info", ApplicationPublishConstants.StageScanning, "开始扫描应用菜单、运行时页面和数据模型", cancellationToken);

            var app = await GetAppAsync(task.AppId, cancellationToken);
            await EnsureTaskBoundaryAsync(task, app, cancellationToken);
            var repositoryRoot = pathGuard.ResolveRepositoryRoot();
            var taskRoot = pathGuard.ResolveTaskRoot(task.AppCode, task.Id);
            var sourceRoot = Path.Combine(taskRoot, "source");
            var releaseRoot = Path.Combine(taskRoot, "release");
            var manifestRoot = Path.Combine(taskRoot, "manifest");
            var logRoot = Path.Combine(taskRoot, "publish-logs");
            var artifactsRoot = Path.Combine(taskRoot, "artifacts");
            var runtimeConfig = BuildRuntimeConfig(task, releaseRoot);

            PrepareTaskDirectory(taskRoot);
            Directory.CreateDirectory(releaseRoot);
            Directory.CreateDirectory(manifestRoot);
            Directory.CreateDirectory(logRoot);
            Directory.CreateDirectory(artifactsRoot);

            ApplicationPublishModuleFileMapLoadResult moduleFileMap;
            try
            {
                moduleFileMap = await moduleFileMapLoader.LoadAsync(repositoryRoot, cancellationToken);
            }
            catch (Exception ex)
            {
                await BlockAsync(task, $"模块文件映射加载失败：{ex.Message}", startedAt, cancellationToken);
                return;
            }

            var snapshot = await BuildDependencySnapshotAsync(task, moduleFileMap, cancellationToken);
            if (snapshot.Menus.Count == 0)
            {
                await BlockAsync(task, "目标应用没有菜单配置，无法闭合发布边界", startedAt, cancellationToken);
                return;
            }

            if (snapshot.FrontendRoutes.Any(IsRuntimePageRoute) &&
                snapshot.PageSchemas.Count == 0)
            {
                await BlockAsync(task, "目标应用包含运行时菜单但没有已发布 PageSchema，无法生成可运行发布包", startedAt, cancellationToken);
                return;
            }

            if (snapshot.UnresolvedDependencies.Count > 0)
            {
                var reasons = string.Join("；", snapshot.UnresolvedDependencies.Select(item => $"{item.Kind}:{item.Value} {item.Reason}"));
                await BlockAsync(task, $"应用依赖图无法闭合：{reasons}", startedAt, cancellationToken);
                return;
            }

            await UpdateTaskAsync(task, ApplicationPublishConstants.StatusRunning, ApplicationPublishConstants.StageSource, 20, null, cancellationToken);
            var sourceResult = await sourceCollector.CreateAsync(
                new ApplicationPublishSourceRequest(
                    repositoryRoot,
                    sourceRoot,
                    manifestRoot,
                    task.AppCode,
                    snapshot.PublishMode,
                    snapshot.ResolvedModules,
                    moduleFileMap.Map),
                cancellationToken);
            task.SourceProjectPath = sourceRoot;
            await db.Updateable(task).UpdateColumns(item => new { item.SourceProjectPath }).ExecuteCommandAsync(cancellationToken);
            await LogAsync(task, "Info", ApplicationPublishConstants.StageSource, $"源码项目已生成：{sourceRoot}", cancellationToken);

            if (task.IncludeBackend)
            {
                await UpdateTaskAsync(task, ApplicationPublishConstants.StatusRunning, ApplicationPublishConstants.StageBackend, 40, null, cancellationToken);
                await RunBackendPublishAsync(task, sourceRoot, releaseRoot, logRoot, cancellationToken);
            }

            Directory.CreateDirectory(Path.Combine(releaseRoot, "data"));
            Directory.CreateDirectory(Path.Combine(releaseRoot, "logs"));
            await WriteRuntimeFilesAsync(task, runtimeConfig, releaseRoot, manifestRoot, cancellationToken);

            if (task.IncludeFrontend)
            {
                await UpdateTaskAsync(task, ApplicationPublishConstants.StatusRunning, ApplicationPublishConstants.StageFrontend, 65, null, cancellationToken);
                await RunFrontendBuildAsync(task, sourceRoot, releaseRoot, logRoot, taskRoot, snapshot, runtimeConfig, cancellationToken);
            }

            await UpdateTaskAsync(task, ApplicationPublishConstants.StatusRunning, ApplicationPublishConstants.StageLeakScan, 78, null, cancellationToken);
            var leakScan = await leakScanner.ScanAsync(
                new ApplicationPublishLeakScanRequest(
                    repositoryRoot,
                    releaseRoot,
                    moduleFileMap.Map,
                    snapshot),
                cancellationToken);
            await packageWriter.WriteJsonAsync(Path.Combine(manifestRoot, "leak-scan-report.json"), leakScan, cancellationToken);
            if (leakScan.Findings.Count > 0)
            {
                var message = BuildLeakScanFailureMessage(leakScan);
                await LogAsync(task, "Error", ApplicationPublishConstants.StageLeakScan, message, cancellationToken);
                throw new InvalidOperationException(message);
            }

            await LogAsync(
                task,
                "Info",
                ApplicationPublishConstants.StageLeakScan,
                $"泄漏扫描通过：扫描 {leakScan.ScannedFileCount} 个产物文件，检查 {leakScan.ForbiddenMarkerCount} 个禁用标记",
                cancellationToken);

            await UpdateTaskAsync(task, ApplicationPublishConstants.StatusRunning, ApplicationPublishConstants.StageManifest, 82, null, cancellationToken);
            var buildCommands = BuildCommands(task, releaseRoot, runtimeConfig);
            var publishManifest = new ApplicationPublishManifest(
                task.AppCode,
                task.Id,
                task.TenantId,
                task.RuntimeIdentifier,
                task.SelfContained,
                DateTime.UtcNow,
                sourceRoot,
                releaseRoot,
                runtimeConfig,
                snapshot,
                sourceResult.IncludedFiles.OrderBy(item => item.Path).ToList(),
                ApplicationPublishSourceCollector.GetExcludedPatterns(),
                buildCommands,
                leakScan);
            await packageWriter.WriteManifestAsync(Path.Combine(manifestRoot, "publish-manifest.json"), publishManifest, cancellationToken);
            await packageWriter.WriteChecksumManifestAsync(taskRoot, Path.Combine(manifestRoot, "checksum-manifest.json"), cancellationToken);

            await UpdateTaskAsync(task, ApplicationPublishConstants.StatusRunning, ApplicationPublishConstants.StagePackage, 90, null, cancellationToken);
            var artifactPath = Path.Combine(artifactsRoot, $"{task.AppCode}-{task.Id}.zip");
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
            await db.Insertable(artifact).ExecuteCommandAsync(cancellationToken);

            task.ReleasePath = releaseRoot;
            task.ArtifactPath = artifactPath;
            task.Status = ApplicationPublishConstants.StatusSucceeded;
            task.Stage = ApplicationPublishConstants.StageCompleted;
            task.ProgressPercent = 100;
            task.FinishedAt = DateTime.UtcNow;
            task.DurationMs = (long)(task.FinishedAt.Value - startedAt).TotalMilliseconds;
            task.UpdatedTime = task.FinishedAt;
            await db.Updateable(task).ExecuteCommandAsync(cancellationToken);
            await CleanupOldArtifactsAsync(task.TenantId!, task.AppCode, options.Value.KeepSuccessfulCount, cancellationToken);
            await LogAsync(task, "Info", ApplicationPublishConstants.StageCompleted, $"发布完成：{app.AppName}", cancellationToken);
        }
        catch (Exception ex)
        {
            await FailAsync(task, ex, startedAt, cancellationToken);
        }
        finally
        {
            publishLock?.Dispose();
        }
    }

    private static bool IsRuntimePageRoute(string routePath) =>
        routePath.StartsWith("/pages/", StringComparison.OrdinalIgnoreCase) ||
        routePath.StartsWith("/runtime/", StringComparison.OrdinalIgnoreCase);

    private async Task RunBackendPublishAsync(
        SystemApplicationPublishTaskEntity task,
        string sourceRoot,
        string releaseRoot,
        string logRoot,
        CancellationToken cancellationToken)
    {
        var logPath = Path.Combine(logRoot, "backend-publish.log");
        await RunProcessAsync(
            task,
            logPath,
            new ApplicationPublishProcessRequest(
                "dotnet",
                [
                    "publish",
                    Path.Combine(sourceRoot, "backend", "AsterERP.Api", "AsterERP.Api.csproj"),
                    "-c",
                    "Release",
                    "-r",
                    task.RuntimeIdentifier,
                    "--self-contained",
                    task.SelfContained ? "true" : "false",
                    "-o",
                    releaseRoot
                ],
                sourceRoot),
            ApplicationPublishConstants.StageBackend,
            cancellationToken);

        DeleteBuildIntermediatesInsideRoot(sourceRoot, sourceRoot);
    }

    private async Task RunFrontendBuildAsync(
        SystemApplicationPublishTaskEntity task,
        string sourceRoot,
        string releaseRoot,
        string logRoot,
        string taskRoot,
        ApplicationPublishDependencySnapshot snapshot,
        ApplicationPublishRuntimeConfig runtimeConfig,
        CancellationToken cancellationToken)
    {
        var frontendRoot = Path.Combine(sourceRoot, "frontend", "AsterERP.Web");
        var frontendOutput = runtimeConfig.FrontendOutputPath;
        var npm = OperatingSystem.IsWindows() ? "npm.cmd" : "npm";
        await frontendTargetWriter.WriteAsync(sourceRoot, snapshot, cancellationToken);

        await RunProcessAsync(
            task,
            Path.Combine(logRoot, "frontend-restore.log"),
            new ApplicationPublishProcessRequest(
                npm,
                ["ci"],
                frontendRoot),
            ApplicationPublishConstants.StageFrontend,
            cancellationToken);

        await RunProcessAsync(
            task,
            Path.Combine(logRoot, "frontend-reachability.log"),
            new ApplicationPublishProcessRequest(
                npm,
                [
                    "run",
                    "publish:reachability"
                ],
                frontendRoot,
                new Dictionary<string, string?>
                {
                    ["VITE_APP_API_BASE_URL"] = runtimeConfig.FrontendApiBaseUrl,
                    ["VITE_APP_BASE_PATH"] = runtimeConfig.FrontendBasePath,
                    ["VITE_APP_TARGET_APP_CODE"] = task.AppCode,
                    ["PUBLISH_REACHABILITY_METAFILE"] = Path.Combine(taskRoot, "manifest", "frontend-metafile.json"),
                    ["PUBLISH_REACHABILITY_REPORT"] = Path.Combine(taskRoot, "manifest", "frontend-purity-report.json"),
                    ["PUBLISH_REACHABILITY_PRUNE"] = "1"
                }),
            ApplicationPublishConstants.StageFrontend,
            cancellationToken);

        await RunProcessAsync(
            task,
            Path.Combine(logRoot, "frontend-build.log"),
            new ApplicationPublishProcessRequest(
                npm,
                [
                    "run",
                    "build",
                ],
                frontendRoot,
                new Dictionary<string, string?>
                {
                    ["VITE_APP_API_BASE_URL"] = runtimeConfig.FrontendApiBaseUrl,
                    ["VITE_APP_BASE_PATH"] = runtimeConfig.FrontendBasePath,
                    ["VITE_APP_OUT_DIR"] = frontendOutput,
                    ["VITE_APP_TARGET_APP_CODE"] = task.AppCode
                }),
            ApplicationPublishConstants.StageFrontend,
            cancellationToken);

        DeleteDirectoryInsideRoot(Path.Combine(frontendRoot, "node_modules"), taskRoot);
    }

    private async Task RunProcessAsync(
        SystemApplicationPublishTaskEntity task,
        string logPath,
        ApplicationPublishProcessRequest request,
        string stage,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        await using var logStream = new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.Read, 81920, useAsync: true);
        await using var writer = new StreamWriter(logStream);
        var result = await processRunner.RunAsync(
            request,
            async (line, token) =>
            {
                await writer.WriteLineAsync(line.AsMemory(), token);
                await writer.FlushAsync(token);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    await LogAsync(task, "Info", stage, line, token);
                }
            },
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"{request.FileName} 执行失败，退出码 {result.ExitCode}");
        }
    }

    private async Task<ApplicationPublishDependencySnapshot> BuildDependencySnapshotAsync(
        SystemApplicationPublishTaskEntity task,
        ApplicationPublishModuleFileMapLoadResult moduleFileMap,
        CancellationToken cancellationToken)
    {
        var menuQuery = db.Queryable<SystemMenuEntity>()
            .Where(item => item.TenantId == task.TenantId && item.AppCode == task.AppCode && !item.IsDeleted);

        var menuRows = await menuQuery
            .OrderBy(item => item.TenantId)
            .OrderBy(item => item.SortOrder)
            .Select(item => new
            {
                item.TenantId,
                item.AppCode,
                item.MenuCode,
                item.MenuName,
                item.RoutePath,
                item.ComponentName,
                item.PageCode,
                item.PermissionCode,
                item.MenuType
            })
            .ToListAsync(cancellationToken);
        var menus = menuRows.Cast<object>().ToList();

        var pages = await db.Queryable<ApplicationDevelopmentPageEntity>()
            .Where(item => item.TenantId == task.TenantId && item.AppCode == task.AppCode &&
                           item.Status == "Published" && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var pageIds = pages.Select(item => item.Id).ToArray();
        var documents = pageIds.Length == 0
            ? []
            : await db.Queryable<ApplicationDesignerDocumentEntity>()
                .Where(item => item.TenantId == task.TenantId && item.AppCode == task.AppCode &&
                               pageIds.Contains(item.PageId) && item.Status == "Published" &&
                               !item.IsDeleted && item.PublishedArtifactId != null && item.PublishedArtifactId != "")
                .ToListAsync(cancellationToken);
        var artifactIds = documents.Select(item => item.PublishedArtifactId!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var artifacts = artifactIds.Length == 0
            ? []
            : await db.Queryable<ApplicationDesignerRuntimeArtifactEntity>()
                .Where(item => item.TenantId == task.TenantId && item.AppCode == task.AppCode &&
                               artifactIds.Contains(item.Id) && item.Status == "Published" && !item.IsDeleted)
                .ToListAsync(cancellationToken);
        var documentsByPageId = documents
            .GroupBy(item => item.PageId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.UpdatedTime ?? item.CreatedTime).First(), StringComparer.OrdinalIgnoreCase);
        var artifactsById = artifacts.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var pageSchemaRows = new List<object>();
        var pageSchemaModelCodes = new List<(string PageCode, string? ModelCode, string? PermissionCode)>();
        var unresolved = new List<ApplicationPublishUnresolvedDependency>();
        foreach (var page in pages)
        {
            if (!documentsByPageId.TryGetValue(page.Id, out var document) ||
                string.IsNullOrWhiteSpace(document.PublishedArtifactId) ||
                !artifactsById.TryGetValue(document.PublishedArtifactId, out var artifact))
            {
                unresolved.Add(new ApplicationPublishUnresolvedDependency(
                    "runtimeArtifact",
                    page.PageCode,
                    "Published DesignerDocument 缺少有效 RuntimeArtifact 指针"));
                continue;
            }

            try
            {
                var runtimeArtifact = ValidatePublishedArtifact(page, document, artifact);
                var modelCode = ReadModelCode(runtimeArtifact);
                var permissionCode = PermissionCodes.BuildAppRuntimePagePermission(page.PageCode, "view");
                pageSchemaRows.Add(new
                {
                    TenantId = page.TenantId,
                    AppCode = page.AppCode,
                    page.PageCode,
                    page.PageName,
                    ModelCode = modelCode,
                    PermissionCode = permissionCode,
                    VersionNo = artifact.RevisionNumber,
                    Status = artifact.Status,
                    ArtifactId = artifact.Id,
                    ArtifactHash = artifact.ArtifactHash,
                    ManifestHash = artifact.ManifestHash
                });
                pageSchemaModelCodes.Add((page.PageCode, modelCode, permissionCode));
            }
            catch (ValidationException exception)
            {
                unresolved.Add(new ApplicationPublishUnresolvedDependency(
                    "runtimeArtifact",
                    page.PageCode,
                    exception.Message));
            }
        }
        var pageSchemas = pageSchemaRows;

        var dataModelQuery = db.Queryable<SystemDataModelEntity>()
            .Where(item => item.TenantId == task.TenantId && item.AppCode == task.AppCode && item.Status == "Published" && !item.IsDeleted);

        var dataModelRows = await dataModelQuery
            .Select(item => new
            {
                item.TenantId,
                item.AppCode,
                item.ModelCode,
                item.ModelName,
                item.ProviderKey,
                item.PermissionCode,
                item.VersionNo,
                item.Status
            })
            .ToListAsync(cancellationToken);
        var dataModels = dataModelRows.Cast<object>().ToList();

        var routeValues = await menuQuery
            .Where(item => item.Visible)
            .Select(item => new { item.RoutePath, item.PageCode })
            .ToListAsync(cancellationToken);

        var frontendRoutes = routeValues
            .Select(item => string.IsNullOrWhiteSpace(item.RoutePath)
                ? string.IsNullOrWhiteSpace(item.PageCode) ? null : $"/pages/{item.PageCode}"
                : item.RoutePath)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(item => item!)
            .ToList();

        var providers = (await dataModelQuery
            .Select(item => item.ProviderKey)
            .ToListAsync(cancellationToken))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (providers.Count == 0)
        {
            providers.Add("system.menus");
        }

        var permissionCodes = menuRows
            .Select(item => item.PermissionCode)
            .Concat(pageSchemaModelCodes.Select(item => item.PermissionCode))
            .Concat(dataModelRows.Select(item => item.PermissionCode))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(item => item!)
            .ToList();

        var closure = moduleClosureResolver.Resolve(moduleFileMap.Map, task.AppCode, permissionCodes, providers);
        unresolved.AddRange(closure.UnresolvedDependencies);
        var dataModelCodes = dataModelRows
            .Select(item => item.ModelCode)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var missingModel in pageSchemaModelCodes
                     .Select(item => item.ModelCode)
                     .Where(item => !string.IsNullOrWhiteSpace(item) && !dataModelCodes.Contains(item!))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            unresolved.Add(new ApplicationPublishUnresolvedDependency("modelCode", missingModel!, "PageSchema 引用的数据模型不存在或未发布"));
        }

        return new ApplicationPublishDependencySnapshot(
            task.AppCode,
            task.TenantId,
            closure.PublishMode,
            menus,
            pageSchemas,
            dataModels,
            permissionCodes,
            BuildBackendRoutes(),
            frontendRoutes,
            providers,
            closure.ModuleKeys,
            closure.ClosureEdges,
            unresolved,
            moduleFileMap.Sha256);
    }

    private static ApplicationPublishRuntimeConfig BuildRuntimeConfig(
        SystemApplicationPublishTaskEntity task,
        string releaseRoot)
    {
        var frontendBasePath = RequireFrontendBasePath(task.FrontendBasePath);
        var frontendApiBaseUrl = string.IsNullOrWhiteSpace(task.FrontendApiBaseUrl)
            ? "/api"
            : task.FrontendApiBaseUrl;
        var backendUrls = $"http://{task.BackendHost}:{task.BackendPort}";
        var frontendOutputPath = Path.Combine(
            releaseRoot,
            "wwwroot",
            frontendBasePath.Trim('/'));

        return new ApplicationPublishRuntimeConfig(
            task.BackendHost,
            task.BackendPort,
            backendUrls,
            frontendBasePath,
            frontendApiBaseUrl,
            frontendOutputPath);
    }

    private JsonObject ValidatePublishedArtifact(
        ApplicationDevelopmentPageEntity page,
        ApplicationDesignerDocumentEntity document,
        ApplicationDesignerRuntimeArtifactEntity artifact)
    {
        if (!string.Equals(page.TenantId, document.TenantId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(page.AppCode, document.AppCode, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(page.TenantId, artifact.TenantId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(page.AppCode, artifact.AppCode, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(document.PageId, page.Id, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(document.PublishedArtifactId, artifact.Id, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(artifact.DocumentId, document.Id, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(page.Status, "Published", StringComparison.Ordinal) ||
            !string.Equals(document.Status, "Published", StringComparison.Ordinal) ||
            !string.Equals(artifact.Status, "Published", StringComparison.Ordinal))
        {
            throw InvalidArtifact("Published runtime artifact workspace boundary or pointer is invalid.");
        }

        try
        {
            var runtimeArtifact = JsonNode.Parse(artifact.ArtifactJson) as JsonObject
                ?? throw InvalidArtifact("Published runtime artifact must be a JSON object.");
            RuntimeArtifactContractValidator.Validate(runtimeArtifact);
            var runtimeDocument = runtimeArtifact["document"] as JsonObject
                ?? throw InvalidArtifact("Published runtime artifact document is missing.");
            var validatedDocument = schemaValidator.ValidateRuntimeArtifact(
                runtimeDocument.ToJsonString(ApplicationDataCenterJson.Options));
            var artifactHash = RequireString(runtimeArtifact, "artifactHash");
            var actualArtifactHash = ApplicationDesignerCanonicalJson.ComputeRuntimeArtifactHash(validatedDocument);
            if (!string.Equals(artifactHash, actualArtifactHash, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(artifact.ArtifactHash, actualArtifactHash, StringComparison.OrdinalIgnoreCase))
            {
                throw InvalidArtifact("Published runtime artifact hash validation failed.");
            }

            var manifestTypes = runtimeArtifact["manifestTypes"] as JsonArray
                ?? throw InvalidArtifact("Published runtime artifact manifestTypes is missing.");
            var manifest = runtimeArtifact["manifest"] as JsonArray
                ?? throw InvalidArtifact("Published runtime artifact manifest is missing.");
            var manifestJson = ApplicationDesignerCanonicalJson.NormalizeRuntimeObject(new JsonObject
            {
                ["types"] = manifestTypes.DeepClone(),
                ["declarations"] = manifest.DeepClone()
            }.ToJsonString());
            var manifestHash = ApplicationDesignerCanonicalJson.ComputeHash(manifestJson);
            if (!string.Equals(artifact.ManifestHash, manifestHash, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(ApplicationDesignerCanonicalJson.NormalizeRuntimeObject(artifact.ManifestJson), manifestJson, StringComparison.Ordinal))
            {
                throw InvalidArtifact("Published runtime artifact manifest validation failed.");
            }

            var revision = runtimeArtifact["revision"]?.GetValue<int>() ?? 0;
            var compilerVersion = RequireString(runtimeArtifact, "compilerVersion");
            var signature = RequireString(runtimeArtifact, "signature");
            var documentId = RequireString(runtimeDocument, "documentId");
            var expectedSignature = ApplicationDesignerCanonicalJson.ComputeSignature(
                documentId,
                artifactHash,
                manifestHash,
                compilerVersion,
                revision.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
            if (!string.Equals(signature, expectedSignature, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(artifact.SignatureHash, expectedSignature, StringComparison.OrdinalIgnoreCase))
            {
                throw InvalidArtifact("Published runtime artifact signature validation failed.");
            }

            var runtimeContext = runtimeDocument["runtimeContext"] as JsonObject
                ?? throw InvalidArtifact("Published runtime artifact runtimeContext is missing.");
            if (!string.Equals(RequireString(runtimeContext, "pageCode"), page.PageCode, StringComparison.OrdinalIgnoreCase))
            {
                throw InvalidArtifact("Published runtime artifact pageCode does not match the published page.");
            }

            return runtimeArtifact;
        }
        catch (ValidationException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw InvalidArtifact($"Published runtime artifact JSON validation failed: {exception.Message}");
        }
        catch (InvalidOperationException exception)
        {
            throw InvalidArtifact($"Published runtime artifact field validation failed: {exception.Message}");
        }
    }

    private static string? ReadModelCode(JsonObject artifact)
    {
        if (artifact["document"] is not JsonObject document || document["runtimeContext"] is not JsonObject runtimeContext)
        {
            return null;
        }

        return runtimeContext["modelCode"]?.GetValue<string>();
    }

    private static string RequireString(JsonObject node, string propertyName)
    {
        var value = node[propertyName]?.GetValue<string>();
        return !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw InvalidArtifact($"Published runtime artifact requires {propertyName}.");
    }

    private static ValidationException InvalidArtifact(string message) =>
        new(message, ErrorCodes.DesignerSchemaInvalid);

    private async Task WriteRuntimeFilesAsync(
        SystemApplicationPublishTaskEntity task,
        ApplicationPublishRuntimeConfig runtimeConfig,
        string releaseRoot,
        string manifestRoot,
        CancellationToken cancellationToken)
    {
        var runtimeFile = new ApplicationPublishRuntimeFile(
            task.AppCode,
            task.TenantId!,
            runtimeConfig.BackendHost,
            runtimeConfig.BackendPort,
            runtimeConfig.BackendUrls,
            runtimeConfig.FrontendBasePath,
            runtimeConfig.FrontendApiBaseUrl,
            runtimeConfig.FrontendOutputPath);
        await packageWriter.WriteJsonAsync(Path.Combine(manifestRoot, "runtime-config.json"), runtimeFile, cancellationToken);
        await packageWriter.WriteJsonAsync(Path.Combine(releaseRoot, "publish-runtime.json"), runtimeFile, cancellationToken);
        await WriteAppSettingsRuntimeAsync(releaseRoot, runtimeFile, cancellationToken);
        await WriteStartScriptsAsync(task, releaseRoot, runtimeConfig, cancellationToken);
        await LogAsync(task, "Info", ApplicationPublishConstants.StageBackend, $"运行配置已写入：{runtimeConfig.BackendUrls} {runtimeConfig.FrontendBasePath}", cancellationToken);
    }

    private static async Task WriteAppSettingsRuntimeAsync(
        string releaseRoot,
        ApplicationPublishRuntimeFile runtimeFile,
        CancellationToken cancellationToken)
    {
        var appsettingsPath = Path.Combine(releaseRoot, "appsettings.json");
        JsonObject root;
        if (File.Exists(appsettingsPath))
        {
            await using var readStream = new FileStream(appsettingsPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            root = await JsonNode.ParseAsync(readStream, cancellationToken: cancellationToken) as JsonObject ?? new JsonObject();
        }
        else
        {
            root = new JsonObject();
        }

        root["Urls"] = runtimeFile.BackendUrls;
        root["PublishedRuntime"] = new JsonObject
        {
            ["AppCode"] = runtimeFile.AppCode,
            ["TenantId"] = runtimeFile.TenantId,
            ["BackendHost"] = runtimeFile.BackendHost,
            ["BackendPort"] = runtimeFile.BackendPort,
            ["BackendUrls"] = runtimeFile.BackendUrls,
            ["FrontendBasePath"] = runtimeFile.FrontendBasePath,
            ["FrontendApiBaseUrl"] = runtimeFile.FrontendApiBaseUrl
        };

        await using var writeStream = new FileStream(appsettingsPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await JsonSerializer.SerializeAsync(writeStream, root, JsonOptions, cancellationToken);
    }

    private static async Task WriteStartScriptsAsync(
        SystemApplicationPublishTaskEntity task,
        string releaseRoot,
        ApplicationPublishRuntimeConfig runtimeConfig,
        CancellationToken cancellationToken)
    {
        var commandPath = Path.Combine(releaseRoot, $"start-{task.AppCode}.cmd");
        var commandContent = string.Join(Environment.NewLine, [
            "@echo off",
            "setlocal",
            "set \"ASPNETCORE_ENVIRONMENT=Production\"",
            $"set \"ASPNETCORE_URLS={runtimeConfig.BackendUrls}\"",
            $"set \"ASTERERP_APP_CODE={task.AppCode}\"",
            $"set \"ASTERERP_TENANT_ID={task.TenantId}\"",
            $"set \"ASTERERP_FRONTEND_BASE_PATH={runtimeConfig.FrontendBasePath}\"",
            $"set \"ASTERERP_FRONTEND_API_BASE_URL={runtimeConfig.FrontendApiBaseUrl}\"",
            "cd /d \"%~dp0\"",
            "AsterERP.Api.exe",
            "endlocal",
            string.Empty
        ]);
        await File.WriteAllTextAsync(commandPath, commandContent, cancellationToken);

        var powershellPath = Path.Combine(releaseRoot, $"start-{task.AppCode}.ps1");
        var powershellContent = string.Join(Environment.NewLine, [
            "$ErrorActionPreference = 'Stop'",
            "$env:ASPNETCORE_ENVIRONMENT = 'Production'",
            $"$env:ASPNETCORE_URLS = '{EscapePowerShellString(runtimeConfig.BackendUrls)}'",
            $"$env:ASTERERP_APP_CODE = '{EscapePowerShellString(task.AppCode)}'",
            $"$env:ASTERERP_TENANT_ID = '{EscapePowerShellString(task.TenantId ?? string.Empty)}'",
            $"$env:ASTERERP_FRONTEND_BASE_PATH = '{EscapePowerShellString(runtimeConfig.FrontendBasePath)}'",
            $"$env:ASTERERP_FRONTEND_API_BASE_URL = '{EscapePowerShellString(runtimeConfig.FrontendApiBaseUrl)}'",
            "Set-Location $PSScriptRoot",
            "& (Join-Path $PSScriptRoot 'AsterERP.Api.exe')",
            string.Empty
        ]);
        await File.WriteAllTextAsync(powershellPath, powershellContent, cancellationToken);
    }

    private static string EscapePowerShellString(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);

    private static IReadOnlyList<string> BuildBackendRoutes() =>
    [
        "/api/auth/*",
        "/api/platform/applications/*",
        "/api/runtime/pages/*",
        "/api/runtime/models/*",
        "/api/runtime/grid-views/*",
        "/hubs/system-notification"
    ];

    private static IReadOnlyList<string> BuildCommands(
        SystemApplicationPublishTaskEntity task,
        string releaseRoot,
        ApplicationPublishRuntimeConfig runtimeConfig)
    {
        var commands = new List<string>();
        if (task.IncludeBackend)
        {
            commands.Add($"dotnet publish backend/AsterERP.Api/AsterERP.Api.csproj -c Release -r {task.RuntimeIdentifier} --self-contained {task.SelfContained.ToString().ToLowerInvariant()} -o {releaseRoot}");
        }

        if (task.IncludeFrontend)
        {
            commands.Add($"VITE_APP_API_BASE_URL={runtimeConfig.FrontendApiBaseUrl} VITE_APP_BASE_PATH={runtimeConfig.FrontendBasePath} VITE_APP_OUT_DIR={runtimeConfig.FrontendOutputPath} VITE_APP_TARGET_APP_CODE={task.AppCode} npm run build");
        }

        return commands;
    }

    private static string BuildLeakScanFailureMessage(ApplicationPublishLeakScanReport leakScan)
    {
        var samples = leakScan.Findings
            .Take(5)
            .Select(item => $"{item.FilePath} 命中 {item.ModuleKey}:{item.MarkerKind}={item.Marker}")
            .ToList();
        return $"发布产物泄漏扫描失败，共 {leakScan.Findings.Count} 个命中；{string.Join("；", samples)}";
    }

    private async Task<SystemApplicationPublishTaskEntity> GetTaskAsync(
        string taskId,
        CancellationToken cancellationToken)
    {
        return (await db.Queryable<SystemApplicationPublishTaskEntity>()
            .Where(item => item.Id == taskId && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new InvalidOperationException("Publish task was not found.");
    }

    private async Task<SystemApplicationEntity> GetAppAsync(
        string appId,
        CancellationToken cancellationToken)
    {
        return (await db.Queryable<SystemApplicationEntity>()
            .Where(item => item.Id == appId && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new InvalidOperationException("Application was not found.");
    }

    private async Task EnsureTaskBoundaryAsync(
        SystemApplicationPublishTaskEntity task,
        SystemApplicationEntity app,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(task.TenantId))
        {
            throw new InvalidOperationException("发布任务缺少 TenantId，已拒绝执行以防止跨租户聚合");
        }

        if (!string.Equals(task.AppCode, app.AppCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("发布任务的 AppCode 与目标应用不一致，已拒绝执行");
        }

        var tenantApp = (await db.Queryable<SystemTenantAppEntity>()
                .Where(item =>
                    item.TenantId == task.TenantId &&
                    item.AppCode == task.AppCode &&
                    !item.IsDeleted &&
                    item.Status == "Enabled")
                .Take(1)
                .ToListAsync(cancellationToken))
            .FirstOrDefault();
        if (tenantApp is null || (tenantApp.ExpiredAt.HasValue && tenantApp.ExpiredAt.Value <= DateTime.UtcNow))
        {
            throw new InvalidOperationException("发布任务目标 TenantId + AppCode 工作区已不存在或已停用");
        }

        _ = RequireFrontendBasePath(task.FrontendBasePath);
    }

    private static string RequireFrontendBasePath(string? value)
    {
        var normalized = value?.Trim().Replace('\\', '/') ?? string.Empty;
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 1 ||
            segments[0].Length > 64 ||
            segments[0].Equals("api", StringComparison.OrdinalIgnoreCase) ||
            segments[0].Equals("hubs", StringComparison.OrdinalIgnoreCase) ||
            segments[0].Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new InvalidOperationException("发布任务 FrontendBasePath 必须是安全的单段路径");
        }

        return "/" + segments[0];
    }

    private async Task UpdateTaskAsync(
        SystemApplicationPublishTaskEntity task,
        string status,
        string stage,
        int progress,
        string? error,
        CancellationToken cancellationToken)
    {
        task.Status = status;
        task.Stage = stage;
        task.ProgressPercent = Math.Clamp(progress, 0, 100);
        task.ErrorMessage = error;
        task.UpdatedTime = DateTime.UtcNow;
        if (status == ApplicationPublishConstants.StatusRunning && task.StartedAt is null)
        {
            task.StartedAt = DateTime.UtcNow;
        }

        await db.Updateable(task).ExecuteCommandAsync(cancellationToken);
    }

    private async Task BlockAsync(
        SystemApplicationPublishTaskEntity task,
        string reason,
        DateTime startedAt,
        CancellationToken cancellationToken)
    {
        task.Status = ApplicationPublishConstants.StatusBlocked;
        task.Stage = ApplicationPublishConstants.StageScanning;
        task.ProgressPercent = 100;
        task.ErrorMessage = reason;
        task.FinishedAt = DateTime.UtcNow;
        task.DurationMs = (long)(task.FinishedAt.Value - startedAt).TotalMilliseconds;
        task.UpdatedTime = task.FinishedAt;
        await db.Updateable(task).ExecuteCommandAsync(cancellationToken);
        await LogAsync(task, "Warn", ApplicationPublishConstants.StageScanning, reason, cancellationToken);
    }

    private async Task FailAsync(
        SystemApplicationPublishTaskEntity task,
        Exception exception,
        DateTime startedAt,
        CancellationToken cancellationToken)
    {
        task.Status = ApplicationPublishConstants.StatusFailed;
        task.ProgressPercent = 100;
        task.ErrorMessage = exception.Message;
        task.FinishedAt = DateTime.UtcNow;
        task.DurationMs = (long)(task.FinishedAt.Value - startedAt).TotalMilliseconds;
        task.UpdatedTime = task.FinishedAt;
        await db.Updateable(task).ExecuteCommandAsync(cancellationToken);
        await LogAsync(task, "Error", task.Stage, exception.Message, cancellationToken);
    }

    private async Task LogAsync(
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

    private async Task CleanupOldArtifactsAsync(
        string tenantId,
        string appCode,
        int keepSuccessfulCount,
        CancellationToken cancellationToken)
    {
        if (keepSuccessfulCount <= 0)
        {
            return;
        }

        var successfulTaskIds = await db.Queryable<SystemApplicationPublishTaskEntity>()
            .Where(item => item.TenantId == tenantId && item.AppCode == appCode && item.Status == ApplicationPublishConstants.StatusSucceeded && !item.IsDeleted)
            .OrderBy(item => item.FinishedAt, OrderByType.Desc)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        var expiredTaskIds = successfulTaskIds.Skip(keepSuccessfulCount).ToList();
        if (expiredTaskIds.Count == 0)
        {
            return;
        }

        var artifacts = await db.Queryable<SystemApplicationPublishArtifactEntity>()
            .Where(item => expiredTaskIds.Contains(item.TaskId) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var artifact in artifacts)
        {
            var artifactPath = ResolveStoredArtifactPath(artifact.StoredPath);
            if (File.Exists(artifactPath))
            {
                File.Delete(artifactPath);
            }

            artifact.IsDeleted = true;
            artifact.DeletedTime = DateTime.UtcNow;
            artifact.UpdatedTime = artifact.DeletedTime;
        }

        if (artifacts.Count > 0)
        {
            await db.Updateable(artifacts).ExecuteCommandAsync(cancellationToken);
        }
    }

    private static void PrepareTaskDirectory(string taskRoot)
    {
        if (Directory.Exists(taskRoot))
        {
            Directory.Delete(taskRoot, recursive: true);
        }

        Directory.CreateDirectory(taskRoot);
    }

    private string ResolveStoredArtifactPath(string storedPath)
    {
        var outputRoot = pathGuard.ResolveOutputRoot();
        var fullPath = Path.IsPathRooted(storedPath)
            ? storedPath
            : Path.Combine(outputRoot, storedPath);
        return pathGuard.EnsureInsideRoot(fullPath, outputRoot);
    }

    private void DeleteDirectoryInsideRoot(string directoryPath, string root)
    {
        var safePath = pathGuard.EnsureInsideRoot(directoryPath, root);
        if (Directory.Exists(safePath))
        {
            Directory.Delete(safePath, recursive: true);
        }
    }

    private void DeleteBuildIntermediatesInsideRoot(string directoryRoot, string safetyRoot)
    {
        if (!Directory.Exists(directoryRoot))
        {
            return;
        }

        var targets = Directory.EnumerateDirectories(directoryRoot, "*", SearchOption.AllDirectories)
            .Where(path =>
            {
                var name = Path.GetFileName(path);
                return name.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                       name.Equals("obj", StringComparison.OrdinalIgnoreCase);
            })
            .OrderByDescending(path => path.Length)
            .ToList();

        foreach (var target in targets)
        {
            DeleteDirectoryInsideRoot(target, safetyRoot);
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
