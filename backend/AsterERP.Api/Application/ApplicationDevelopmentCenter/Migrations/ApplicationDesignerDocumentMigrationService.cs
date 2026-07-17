using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AsterERP.Api.Application.ApplicationDevelopmentCenter;
using AsterERP.Api.Modules.ApplicationDevelopmentCenter;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Application.ApplicationConsole;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.Platform;
using AsterERP.Shared.Exceptions;
using AsterERP.Shared;
using AsterERP.Contracts.ApplicationConsole;
using AsterERP.Contracts.ApplicationDesigner;
using AsterERP.Contracts.ApplicationDevelopmentCenter;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationDevelopmentCenter.Migrations;

public sealed record ApplicationDesignerMigrationTableInventory(
    string TableName,
    string JsonColumn,
    bool Exists,
    bool Inspectable,
    int RowCount,
    int LegacyLayoutRows,
    int CanonicalProtocolRows,
    int InvalidJsonRows,
    int InvalidProtocolRows);

public sealed record ApplicationDesignerMigrationInventory(
    IReadOnlyList<ApplicationDesignerMigrationTableInventory> Tables,
    int TotalRows,
    int LegacyLayoutRows,
    int CanonicalProtocolRows,
    int InvalidJsonRows,
    int InvalidProtocolRows,
    bool CanRetireLegacyLayoutFields,
    IReadOnlyList<string> RetirementBlockers);

public sealed class ApplicationDesignerDocumentMigrationService(
    ISqlSugarClient db,
    ApplicationDevelopmentSchemaValidator? validator = null,
    ApplicationDesignerMigrationRunService? migrationRunCoordinator = null,
    IWorkspaceDatabaseAccessor? workspaceDatabaseAccessor = null,
    ICurrentUser? currentUser = null,
    ApplicationDatabaseBindingResolver? bindingResolver = null,
    IApplicationDatabaseConnectionFactory? connectionFactory = null,
    ApplicationSystemAdministrationSchemaInitializer? applicationSystemAdministrationSchemaInitializer = null,
    ApplicationDevelopmentSchemaCompiler? schemaCompiler = null,
    ApplicationDesignerArtifactPublisher? artifactPublisher = null,
    ILogger<ApplicationDesignerDocumentMigrationService>? logger = null)
{
    public const string MigrationRevision = "application-designer-latest";
    private readonly ApplicationDevelopmentSchemaValidator schemaValidator = validator ?? new ApplicationDevelopmentSchemaValidator();
    private readonly ApplicationDesignerMigrationRunService migrationRunService = migrationRunCoordinator ?? new ApplicationDesignerMigrationRunService();

    private static readonly string[] LegacyLayoutProperties =
    [
        "display", "layoutMode", "position", "x", "y", "width", "height", "minWidth", "maxWidth", "minHeight", "maxHeight", "aspectRatio", "zIndex",
        "constraints", "flex", "flexDirection", "flexWrap", "flexGrow", "flexShrink", "flexBasis", "alignItems", "justifyContent", "alignSelf", "order", "gap",
        "columnGap", "rowGap", "gridTemplateColumns", "gridTemplateRows", "gridAutoFlow", "gridRow", "gridRowStart", "gridRowEnd", "gridColumn", "gridColumnStart",
        "gridColumnEnd", "gridColumnSpan", "gridRowSpan", "gridArea", "justifyItems", "justifySelf"
    ];

    private static readonly (string TableName, string JsonColumn)[] LayoutInventoryTables =
    [
        ("app_designer_documents", "DocumentJson"),
        ("app_designer_revisions", "DocumentJson"),
        ("app_designer_runtime_artifacts", "ArtifactJson"),
        ("app_dev_documents", "DocumentJson"),
        ("app_dev_document_revisions", "DocumentJson"),
        ("app_dev_runtime_artifacts", "ArtifactJson")
    ];

    public async Task<int> MigrateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var scope = ResolveScope();
        if (scope is null && bindingResolver is not null && connectionFactory is not null)
        {
            return await MigrateRegisteredApplicationDatabasesAsync(bindingResolver, connectionFactory, cancellationToken);
        }

        return await MigrateDatabaseAsync(ResolveDatabase(scope), scope, cancellationToken);
    }

    /// <summary>
    /// Runs the one-time Designer document migration against the already resolved
    /// application workspace database. The caller is responsible for resolving
    /// that database through the application binding/connection-factory path.
    /// </summary>
    public Task<int> MigrateWorkspaceOnceAsync(
        ISqlSugarClient workspaceDb,
        string tenantId,
        string appCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspaceDb);
        var scope = CreateMigrationScope(tenantId, appCode);
        return MigrateDatabaseAsync(workspaceDb, scope, cancellationToken);
    }

    private async Task<int> MigrateRegisteredApplicationDatabasesAsync(
        ApplicationDatabaseBindingResolver bindingResolver,
        IApplicationDatabaseConnectionFactory connectionFactory,
        CancellationToken cancellationToken)
    {
        var applications = await db.Queryable<SystemTenantAppEntity>()
            .Where(item => !item.IsDeleted && item.Status == "Enabled" && item.AppCode != "SYSTEM")
            .ToListAsync(cancellationToken);
        var migrated = 0;
        foreach (var application in applications)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resolution = bindingResolver.ResolveStatus(
                application.ConfigJson,
                application.TenantId,
                application.AppCode);
            if (resolution.Status != ApplicationDatabaseBindingStatus.Ready || resolution.Options is null)
            {
                logger?.LogWarning(
                    "Application Designer migration skipped {TenantId}/{AppCode}: {Status} - {Message}",
                    application.TenantId,
                    application.AppCode,
                    resolution.Status,
                    resolution.Message);
                continue;
            }

            using var applicationDb = connectionFactory.Create(resolution.Options);
            try
            {
                applicationDb.Ado.GetInt("SELECT 1");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger?.LogWarning(
                    exception,
                    "Application Designer migration skipped {TenantId}/{AppCode}: {Status}",
                    application.TenantId,
                    application.AppCode,
                    ApplicationDatabaseBindingStatus.Unavailable);
                continue;
            }

            applicationSystemAdministrationSchemaInitializer?.Initialize(applicationDb);
            migrated += await MigrateDatabaseAsync(
                applicationDb,
                new MigrationScope(application.TenantId, application.AppCode.Trim().ToUpperInvariant()),
                cancellationToken);
        }

        return migrated;
    }

    private async Task<int> MigrateDatabaseAsync(
        ISqlSugarClient migrationDb,
        MigrationScope? scope,
        CancellationToken cancellationToken)
    {
        await RecoverInterruptedRunsAsync(migrationDb, scope, cancellationToken);
        var pagesQuery = migrationDb.Queryable<ApplicationDevelopmentPageEntity>()
            .Where(item => !item.IsDeleted)
            ;
        var documentsQuery = migrationDb.Queryable<ApplicationDesignerDocumentEntity>()
            .Where(item => !item.IsDeleted)
            ;
        if (scope is not null)
        {
            pagesQuery = pagesQuery.Where(item => item.TenantId == scope.TenantId && item.AppCode == scope.AppCode);
            documentsQuery = documentsQuery.Where(item => item.TenantId == scope.TenantId && item.AppCode == scope.AppCode);
        }

        var pages = await pagesQuery.ToListAsync(cancellationToken);
        var documents = await documentsQuery.ToListAsync(cancellationToken);
        EnsureNoDuplicateDocuments(documents);
        var legacySources = await ReadLegacyPageSourcesAsync(migrationDb, cancellationToken);
        var pagesById = pages.ToDictionary(page => page.Id, StringComparer.OrdinalIgnoreCase);
        var migrated = await NormalizeExistingDocumentsAsync(migrationDb, documents, pagesById, cancellationToken);

        var migrationGroups = pages
            .Where(page => !documents.Any(item => item.PageId == page.Id && item.TenantId == page.TenantId && item.AppCode == page.AppCode))
            .GroupBy(page => (page.TenantId, page.AppCode));

        foreach (var group in migrationGroups)
        {
            var backupPayload = JsonSerializer.Serialize(new
            {
                migrationRevision = MigrationRevision,
                pages = group.Select(page => new { page.Id, Source = legacySources.GetValueOrDefault(page.Id) }).ToArray()
            });
            var run = await migrationRunService.AcquireAsync(
                migrationDb,
                group.Key.TenantId,
                group.Key.AppCode,
                MigrationRevision,
                backupPayload,
                null,
                null,
                null,
                null,
                cancellationToken);
            var ownsTransaction = false;
            try
            {
                ownsTransaction = migrationDb.Ado.Transaction is null;
                if (ownsTransaction)
                {
                    await migrationDb.Ado.BeginTranAsync();
                }

                var groupDocuments = new List<ApplicationDesignerDocumentEntity>();
                foreach (var page in group)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!legacySources.TryGetValue(page.Id, out var legacySource))
                    {
                        logger?.LogWarning("Designer Document migration skipped page {PageId}: no legacy source exists.", page.Id);
                        continue;
                    }
                    var migrationData = BuildMigrationData(page, legacySource);
                    schemaValidator.ValidateDraft(migrationData.Document.DocumentJson);
                    await migrationDb.Insertable(migrationData.Document).ExecuteCommandAsync(cancellationToken);
                    await migrationDb.Insertable(migrationData.Revision).ExecuteCommandAsync(cancellationToken);
                    await migrationDb.Insertable(migrationData.Migration).ExecuteCommandAsync(cancellationToken);
                    groupDocuments.Add(migrationData.Document);
                }

                run.RollbackPointer = BuildRollbackPointer(groupDocuments.Select(document =>
                    new
                    {
                        document.PageId,
                        document.Id,
                        RevisionId = document.CurrentRevisionId,
                        document.SourceHash,
                        document.TargetHash
                    }));
                await migrationDb.Updateable(run)
                    .UpdateColumns(item => new { item.RollbackPointer })
                    .ExecuteCommandAsync(cancellationToken);
                var healthCheckId = await ValidateMigrationHealthAsync(migrationDb, groupDocuments, cancellationToken);
                await migrationRunService.CompleteAsync(migrationDb, run, healthCheckId, cancellationToken: cancellationToken);
                if (ownsTransaction)
                {
                    await migrationDb.Ado.CommitTranAsync();
                }

                documents.AddRange(groupDocuments);
                migrated += groupDocuments.Count;
            }
            catch (Exception exception)
            {
                if (ownsTransaction && migrationDb.Ado.Transaction is not null)
                {
                    await migrationDb.Ado.RollbackTranAsync();
                }

                await migrationRunService.FailAsync(migrationDb, run, exception, CancellationToken.None);
                throw;
            }
        }

        return migrated;
    }

    private async Task<int> NormalizeExistingDocumentsAsync(
        ISqlSugarClient migrationDb,
        IReadOnlyCollection<ApplicationDesignerDocumentEntity> documents,
        IReadOnlyDictionary<string, ApplicationDevelopmentPageEntity> pagesById,
        CancellationToken cancellationToken)
    {
        var candidates = documents
            .Where(document => pagesById.ContainsKey(document.PageId))
            .Select(document => (Document: document, Page: pagesById[document.PageId]))
            .Where(item => !string.Equals(item.Document.AppCode, "SYSTEM", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var publishedArtifactIds = candidates
            .Select(item => item.Document.PublishedArtifactId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var publishedArtifacts = publishedArtifactIds.Length == 0
            ? new Dictionary<string, ApplicationDesignerRuntimeArtifactEntity>(StringComparer.OrdinalIgnoreCase)
            : (await migrationDb.Queryable<ApplicationDesignerRuntimeArtifactEntity>()
                .Where(item => publishedArtifactIds.Contains(item.Id) && !item.IsDeleted)
                .ToListAsync(cancellationToken))
                .ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var changes = new List<ExistingDocumentNormalization>();

        foreach (var (document, page) in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalizedJson = NormalizeExistingDocument(document.DocumentJson, page);
            var artifactIsStale = document.PublishedArtifactId is not null &&
                publishedArtifacts.TryGetValue(document.PublishedArtifactId, out var publishedArtifact) &&
                IsPublishedArtifactStale(document, publishedArtifact);
            if (string.Equals(normalizedJson, document.DocumentJson, StringComparison.Ordinal) && !artifactIsStale)
            {
                continue;
            }

            var validated = schemaValidator.ValidateDraft(normalizedJson);
            var revision = await RequireCurrentRevisionForMigrationAsync(migrationDb, document, cancellationToken);
            var documentNode = validated.DeepClone();
            documentNode["revision"] = revision.RevisionNumber + 1;
            normalizedJson = ApplicationDesignerCanonicalJson.NormalizeDocument(documentNode.ToJsonString());
            schemaValidator.ValidateDraft(normalizedJson);
            changes.Add(new ExistingDocumentNormalization(document, page, revision, normalizedJson));
        }

        if (changes.Count == 0)
        {
            return 0;
        }

        var ownsTransaction = migrationDb.Ado.Transaction is null;
        if (ownsTransaction)
        {
            await migrationDb.Ado.BeginTranAsync();
        }

        try
        {
            foreach (var change in changes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var oldDocumentJson = change.Document.DocumentJson;
                var oldDocumentHash = change.Document.DocumentHash;
                var targetHash = ApplicationDesignerCanonicalJson.ComputeDocumentHash(change.DocumentJson);
                var nextRevision = new ApplicationDesignerRevisionEntity
                {
                    TenantId = change.Document.TenantId,
                    AppCode = change.Document.AppCode,
                    DocumentId = change.Document.Id,
                    RevisionNumber = change.Revision.RevisionNumber + 1,
                    DocumentJson = change.DocumentJson,
                    DocumentHash = targetHash,
                    SourceHash = change.Document.SourceHash,
                    TargetHash = targetHash,
                    MigrationRevision = MigrationRevision,
                    CompilerRevision = change.Revision.CompilerRevision,
                    ManifestHash = change.Revision.ManifestHash,
                    ManifestJson = change.Revision.ManifestJson,
                    SourceArtifactHash = change.Revision.SourceArtifactHash,
                    ChangeSetJson = ApplicationDesignerCanonicalJson.NormalizeObject(JsonSerializer.Serialize(new
                    {
                        type = "resource-id-migration",
                        fromHash = oldDocumentHash,
                        sourceRevision = change.Revision.RevisionNumber
                    })),
                    DiagnosticsJson = "[]",
                    CreatedBy = change.Document.UpdatedBy ?? change.Document.CreatedBy,
                    CreatedTime = DateTime.UtcNow,
                    IsDeleted = false
                };
                await migrationDb.Insertable(nextRevision).ExecuteCommandAsync(cancellationToken);

                var migration = new ApplicationDesignerMigrationEntity
                {
                    TenantId = change.Document.TenantId,
                    AppCode = change.Document.AppCode,
                    DocumentId = change.Document.Id,
                    PageId = change.Document.PageId,
                    SourceHash = string.IsNullOrWhiteSpace(oldDocumentHash) ? change.Document.SourceHash : oldDocumentHash,
                    TargetHash = targetHash,
                    MigrationRevision = MigrationRevision,
                    DiagnosticsJson = "[]",
                    BackupDocumentJson = oldDocumentJson,
                    BackupLocation = $"designer-migration://{change.Document.TenantId}/{change.Document.AppCode}/{change.Document.PageId}/{oldDocumentHash}",
                    RollbackRevisionId = change.Revision.Id,
                    StartedTime = DateTime.UtcNow,
                    CompletedTime = DateTime.UtcNow,
                    CreatedBy = nextRevision.CreatedBy,
                    CreatedTime = DateTime.UtcNow
                };
                await migrationDb.Insertable(migration).ExecuteCommandAsync(cancellationToken);

                change.Document.DocumentJson = change.DocumentJson;
                change.Document.DocumentHash = targetHash;
                change.Document.TargetHash = targetHash;
                change.Document.MigrationRevision = MigrationRevision;
                change.Document.CurrentRevisionId = nextRevision.Id;
                change.Document.UpdatedBy = nextRevision.CreatedBy;
                change.Document.UpdatedTime = nextRevision.CreatedTime;
                await migrationDb.Updateable(change.Document).ExecuteCommandAsync(cancellationToken);

                if (schemaCompiler is not null && artifactPublisher is not null)
                {
                    var compiledArtifactJson = schemaCompiler.CompileSchema(
                        change.Page.PageCode,
                        change.Page.PageName,
                        change.Page.PageType,
                        ReadPageParameters(change.Page.PageParametersJson),
                        change.DocumentJson,
                        change.Page.PermissionConfigJson,
                        modelCode: ReadModelCode(change.DocumentJson));
                    var artifact = await artifactPublisher.PublishAsync(
                        migrationDb,
                        new ApplicationDataCenterWorkspace(change.Document.TenantId, change.Document.AppCode, "application-migration"),
                        change.Document,
                        compiledArtifactJson,
                        change.Document.PublishedArtifactId,
                        cancellationToken);
                    change.Page.PublishedArtifactId = artifact.Id;
                    change.Page.Status = "Published";
                    change.Page.UpdatedBy = nextRevision.CreatedBy;
                    change.Page.UpdatedTime = nextRevision.CreatedTime;
                    await migrationDb.Updateable(change.Page)
                        .UpdateColumns(item => new
                        {
                            item.PublishedArtifactId,
                            item.Status,
                            item.UpdatedBy,
                            item.UpdatedTime
                        })
                        .ExecuteCommandAsync(cancellationToken);
                }
            }

            if (ownsTransaction)
            {
                await migrationDb.Ado.CommitTranAsync();
            }

            return changes.Count;
        }
        catch
        {
            if (ownsTransaction && migrationDb.Ado.Transaction is not null)
            {
                await migrationDb.Ado.RollbackTranAsync();
            }

            throw;
        }
    }

    private static async Task<ApplicationDesignerRevisionEntity> RequireCurrentRevisionForMigrationAsync(
        ISqlSugarClient migrationDb,
        ApplicationDesignerDocumentEntity document,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(document.CurrentRevisionId))
        {
            throw new ValidationException($"Designer Document current revision is missing for page '{document.PageId}'.", ErrorCodes.DesignerSchemaInvalid);
        }

        var revision = await migrationDb.Queryable<ApplicationDesignerRevisionEntity>()
            .Where(item => item.Id == document.CurrentRevisionId && !item.IsDeleted)
            .FirstAsync(cancellationToken);
        return revision ?? throw new ValidationException($"Designer Document current revision is missing for page '{document.PageId}'.", ErrorCodes.DesignerSchemaInvalid);
    }

    private static string NormalizeExistingDocument(string sourceJson, ApplicationDevelopmentPageEntity page)
    {
        var document = JsonNode.Parse(sourceJson) as JsonObject
            ?? throw new ValidationException("Designer Document must be a JSON object", ErrorCodes.DesignerSchemaInvalid);
        NormalizeElements(document, page);
        NormalizeLegacyValues(document, preserveCanonicalDefinitions: true);
        NormalizeLatestActionTypes(document);
        return ApplicationDesignerCanonicalJson.NormalizeDocument(document.ToJsonString(ApplicationDataCenterJson.Options));
    }

    private sealed record ExistingDocumentNormalization(
        ApplicationDesignerDocumentEntity Document,
        ApplicationDevelopmentPageEntity Page,
        ApplicationDesignerRevisionEntity Revision,
        string DocumentJson);

    private static IReadOnlyList<ApplicationDevelopmentPageParameterDto> ReadPageParameters(string json) =>
        JsonSerializer.Deserialize<List<ApplicationDevelopmentPageParameterDto>>(json, ApplicationDataCenterJson.Options)
        ?? [];

    private static string? ReadModelCode(string documentJson)
    {
        var document = JsonNode.Parse(documentJson) as JsonObject;
        return document?["runtimeContext"]?["modelCode"]?.GetValue<string>();
    }

    private static async Task<Dictionary<string, LegacyPageSource>> ReadLegacyPageSourcesAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        var schema = new Infrastructure.Database.SqliteSchemaExecutor(db);
        if (!await schema.HasColumnAsync("app_dev_pages", "LayoutDraftJson", cancellationToken) || !await schema.HasColumnAsync("app_dev_pages", "SchemaDraftJson", cancellationToken))
        {
            return new Dictionary<string, LegacyPageSource>(StringComparer.OrdinalIgnoreCase);
        }
        var table = await schema.ExecuteDataTableAsync("SELECT Id, LayoutDraftJson, SchemaDraftJson FROM app_dev_pages WHERE IsDeleted = 0", cancellationToken);
        return table.Rows.Cast<global::System.Data.DataRow>()
            .Select(row => new LegacyPageSource(Convert.ToString(row["Id"]) ?? "", Convert.ToString(row["LayoutDraftJson"]) ?? "{}", Convert.ToString(row["SchemaDraftJson"]) ?? "{}"))
            .Where(item => item.PageId.Length > 0)
            .ToDictionary(item => item.PageId, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task RecoverInterruptedRunsAsync(
        ISqlSugarClient migrationDb,
        MigrationScope? scope,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var runsQuery = migrationDb.Queryable<ApplicationDesignerMigrationRunEntity>()
            .Where(item => item.Status == "Running" && item.LockExpiresTime != null && item.LockExpiresTime <= now && !item.IsDeleted);
        if (scope is not null)
        {
            runsQuery = runsQuery.Where(item => item.TenantId == scope.TenantId && item.AppCode == scope.AppCode);
        }

        var interruptedRuns = await runsQuery.ToListAsync(cancellationToken);
        foreach (var run in interruptedRuns)
        {
            run.Status = "Failed";
            run.DiagnosticsJson = JsonSerializer.Serialize(new[]
            {
                new
                {
                    code = "migration.interrupted",
                    message = "Migration was interrupted before completion and will be retried from its immutable backup."
                }
            });
            run.CompletedTime = now;
            run.LockExpiresTime = null;
            await migrationDb.Updateable(run)
                .UpdateColumns(item => new { item.Status, item.DiagnosticsJson, item.CompletedTime, item.LockExpiresTime })
                .ExecuteCommandAsync(cancellationToken);
        }
    }

    private static void EnsureNoDuplicateDocuments(IEnumerable<ApplicationDesignerDocumentEntity> documents)
    {
        var duplicate = documents
            .GroupBy(item => (item.TenantId, item.AppCode, item.PageId))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ValidationException(
                $"Multiple current DesignerDocuments exist for page '{duplicate.Key.PageId}'.",
                ErrorCodes.DesignerSchemaInvalid);
        }
    }

    private static void EnsureDocumentMetadataMatchesPage(string canonicalJson, ApplicationDevelopmentPageEntity page)
    {
        var document = JsonNode.Parse(canonicalJson)?.AsObject()
            ?? throw new ValidationException("Designer Document metadata is invalid.", ErrorCodes.DesignerSchemaInvalid);
        var documentPageType = document["pageType"]?.GetValue<string>();
        if (!string.Equals(documentPageType, NormalizePageType(page.PageType), StringComparison.Ordinal))
        {
            throw new ValidationException("Designer Document page type does not match page metadata.", ErrorCodes.DesignerSchemaInvalid);
        }

        var expectedParameters = (JsonNode.Parse(page.PageParametersJson) as JsonArray ?? new JsonArray())
            .ToJsonString(ApplicationDataCenterJson.Options);
        var actualParameters = (document["pageParameters"] as JsonArray ?? new JsonArray())
            .ToJsonString(ApplicationDataCenterJson.Options);
        if (!string.Equals(expectedParameters, actualParameters, StringComparison.Ordinal))
        {
            throw new ValidationException("Designer Document page parameters do not match page metadata.", ErrorCodes.DesignerSchemaInvalid);
        }
    }

    private MigrationScope? ResolveScope()
    {
        if (currentUser is null || !currentUser.IsAsterErpAuthenticated())
        {
            return null;
        }

        var tenantId = currentUser.GetAsterErpTenantId();
        var appCode = currentUser.GetAsterErpAppCode();
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(appCode) ||
            string.Equals(appCode, "SYSTEM", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new MigrationScope(tenantId, appCode.Trim().ToUpperInvariant());
    }

    private static MigrationScope CreateMigrationScope(string tenantId, string appCode)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(appCode))
        {
            throw new ArgumentException("Application code is required.", nameof(appCode));
        }

        return new MigrationScope(tenantId.Trim(), appCode.Trim().ToUpperInvariant());
    }

    private ISqlSugarClient ResolveDatabase(MigrationScope? scope) =>
        scope is not null && workspaceDatabaseAccessor is not null
            ? workspaceDatabaseAccessor.GetCurrentDb()
            : db;

    private static void EnsurePageInScope(ApplicationDevelopmentPageEntity page, MigrationScope? scope)
    {
        if (scope is not null &&
            (!string.Equals(page.TenantId, scope.TenantId, StringComparison.Ordinal) ||
             !string.Equals(page.AppCode, scope.AppCode, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ValidationException("页面不属于当前应用工作区", ErrorCodes.PermissionDenied);
        }
    }

    private sealed record MigrationScope(string TenantId, string AppCode);

    private static MigrationData BuildMigrationData(
        ApplicationDevelopmentPageEntity page,
        LegacyPageSource legacySource)
    {
        var sourceJson = SelectSourceDocument(legacySource);
        var sourceHash = ComputeSourceHash(sourceJson);
        var targetJson = ApplicationDesignerCanonicalJson.NormalizeObject(NormalizeJson(sourceJson, page));
        var targetHash = ApplicationDesignerCanonicalJson.ComputeDocumentHash(targetJson);
        var document = new ApplicationDesignerDocumentEntity
        {
            TenantId = page.TenantId,
            AppCode = page.AppCode,
            PageId = page.Id,
            VersionId = page.VersionId,
            DocumentJson = targetJson,
            DocumentHash = targetHash,
            SourceHash = sourceHash,
            TargetHash = targetHash,
            MigrationRevision = MigrationRevision,
            DiagnosticsJson = "[]",
            Status = page.Status,
            CreatedBy = page.CreatedBy,
            CreatedTime = page.CreatedTime
        };
        var revision = new ApplicationDesignerRevisionEntity
        {
            TenantId = page.TenantId,
            AppCode = page.AppCode,
            DocumentId = document.Id,
            RevisionNumber = 1,
            DocumentJson = targetJson,
            DocumentHash = targetHash,
            SourceHash = sourceHash,
            TargetHash = targetHash,
            MigrationRevision = MigrationRevision,
            ChangeSetJson = "{\"type\":\"initial-migration\"}",
            DiagnosticsJson = "[]",
            CreatedBy = page.CreatedBy,
            CreatedTime = page.CreatedTime
        };
        document.CurrentRevisionId = revision.Id;
        return new MigrationData(document, revision, new ApplicationDesignerMigrationEntity
        {
            TenantId = page.TenantId,
            AppCode = page.AppCode,
            DocumentId = document.Id,
            PageId = page.Id,
            SourceHash = sourceHash,
            TargetHash = targetHash,
            MigrationRevision = MigrationRevision,
            DiagnosticsJson = "[]",
            BackupDocumentJson = sourceJson,
            BackupLocation = $"designer-migration://{page.TenantId}/{page.AppCode}/{page.Id}/{sourceHash}",
            RollbackRevisionId = revision.Id,
            StartedTime = DateTime.UtcNow,
            CompletedTime = DateTime.UtcNow,
            CreatedBy = page.CreatedBy,
            CreatedTime = DateTime.UtcNow
        });
    }

    private sealed record MigrationData(
        ApplicationDesignerDocumentEntity Document,
        ApplicationDesignerRevisionEntity Revision,
        ApplicationDesignerMigrationEntity Migration);

    private sealed record LegacyPageSource(string PageId, string LayoutDraftJson, string SchemaDraftJson);

    private static string SelectSourceDocument(LegacyPageSource source)
    {
        if (!string.IsNullOrWhiteSpace(source.LayoutDraftJson) && source.LayoutDraftJson != "{}")
        {
            return source.LayoutDraftJson;
        }

        if (!string.IsNullOrWhiteSpace(source.SchemaDraftJson) && source.SchemaDraftJson != "{}")
        {
            return source.SchemaDraftJson;
        }

        return source.LayoutDraftJson;
    }

    public string NormalizeLegacyDocument(string sourceJson, ApplicationDevelopmentPageEntity page) =>
        NormalizeJson(sourceJson, page);

    /// <summary>
    /// Returns a read-only inventory for the six known Designer document/artifact
    /// stores. Missing historical tables are already retired; an existing table
    /// without its expected JSON column is a retirement blocker. No DDL or row
    /// mutation is performed by this method.
    /// </summary>
    public Task<ApplicationDesignerMigrationInventory> GetMigrationInventoryAsync(CancellationToken cancellationToken = default) =>
        GetMigrationInventoryAsync(ResolveDatabase(ResolveScope()), cancellationToken);

    /// <summary>
    /// Stops historical-schema retirement while any active Designer document or
    /// runtime store still contains legacy layout fields or an invalid protocol.
    /// This is a read-only gate and is intended to run inside the caller's
    /// retirement transaction before destructive DDL.
    /// </summary>
    public async Task EnsureRetirementReadyAsync(ISqlSugarClient migrationDb, CancellationToken cancellationToken = default)
    {
        var inventory = await GetMigrationInventoryAsync(migrationDb, cancellationToken);
        if (!inventory.CanRetireLegacyLayoutFields)
        {
            throw new InvalidOperationException(
                $"Designer layout retirement is blocked by inventory: {string.Join("; ", inventory.RetirementBlockers)}");
        }
    }

    private async Task<ApplicationDesignerMigrationInventory> GetMigrationInventoryAsync(ISqlSugarClient migrationDb, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var schema = new SqliteSchemaExecutor(migrationDb);
        var tables = new List<ApplicationDesignerMigrationTableInventory>(LayoutInventoryTables.Length);
        var blockers = new List<string>();

        foreach (var (tableName, jsonColumn) in LayoutInventoryTables)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await schema.HasTableAsync(tableName, cancellationToken))
            {
                tables.Add(new ApplicationDesignerMigrationTableInventory(tableName, jsonColumn, false, true, 0, 0, 0, 0, 0));
                continue;
            }

            if (!await schema.HasColumnAsync(tableName, jsonColumn, cancellationToken))
            {
                tables.Add(new ApplicationDesignerMigrationTableInventory(tableName, jsonColumn, true, false, 0, 0, 0, 0, 0));
                blockers.Add($"{tableName}.{jsonColumn} is missing");
                continue;
            }

            var hasDeletedColumn = await schema.HasColumnAsync(tableName, "IsDeleted", cancellationToken);
            var query = $"SELECT \"{jsonColumn}\" AS JsonValue FROM \"{tableName}\"" + (hasDeletedColumn ? " WHERE IsDeleted = 0" : string.Empty);
            var rows = await schema.ExecuteDataTableAsync(query, cancellationToken);
            var legacyLayoutRows = 0;
            var canonicalProtocolRows = 0;
            var invalidJsonRows = 0;
            var invalidProtocolRows = 0;
            foreach (global::System.Data.DataRow row in rows.Rows)
            {
                var json = row["JsonValue"] is DBNull ? null : Convert.ToString(row["JsonValue"], CultureInfo.InvariantCulture);
                var classification = ClassifyLayoutJson(json);
                if (classification.InvalidJson)
                {
                    invalidJsonRows++;
                }
                else
                {
                    if (classification.HasLegacyLayout) legacyLayoutRows++;
                    if (classification.HasCanonicalProtocol && !classification.HasLegacyLayout) canonicalProtocolRows++;
                    if (classification.HasInvalidProtocol) invalidProtocolRows++;
                }
            }

            var tableInventory = new ApplicationDesignerMigrationTableInventory(
                tableName,
                jsonColumn,
                true,
                true,
                rows.Rows.Count,
                legacyLayoutRows,
                canonicalProtocolRows,
                invalidJsonRows,
                invalidProtocolRows);
            tables.Add(tableInventory);
            if (legacyLayoutRows > 0) blockers.Add($"{tableName} contains {legacyLayoutRows} row(s) with legacy layout fields");
            if (invalidJsonRows > 0) blockers.Add($"{tableName} contains {invalidJsonRows} invalid JSON row(s)");
            if (invalidProtocolRows > 0) blockers.Add($"{tableName} contains {invalidProtocolRows} invalid layout.protocol row(s)");
        }

        var totalRows = tables.Sum(item => item.RowCount);
        var legacyRows = tables.Sum(item => item.LegacyLayoutRows);
        var canonicalRows = tables.Sum(item => item.CanonicalProtocolRows);
        var invalidJsonRowsTotal = tables.Sum(item => item.InvalidJsonRows);
        var invalidProtocolRowsTotal = tables.Sum(item => item.InvalidProtocolRows);
        return new ApplicationDesignerMigrationInventory(
            tables,
            totalRows,
            legacyRows,
            canonicalRows,
            invalidJsonRowsTotal,
            invalidProtocolRowsTotal,
            blockers.Count == 0,
            blockers);
    }

    private static string NormalizeJson(string sourceJson, ApplicationDevelopmentPageEntity page)
    {
        try
        {
            var parsed = JsonNode.Parse(sourceJson) as JsonObject
                ?? throw new ValidationException("源页面文档必须是 JSON 对象", AsterERP.Shared.ErrorCodes.DesignerSchemaInvalid);
            var document = parsed["document"] as JsonObject ?? parsed;
            foreach (var property in new[] { "schemaVersion", "tree", "selectedElementId", "selectedNodeIds", "primaryNodeId", "anchorNodeId", "viewport", "history", "historyIndex", "dirty", "saving", "selection", "editorState", "runtimeEditorState", "panelState", "transactionId" })
            {
                document.Remove(property);
            }

            document["documentId"] = page.Id;
            document["revision"] = 1;
            document["metadata"] ??= new JsonObject
            {
                ["pageCode"] = page.PageCode,
                ["pageName"] = page.PageName
            };
            var pageType = NormalizePageType(page.PageType);
            var pageParameters = JsonNode.Parse(page.PageParametersJson) as JsonArray ?? new JsonArray();
            document["pageType"] = pageType;
            document["pageParameters"] = pageParameters.DeepClone();
            var runtimeContext = document["runtimeContext"] as JsonObject ?? new JsonObject();
            runtimeContext["pageCode"] = page.PageCode;
            runtimeContext["pageName"] = page.PageName;
            runtimeContext["pageType"] = pageType;
            runtimeContext["pageParameters"] = pageParameters.DeepClone();
            document["runtimeContext"] = runtimeContext;
            if (document["pages"] is not JsonArray pages || pages.Count == 0)
            {
                var existingRootId = (document["elements"] as JsonObject)?.FirstOrDefault().Key ?? $"{page.PageCode}_root";
                document["pages"] = new JsonArray(new JsonObject
                {
                    ["id"] = page.PageCode,
                    ["name"] = page.PageName,
                    ["rootElementId"] = existingRootId
                });
            }
            NormalizeElements(document, page);
            NormalizeLegacyValues(document, preserveCanonicalDefinitions: true);
            NormalizeLatestActionTypes(document);
            return document.ToJsonString(ApplicationDataCenterJson.Options);
        }
        catch (JsonException exception)
        {
            throw new ValidationException($"源页面文档不是合法 JSON：{exception.Message}", AsterERP.Shared.ErrorCodes.DesignerSchemaInvalid);
        }
    }

    private static void NormalizeElements(JsonObject document, ApplicationDevelopmentPageEntity page)
    {
        var elements = document["elements"] as JsonObject ?? new JsonObject();
        document["elements"] = elements;
        var rootId = document["pages"]?.AsArray().FirstOrDefault()?["rootElementId"]?.GetValue<string>() ?? $"{page.PageCode}_root";
        foreach (var (id, value) in elements.ToArray())
        {
            var node = value as JsonObject ?? new JsonObject();
            node["id"] ??= id;
            node["type"] ??= "layout.page";
            node["children"] ??= new JsonArray();
            node["parentId"] ??= id == rootId ? null : FindParentId(elements, id);
            node["props"] ??= new JsonObject();
            node["layout"] ??= new JsonObject();
            node["style"] ??= new JsonObject();
            node["events"] ??= new JsonArray();
            node["layout"] = MigrateLegacyLayoutToCanonicalProtocol(node["layout"] as JsonObject ?? new JsonObject());
            NormalizeLegacyBindings(node);
            elements[id] = node;
        }

        foreach (var (id, value) in elements.ToArray())
        {
            if (value is not JsonObject node)
            {
                continue;
            }

            var normalizedChildren = new JsonArray();
            var childIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (node["children"] is JsonArray children)
            {
                foreach (var child in children)
                {
                    if (child is not JsonValue childValue || !childValue.TryGetValue<string>(out var childId) ||
                        string.IsNullOrWhiteSpace(childId) || !elements.ContainsKey(childId) || !childIds.Add(childId))
                    {
                        continue;
                    }

                    normalizedChildren.Add(childId);
                }
            }

            node["children"] = normalizedChildren;
            if (id != rootId && node["parentId"] is JsonValue parentValue &&
                parentValue.TryGetValue<string>(out var parentId) &&
                (string.IsNullOrWhiteSpace(parentId) || !elements.ContainsKey(parentId)))
            {
                node["parentId"] = null;
            }
        }
    }

    private static bool IsPublishedArtifactStale(
        ApplicationDesignerDocumentEntity document,
        ApplicationDesignerRuntimeArtifactEntity artifact)
    {
        if (artifact.ArtifactJson.Contains("::", StringComparison.Ordinal) ||
            !string.Equals(artifact.DocumentId, document.Id, StringComparison.Ordinal) ||
            !string.Equals(artifact.RevisionId, document.CurrentRevisionId, StringComparison.Ordinal) ||
            !string.Equals(artifact.TargetHash, document.DocumentHash, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            var root = JsonNode.Parse(artifact.ArtifactJson) as JsonObject;
            if (root is null)
            {
                return true;
            }

            return !string.Equals(root["artifactHash"]?.GetValue<string>(), artifact.ArtifactHash, StringComparison.OrdinalIgnoreCase) ||
                   !string.Equals(root["signature"]?.GetValue<string>(), artifact.SignatureHash, StringComparison.OrdinalIgnoreCase) ||
                   !string.Equals(root["compilerVersion"]?.GetValue<string>(), artifact.CompilerRevision, StringComparison.Ordinal) ||
                   !string.Equals(root["migrationRevision"]?.GetValue<string>(), artifact.MigrationRevision, StringComparison.Ordinal) ||
                   (root["revision"]?.GetValue<int>() ?? 0) != artifact.RevisionNumber ||
                   !string.Equals(root["migrationRevision"]?.GetValue<string>(), RuntimeCapabilityContract.MigrationRevision, StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            return true;
        }
    }

    private static LayoutJsonClassification ClassifyLayoutJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new LayoutJsonClassification(true, false, false, false);
        try
        {
            var root = JsonNode.Parse(json) as JsonObject;
            var document = root?["document"] as JsonObject ?? root;
            if (document?["elements"] is not JsonObject elements)
            {
                return new LayoutJsonClassification(false, false, false, false);
            }

            var hasLegacyLayout = false;
            var hasCanonicalProtocol = false;
            var hasInvalidProtocol = false;
            foreach (var element in elements.Select(item => item.Value).OfType<JsonObject>())
            {
                if (element["layout"] is not JsonObject layout) continue;
                if (LegacyLayoutProperties.Any(layout.ContainsKey)) hasLegacyLayout = true;
                if (layout["protocol"] is JsonObject protocol)
                {
                    if (HasCanonicalProtocolShape(protocol)) hasCanonicalProtocol = true;
                    else hasInvalidProtocol = true;
                }
            }

            return new LayoutJsonClassification(false, hasLegacyLayout, hasCanonicalProtocol, hasInvalidProtocol);
        }
        catch (JsonException)
        {
            return new LayoutJsonClassification(true, false, false, false);
        }
    }

    private static bool HasCanonicalProtocolShape(JsonObject protocol) =>
        protocol["container"] is JsonObject &&
        protocol["placement"] is JsonObject &&
        protocol["size"] is JsonObject;

    private readonly record struct LayoutJsonClassification(
        bool InvalidJson,
        bool HasLegacyLayout,
        bool HasCanonicalProtocol,
        bool HasInvalidProtocol);

    /// <summary>
    /// Converts the persisted legacy layout projection into the canonical layout.protocol shape.
    /// The input is never mutated, and an existing protocol is preserved so repeated migration is stable.
    /// </summary>
    public static JsonObject MigrateLegacyLayoutToCanonicalProtocol(JsonObject layout)
    {
        var migrated = (JsonObject)layout.DeepClone();
        if (migrated.ContainsKey("protocol"))
        {
            RemoveLegacyLayoutProperties(migrated);
            return migrated;
        }

        var mode = ReadLegacyLayoutMode(migrated);
        var protocol = new JsonObject
        {
            ["container"] = BuildLegacyContainer(migrated, mode),
            ["placement"] = BuildLegacyPlacement(migrated, mode),
            ["size"] = BuildLegacySize(migrated)
        };
        RemoveLegacyLayoutProperties(migrated);
        migrated["protocol"] = protocol;
        return migrated;
    }

    private static string ReadLegacyLayoutMode(JsonObject layout)
    {
        var explicitMode = ReadString(layout["layoutMode"]);
        if (explicitMode is not null)
        {
            return explicitMode.ToLowerInvariant() switch
            {
                "flex" => "flex",
                "grid" => "grid",
                "constraints" => "constraints",
                "free" or "absolute" => "free",
                _ => "free"
            };
        }

        return ReadString(layout["display"])?.ToLowerInvariant() switch
        {
            "flex" => "flex",
            "grid" => "grid",
            "constraints" => "constraints",
            _ when layout["constraints"] is JsonObject => "constraints",
            _ => "free"
        };
    }

    private static JsonObject BuildLegacyContainer(JsonObject layout, string mode) => mode switch
    {
        "flex" => new JsonObject
        {
            ["mode"] = "flex",
            ["flex"] = new JsonObject
            {
                ["direction"] = ReadFlexDirection(layout),
                ["wrap"] = ReadFlexWrap(layout),
                ["gap"] = JsonValue.Create(ReadNonNegativeNumber(layout["gap"])),
                ["alignItems"] = ReadAlignment(layout["alignItems"], "start"),
                ["justifyContent"] = ReadJustifyContent(layout["justifyContent"])
            }
        },
        "grid" => new JsonObject
        {
            ["mode"] = "grid",
            ["grid"] = new JsonObject
            {
                ["columns"] = ReadGridTracks(layout["gridTemplateColumns"], "1fr"),
                ["rows"] = ReadGridTracks(layout["gridTemplateRows"], "auto"),
                ["columnGap"] = JsonValue.Create(ReadNonNegativeNumber(layout["columnGap"] ?? layout["gap"])),
                ["rowGap"] = JsonValue.Create(ReadNonNegativeNumber(layout["rowGap"] ?? layout["gap"])),
                ["autoFlow"] = ReadGridAutoFlow(layout["gridAutoFlow"])
            }
        },
        "constraints" => new JsonObject
        {
            ["mode"] = "constraints",
            ["constraints"] = new JsonObject { ["coordinateSpace"] = "parent-padding-box" }
        },
        _ => new JsonObject { ["mode"] = "free" }
    };

    private static JsonObject BuildLegacyPlacement(JsonObject layout, string mode) => mode switch
    {
        "flex" => new JsonObject
        {
            ["kind"] = "flex-item",
            ["flexItem"] = new JsonObject
            {
                ["order"] = JsonValue.Create(ReadFiniteNumber(layout["order"])),
                ["grow"] = JsonValue.Create(ReadNonNegativeNumber(layout["flexGrow"])),
                ["shrink"] = JsonValue.Create(ReadNonNegativeNumber(layout["flexShrink"], 1)),
                ["basis"] = ReadDimension(layout["flexBasis"]),
                ["alignSelf"] = ReadAlignment(layout["alignSelf"], "auto", "auto")
            }
        },
        "grid" => BuildLegacyGridPlacement(layout),
        "constraints" => new JsonObject
        {
            ["kind"] = "constrained",
            ["constrained"] = BuildLegacyConstraintPlacement(layout)
        },
        _ => BuildLegacyAbsolutePlacement(layout)
    };

    private static JsonObject BuildLegacyAbsolutePlacement(JsonObject layout)
    {
        var absolute = new JsonObject
        {
            ["x"] = JsonValue.Create(ReadFiniteNumber(layout["x"])),
            ["y"] = JsonValue.Create(ReadFiniteNumber(layout["y"]))
        };
        if (ReadOptionalNumber(layout["zIndex"]) is { } zIndex)
        {
            absolute["zIndex"] = zIndex;
        }

        return new JsonObject
        {
            ["kind"] = "absolute",
            ["absolute"] = absolute
        };
    }

    private static JsonObject BuildLegacyGridPlacement(JsonObject layout)
    {
        var area = ReadString(layout["gridArea"])?.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var row = ReadLegacyGridLine(
            layout["gridRow"] ?? layout["gridRowStart"] ?? (area?.Length == 4 ? JsonValue.Create(area[0]) : null),
            layout["gridRowEnd"] ?? (area?.Length == 4 ? JsonValue.Create(area[2]) : null));
        var column = ReadLegacyGridLine(
            layout["gridColumn"] ?? layout["gridColumnStart"] ?? (area?.Length == 4 ? JsonValue.Create(area[1]) : null),
            layout["gridColumnEnd"] ?? (area?.Length == 4 ? JsonValue.Create(area[3]) : null));
        return new JsonObject
        {
            ["kind"] = "grid-item",
            ["gridItem"] = new JsonObject
            {
                ["rowStart"] = row.Start,
                ["rowSpan"] = row.Span,
                ["columnStart"] = column.Start,
                ["columnSpan"] = column.Span,
                ["alignSelf"] = ReadAlignment(layout["alignSelf"], "auto", "auto"),
                ["justifySelf"] = ReadAlignment(layout["justifySelf"], "auto", "auto")
            }
        };
    }

    private static JsonObject BuildLegacyConstraintPlacement(JsonObject layout)
    {
        var source = layout["constraints"] as JsonObject;
        var result = new JsonObject();
        foreach (var name in new[] { "left", "right", "top", "bottom", "centerX", "centerY" })
        {
            var value = source?[name] ?? layout[name];
            if (TryReadFiniteNumber(value, out var number))
            {
                result[name] = JsonValue.Create(number);
            }
        }

        foreach (var name in new[] { "stretchX", "stretchY" })
        {
            var value = source?[name] ?? layout[name];
            if (value is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out var enabled))
            {
                result[name] = enabled;
            }
        }

        return result;
    }

    private static JsonObject BuildLegacySize(JsonObject layout)
    {
        var size = new JsonObject
        {
            ["width"] = ReadDimension(layout["width"]),
            ["height"] = ReadDimension(layout["height"])
        };
        foreach (var name in new[] { "minWidth", "maxWidth", "minHeight", "maxHeight" })
        {
            if (IsDimension(layout[name]))
            {
                size[name] = layout[name]!.DeepClone();
            }
        }

        if (TryReadFiniteNumber(layout["aspectRatio"], out var aspectRatio) && aspectRatio > 0)
        {
            size["aspectRatio"] = JsonValue.Create(aspectRatio);
        }

        return size;
    }

    private static void RemoveLegacyLayoutProperties(JsonObject layout)
    {
        foreach (var property in LegacyLayoutProperties)
        {
            layout.Remove(property);
        }
    }

    private static string ReadFlexDirection(JsonObject layout) => ReadString(layout["flexDirection"])?.ToLowerInvariant() switch
    {
        "row-reverse" => "row-reverse",
        "column" => "column",
        "column-reverse" => "column-reverse",
        _ => "row"
    };

    private static string ReadFlexWrap(JsonObject layout) => ReadString(layout["flexWrap"])?.ToLowerInvariant() switch
    {
        "wrap" => "wrap",
        "wrap-reverse" => "wrap-reverse",
        _ => "nowrap"
    };

    private static string ReadAlignment(JsonNode? value, string fallback, string? autoFallback = null) => ReadString(value)?.ToLowerInvariant() switch
    {
        "flex-start" or "start" => "start",
        "flex-end" or "end" => "end",
        "center" => "center",
        "stretch" => "stretch",
        "baseline" => "baseline",
        "auto" when autoFallback is not null => autoFallback,
        "auto" => "auto",
        _ => fallback
    };

    private static string ReadJustifyContent(JsonNode? value) => ReadString(value)?.ToLowerInvariant() switch
    {
        "flex-start" or "start" => "start",
        "flex-end" or "end" => "end",
        "center" => "center",
        "space-between" => "space-between",
        "space-around" => "space-around",
        "space-evenly" => "space-evenly",
        _ => "start"
    };

    private static string ReadGridAutoFlow(JsonNode? value) => ReadString(value)?.ToLowerInvariant() switch
    {
        "column" => "column",
        "dense" => "dense",
        "row-dense" => "row-dense",
        "column-dense" => "column-dense",
        _ => "row"
    };

    private static JsonArray ReadGridTracks(JsonNode? value, string fallback)
    {
        var tracks = new JsonArray();
        if (TryReadFiniteNumber(value, out var count))
        {
            var trackCount = Math.Clamp((int)Math.Truncate(count), 1, 100);
            for (var index = 0; index < trackCount; index++)
            {
                tracks.Add($"repeat({trackCount}, minmax(0, 1fr))");
            }

            return tracks;
        }

        tracks.Add(ReadString(value) is { Length: > 0 } text ? text : fallback);
        return tracks;
    }

    private static (JsonNode Start, int Span) ReadLegacyGridLine(JsonNode? value, JsonNode? endValue)
    {
        var text = ReadString(value);
        if (text is not null)
        {
            var parts = text.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0 && parts[0].Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                return (JsonValue.Create("auto")!, 1);
            }

            if (TryReadPositiveInteger(parts.FirstOrDefault(), out var start))
            {
                if (parts.Length > 1 && parts[1].StartsWith("span ", StringComparison.OrdinalIgnoreCase) &&
                    TryReadPositiveInteger(parts[1]["span ".Length..], out var span))
                {
                    return (JsonValue.Create(start)!, span);
                }

                if (parts.Length > 1 && TryReadPositiveInteger(parts[1], out var end))
                {
                    return (JsonValue.Create(start)!, Math.Max(1, end - start));
                }

                return (JsonValue.Create(start)!, 1);
            }
        }

        if (TryReadPositiveInteger(value, out var numericStart))
        {
            var span = TryReadPositiveInteger(endValue, out var numericEnd) ? Math.Max(1, numericEnd - numericStart) : 1;
            return (JsonValue.Create(numericStart)!, span);
        }

        return (JsonValue.Create("auto")!, 1);
    }

    private static bool TryReadPositiveInteger(string? value, out int number) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number) && number > 0;

    private static bool TryReadPositiveInteger(JsonNode? value, out int number)
    {
        if (TryReadFiniteNumber(value, out var numeric) && numeric > 0 && numeric == Math.Truncate(numeric) && numeric <= int.MaxValue)
        {
            number = (int)numeric;
            return true;
        }

        number = default;
        return false;
    }

    private static JsonNode ReadDimension(JsonNode? value) => IsDimension(value) ? value!.DeepClone() : JsonValue.Create("auto")!;

    private static bool IsDimension(JsonNode? value)
    {
        if (TryReadFiniteNumber(value, out var number))
        {
            return number >= 0;
        }

        var text = ReadString(value);
        if (text is null)
        {
            return false;
        }

        if (text is "auto" or "min-content" or "max-content" or "fit-content")
        {
            return true;
        }

        if (!text.EndsWith('%') && !text.EndsWith("px", StringComparison.Ordinal))
        {
            return false;
        }

        var numericPart = text.EndsWith('%') ? text[..^1] : text[..^2];
        return decimal.TryParse(numericPart, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var numeric) && numeric >= 0;
    }

    private static string? ReadString(JsonNode? value) =>
        value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text) ? text?.Trim() : null;

    private static double ReadFiniteNumber(JsonNode? value, double fallback = 0) =>
        TryReadFiniteNumber(value, out var number) ? number : fallback;

    private static double ReadNonNegativeNumber(JsonNode? value, double fallback = 0) =>
        Math.Max(0, ReadFiniteNumber(value, fallback));

    private static JsonNode? ReadOptionalNumber(JsonNode? value) =>
        TryReadFiniteNumber(value, out var number) ? JsonValue.Create(number) : null;

    private static bool TryReadFiniteNumber(JsonNode? value, out double number)
    {
        number = default;
        return value is JsonValue jsonValue &&
            jsonValue.TryGetValue<double>(out number) &&
            double.IsFinite(number);
    }

    private static string? FindParentId(JsonObject elements, string childId)
    {
        foreach (var (id, value) in elements)
        {
            if (value?["children"] is JsonArray children && children.Any(item => item?.GetValue<string>() == childId))
            {
                return id;
            }
        }

        return null;
    }

    private static void NormalizeLegacyBindings(JsonObject node)
    {
        var legacyBindings = node["bindings"] as JsonObject ?? new JsonObject();
        var bindings = new JsonObject();
        var props = node["props"] as JsonObject ?? new JsonObject();
        foreach (var (key, child) in legacyBindings)
        {
            var normalized = child is JsonObject childObject
                ? NormalizeLegacyValueObject(key, childObject)
                : child is JsonArray array ? NormalizeLegacyArray(array, key) : child;
            if (string.Equals(key, "props", StringComparison.Ordinal))
            {
                if (normalized is not JsonObject propertyBindings)
                {
                    throw new ValidationException("Legacy bindings.props must be an object before DesignerDocument migration.", ErrorCodes.DesignerSchemaInvalid);
                }

                foreach (var (propertyName, propertyValue) in propertyBindings)
                {
                    SetNestedJsonValue(props, propertyName, propertyValue?.DeepClone());
                }
            }
            else if (key.StartsWith("props.", StringComparison.Ordinal) && key.Length > "props.".Length)
            {
                SetNestedJsonValue(props, key["props.".Length..], normalized?.DeepClone());
            }
            else
            {
                bindings[key] = normalized?.DeepClone();
            }
        }

        if (node["dataBinding"] is JsonObject legacyDataBinding)
        {
            var legacyExpression = legacyDataBinding["valueExpression"] as JsonObject ?? legacyDataBinding;
            if (IsLegacyResourceExpression(legacyExpression))
            {
                bindings["data"] = ToResourceRef(legacyExpression);
            }
            else if (legacyDataBinding["field"]?.GetValue<string>() is { Length: > 0 } field)
            {
                bindings["data"] = new JsonObject { ["field"] = field.Trim() };
            }
            else if (legacyDataBinding.Count > 0)
            {
                throw new ValidationException("Legacy data binding must contain a non-empty source and path before DesignerDocument migration.", ErrorCodes.DesignerSchemaInvalid);
            }
        }

        node["bindings"] = bindings;
        node["props"] = NormalizeLegacyValues(props);
        if (node["layout"] is JsonObject layout) node["layout"] = NormalizeLegacyValues(layout);
        if (node["style"] is JsonObject style) node["style"] = NormalizeLegacyValues(style);
        node.Remove("dataBinding");
    }

    private static void SetNestedJsonValue(JsonObject target, string path, JsonNode? value)
    {
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0) return;
        var current = target;
        foreach (var segment in segments[..^1])
        {
            if (current[segment] is not JsonObject child)
            {
                child = new JsonObject();
                current[segment] = child;
            }

            current = child;
        }

        current[segments[^1]] = value;
    }

    private static JsonObject NormalizeLegacyValues(JsonObject value, bool preserveCanonicalDefinitions = false)
    {
        foreach (var key in value.Select(item => item.Key).ToArray())
        {
            if (preserveCanonicalDefinitions && IsCanonicalDefinitionContainer(key))
            {
                continue;
            }

            var child = value[key];
            var normalized = child is JsonObject childObject
                ? NormalizeLegacyValueObject(key, childObject)
                : child is JsonArray array ? NormalizeLegacyArray(array, key) : child;
            if (!ReferenceEquals(child, normalized))
            {
                value[key] = normalized;
            }
        }
        return value;
    }

    private static JsonArray NormalizeLegacyArray(JsonArray value, string propertyName)
    {
        for (var index = 0; index < value.Count; index++)
        {
            var child = value[index];
            var normalized = child is JsonObject childObject
                ? NormalizeLegacyArrayObject(propertyName, childObject)
                : child is JsonArray array ? NormalizeLegacyArray(array, propertyName) : child;
            if (!ReferenceEquals(child, normalized))
            {
                value[index] = normalized;
            }
        }
        return value;
    }

    private static bool IsCanonicalDefinitionContainer(string propertyName) =>
        string.Equals(propertyName, "variables", StringComparison.Ordinal);

    private static JsonObject NormalizeLegacyArrayObject(string propertyName, JsonObject value)
    {
        if (value.ContainsKey("source") || value.ContainsKey("path"))
        {
            if (string.Equals(propertyName, "selectionMappings", StringComparison.Ordinal) &&
                value["path"]?.GetValue<string>() is { Length: 0 })
            {
                value.Remove("path");
                return value;
            }

            return NormalizeLegacyValueObject(propertyName, value);
        }

        return NormalizeLegacyValues(value);
    }

    private static JsonObject NormalizeLegacyValueObject(string propertyName, JsonObject value)
    {
        if (IsMicroflowExpressionProperty(propertyName))
        {
            if (value["path"]?.GetValue<string>() is { Length: > 0 })
            {
                return ToResourceRef(value);
            }

            if (value.ContainsKey("path"))
            {
                value.Remove("path");
            }

            return value;
        }

        if (value.ContainsKey("source") || value.ContainsKey("path"))
        {
            if (!IsLegacyResourceExpression(value))
            {
                throw new ValidationException(
                    $"Legacy source/path binding at '{propertyName}' must contain non-empty source and path.",
                    ErrorCodes.DesignerSchemaInvalid);
            }

            return ToResourceRef(value);
        }

        if (IsResourceRef(value))
        {
            return NormalizeResourceRef(value);
        }

        return NormalizeLegacyValues(value);
    }

    private static bool IsMicroflowExpressionProperty(string propertyName) =>
        string.Equals(propertyName, "sourceExpression", StringComparison.Ordinal) ||
        string.Equals(propertyName, "valueExpression", StringComparison.Ordinal);

    private static void NormalizeLatestActionTypes(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            if (obj["type"] is JsonValue typeValue &&
                typeValue.TryGetValue<string>(out var type) &&
                string.Equals(type, "runMicroflow", StringComparison.Ordinal))
            {
                obj["type"] = "runPageMicroflow";
            }

            foreach (var child in obj.Select(item => item.Value).ToArray())
            {
                if (child is not null)
                {
                    NormalizeLatestActionTypes(child);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                if (child is not null)
                {
                    NormalizeLatestActionTypes(child);
                }
            }
        }
    }

    private static bool IsLegacyResourceExpression(JsonObject value) =>
        value["source"]?.GetValue<string>() is { Length: > 0 } && value["path"]?.GetValue<string>() is { Length: > 0 };

    private static bool IsResourceRef(JsonObject value) =>
        value["resourceId"]?.GetValue<string>() is { Length: > 0 } &&
        (value.ContainsKey("displayName") || value.ContainsKey("resourceType") || value.ContainsKey("valueType"));

    private static JsonObject NormalizeResourceRef(JsonObject value)
    {
        var resourceId = value["resourceId"]?.GetValue<string>()?.Trim();
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            throw new ValidationException(
                "Latest ResourceRef must contain a non-empty resourceId.",
                ErrorCodes.DesignerSchemaInvalid);
        }

        var resourceType = value["resourceType"]?.GetValue<string>()?.Trim();
        value["resourceType"] = resourceType ?? ResourceTypeFromId(resourceId);
        value["resourceId"] = NormalizeResourceId(resourceId, value["resourceType"]?.GetValue<string>());
        if (!value.ContainsKey("expectedType") && value["valueType"] is JsonNode valueType)
        {
            value["expectedType"] = valueType.DeepClone();
        }
        value["conversionPipeline"] ??= new JsonArray();
        return value;
    }

    private static string NormalizeResourceId(string resourceId, string? resourceType)
    {
        if (!resourceId.Contains("::", StringComparison.Ordinal)) return resourceId.Trim();

        var parts = resourceId.Split("::", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var source = string.IsNullOrWhiteSpace(resourceType) ? parts.FirstOrDefault() ?? string.Empty : resourceType.Trim();
        if (parts.Count > 0 && string.Equals(parts[0], source, StringComparison.OrdinalIgnoreCase)) parts.RemoveAt(0);
        var path = parts.Count > 0 ? parts[^1] : "*";
        if (parts.Count > 0) parts.RemoveAt(parts.Count - 1);
        var fields = new[] { source, string.Join(":", parts), path }
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim());
        return string.Join(":", fields);
    }

    private static string ResourceTypeFromId(string resourceId) =>
        resourceId.Contains(':')
            ? resourceId[..resourceId.IndexOf(':')].Trim()
            : resourceId.Trim();

    private static JsonObject ToResourceRef(JsonObject value)
    {
        var source = value["source"]?.GetValue<string>();
        var path = value["path"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(path))
        {
            throw new ValidationException(
                "Legacy data binding must contain a non-empty source and path before latest DesignerDocument migration.",
                ErrorCodes.DesignerSchemaInvalid);
        }

        var modelCode = value["modelCode"]?.GetValue<string>() ?? string.Empty;
        var valueType = value["expectedType"]?.GetValue<string>() ?? value["valueType"]?.GetValue<string>() ?? "json";
        var result = new JsonObject
        {
            ["displayName"] = $"{source}.{path}",
            ["resourceId"] = NormalizeResourceId($"{source}::{modelCode}::{path}", source),
            ["resourceType"] = source,
            ["expectedType"] = valueType,
            ["conversionPipeline"] = new JsonArray(),
            ["valueType"] = valueType
        };
        if (value["fallback"] is JsonNode fallback)
        {
            result["fallback"] = new JsonObject { ["kind"] = valueType, ["value"] = fallback.DeepClone() };
        }
        return result;
    }

    private static string NormalizePageType(string? value) =>
        ApplicationDevelopmentPageTypes.IsValid(value ?? string.Empty)
            ? value!.Trim().ToLowerInvariant()
            : ApplicationDevelopmentPageTypes.Standard;

    private static string ComputeHash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string ComputeSourceHash(string sourceJson) => $"sha256:{ComputeHash(sourceJson)}";

    private static string BuildRollbackPointer(IEnumerable<object> entries) =>
        JsonSerializer.Serialize(new
        {
            kind = "designer-migration-rollback",
            entries = entries.ToArray()
        });

    private static async Task<string> ValidateMigrationHealthAsync(
        ISqlSugarClient db,
        IReadOnlyCollection<ApplicationDesignerDocumentEntity> documents,
        CancellationToken cancellationToken)
    {
        var entries = new List<object>();
        foreach (var document in documents)
        {
            var persisted = (await db.Queryable<ApplicationDesignerDocumentEntity>()
                    .Where(item => item.Id == document.Id && !item.IsDeleted)
                    .Take(1)
                    .ToListAsync(cancellationToken)).FirstOrDefault();
            var revision = (await db.Queryable<ApplicationDesignerRevisionEntity>()
                    .Where(item => item.Id == document.CurrentRevisionId && !item.IsDeleted)
                    .Take(1)
                    .ToListAsync(cancellationToken)).FirstOrDefault();
            if (persisted is null || revision is null ||
                !string.Equals(persisted.DocumentHash, document.DocumentHash, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(revision.DocumentHash, document.DocumentHash, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(persisted.DocumentJson, document.DocumentJson, StringComparison.Ordinal) ||
                !string.Equals(revision.DocumentJson, document.DocumentJson, StringComparison.Ordinal))
            {
                throw new ValidationException($"Migration health check failed for Designer Document '{document.Id}'.", ErrorCodes.DesignerSchemaInvalid);
            }
            entries.Add(new { document.Id, document.DocumentHash, document.CurrentRevisionId });
        }

        var healthPayload = JsonSerializer.Serialize(new
        {
            kind = "designer-migration-health",
            entries = entries.ToArray()
        });
        return $"migration-health:{ApplicationDesignerCanonicalJson.ComputeHash(ApplicationDesignerCanonicalJson.NormalizeObject(healthPayload))}";
    }
}
