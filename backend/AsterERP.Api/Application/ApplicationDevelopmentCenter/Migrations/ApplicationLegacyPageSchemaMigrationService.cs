using System.Text.Json;
using System.Text.Json.Nodes;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Application.ApplicationDevelopmentCenter.Runtime;
using AsterERP.Api.Application.ApplicationConsole;
using AsterERP.Api.Modules.ApplicationDevelopmentCenter;
using AsterERP.Api.Modules.Platform;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using AsterERP.Contracts.ApplicationConsole;
using AsterERP.Contracts.ApplicationDevelopmentCenter;
using AsterERP.Contracts.ApplicationDesigner;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationDevelopmentCenter.Migrations;

public sealed class ApplicationLegacyPageSchemaMigrationService(
    ISqlSugarClient db,
    ApplicationDesignerDocumentMigrationService documentMigrationService,
    ApplicationDevelopmentSchemaCompiler schemaCompiler,
    ApplicationDesignerArtifactPublisher artifactPublisher,
    ApplicationDatabaseBindingResolver? bindingResolver = null,
    IApplicationDatabaseConnectionFactory? connectionFactory = null,
    ApplicationSystemAdministrationSchemaInitializer? schemaInitializer = null,
    ILogger<ApplicationLegacyPageSchemaMigrationService>? logger = null)
{
    public const string MigrationRevision = "application-page-schema-to-runtime-artifact-latest";

    public async Task<int> MigrateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (bindingResolver is not null && connectionFactory is not null)
        {
            return await MigrateRegisteredApplicationDatabasesAsync(bindingResolver, connectionFactory, cancellationToken);
        }

        return await MigrateDatabaseAsync(db, cancellationToken);
    }

    /// <summary>
    /// Runs both one-time migration stages against an already resolved
    /// application workspace database. No database path is inferred here.
    /// </summary>
    public async Task<int> MigrateWorkspaceOnceAsync(
        ISqlSugarClient workspaceDb,
        string tenantId,
        string appCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspaceDb);
        var migrated = await documentMigrationService.MigrateWorkspaceOnceAsync(
            workspaceDb,
            tenantId,
            appCode,
            cancellationToken);
        return migrated + await MigrateDatabaseAsync(workspaceDb, cancellationToken);
    }

    private async Task<int> MigrateRegisteredApplicationDatabasesAsync(
        ApplicationDatabaseBindingResolver resolver,
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
            var resolution = resolver.ResolveStatus(
                application.ConfigJson,
                application.TenantId,
                application.AppCode);
            if (resolution.Status != ApplicationDatabaseBindingStatus.Ready || resolution.Options is null)
            {
                logger?.LogWarning(
                    "Legacy page schema migration skipped {TenantId}/{AppCode}: {Status} - {Message}",
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
                    "Legacy page schema migration skipped {TenantId}/{AppCode}: {Status}",
                    application.TenantId,
                    application.AppCode,
                    ApplicationDatabaseBindingStatus.Unavailable);
                continue;
            }

            schemaInitializer?.Initialize(applicationDb);
            migrated += await MigrateDatabaseAsync(applicationDb, cancellationToken);
        }

        return migrated;
    }

    private async Task<int> MigrateDatabaseAsync(ISqlSugarClient migrationDb, CancellationToken cancellationToken)
    {
        var schema = new SqliteSchemaExecutor(migrationDb);
        if (!await schema.HasTableAsync("system_page_schemas", cancellationToken))
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
            var legacyRows = await ReadLegacyRowsAsync(schema, cancellationToken);
            var migrated = 0;
            foreach (var legacyRow in legacyRows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = (await migrationDb.Queryable<ApplicationDevelopmentPageEntity>()
                        .Where(item => !item.IsDeleted && item.TenantId == legacyRow.TenantId && item.AppCode == legacyRow.AppCode && item.PageCode == legacyRow.PageCode)
                        .Take(1)
                        .ToListAsync(cancellationToken))
                    .FirstOrDefault();
                if (page is null)
                {
                    logger?.LogWarning("Legacy page schema migration skipped {LegacySchemaId}: no matching latest development page.", legacyRow.Id);
                    continue;
                }
                var document = (await migrationDb.Queryable<ApplicationDesignerDocumentEntity>()
                        .Where(item => !item.IsDeleted && item.TenantId == page.TenantId && item.AppCode == page.AppCode && item.PageId == page.Id)
                        .OrderBy(item => item.CreatedTime, OrderByType.Desc)
                        .Take(1)
                        .ToListAsync(cancellationToken))
                    .FirstOrDefault();
                if (document is null)
                {
                    logger?.LogWarning("Legacy page schema migration skipped {LegacySchemaId}: no matching DesignerDocument.", legacyRow.Id);
                    continue;
                }
                await MigrateRowAsync(migrationDb, legacyRow, page, document, cancellationToken);
                migrated++;
            }

            await documentMigrationService.EnsureRetirementReadyAsync(migrationDb, cancellationToken);

            await schema.RenameColumnIfExistsAsync("app_dev_pages", "PublishedPageSchemaId", "PublishedArtifactId", cancellationToken);
            await schema.RenameColumnIfExistsAsync("system_menus", "PageSchemaId", "ArtifactId", cancellationToken);
            await schema.DropColumnIfExistsAsync("app_designer_publish_records", "PublishedPageSchemaId", cancellationToken);
            await schema.DropColumnIfExistsAsync("app_dev_pages", "LayoutDraftJson", cancellationToken);
            await schema.DropColumnIfExistsAsync("app_dev_pages", "SchemaDraftJson", cancellationToken);
            await schema.DropTableIfExistsAsync("system_page_schemas", cancellationToken);

            if (ownsTransaction)
            {
                await migrationDb.Ado.CommitTranAsync();
            }

            return migrated;
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

    private static async Task<IReadOnlyList<LegacyPageSchemaRow>> ReadLegacyRowsAsync(
        SqliteSchemaExecutor schema,
        CancellationToken cancellationToken)
    {
        var table = await schema.ExecuteDataTableAsync(
            "SELECT Id, TenantId, AppCode, PageCode, PageName, PageType, ModelCode, PermissionCode, VersionNo, Status, SchemaJson, IsDeleted FROM system_page_schemas WHERE IsDeleted = 0 AND Status = 'Published' ORDER BY CreatedTime, Id;",
            cancellationToken);
        return table.Rows.Cast<global::System.Data.DataRow>()
            .Select(row => new LegacyPageSchemaRow(
                ReadRequiredString(row, "Id"),
                ReadRequiredString(row, "TenantId"),
                ReadRequiredString(row, "AppCode"),
                ReadRequiredString(row, "PageCode"),
                ReadRequiredString(row, "PageName"),
                ReadOptionalString(row, "PageType") ?? "custom",
                ReadOptionalString(row, "ModelCode"),
                ReadOptionalString(row, "PermissionCode"),
                ReadInt32(row, "VersionNo", 1),
                ReadOptionalString(row, "Status") ?? "Published",
                ReadOptionalString(row, "SchemaJson") ?? "{}")
            { IsDeleted = ReadInt32(row, "IsDeleted", 0) != 0 })
            .ToArray();
    }

    private static string ReadRequiredString(global::System.Data.DataRow row, string column) =>
        ReadOptionalString(row, column)
        ?? throw new ValidationException($"Legacy page schema column '{column}' is null.", ErrorCodes.DesignerSchemaInvalid);

    private static string? ReadOptionalString(global::System.Data.DataRow row, string column) =>
        row[column] is DBNull ? null : Convert.ToString(row[column], global::System.Globalization.CultureInfo.InvariantCulture);

    private static int ReadInt32(global::System.Data.DataRow row, string column, int fallback) =>
        row[column] is DBNull || !int.TryParse(Convert.ToString(row[column], global::System.Globalization.CultureInfo.InvariantCulture), out var value)
            ? fallback
            : value;

    private async Task MigrateRowAsync(
        ISqlSugarClient migrationDb,
        LegacyPageSchemaRow legacyRow,
        ApplicationDevelopmentPageEntity page,
        ApplicationDesignerDocumentEntity document,
        CancellationToken cancellationToken)
    {
        var normalizedDocumentJson = documentMigrationService.NormalizeLegacyDocument(legacyRow.SchemaJson, page);
        normalizedDocumentJson = ApplicationDesignerCanonicalJson.NormalizeObject(normalizedDocumentJson);
        var sourceHash = ApplicationDesignerCanonicalJson.ComputeHash(legacyRow.SchemaJson);
        var revision = await GetNextRevisionNumberAsync(migrationDb, document, cancellationToken);
        var documentNode = JsonNode.Parse(normalizedDocumentJson)?.AsObject()
            ?? throw new ValidationException("Migrated DesignerDocument must be a JSON object.", ErrorCodes.DesignerSchemaInvalid);
        documentNode["revision"] = revision;
        normalizedDocumentJson = ApplicationDesignerCanonicalJson.NormalizeObject(documentNode.ToJsonString());
        var documentHash = ApplicationDesignerCanonicalJson.ComputeDocumentHash(normalizedDocumentJson);
        var revisionEntity = new ApplicationDesignerRevisionEntity
        {
            TenantId = page.TenantId,
            AppCode = page.AppCode,
            DocumentId = document.Id,
            RevisionNumber = revision,
            DocumentJson = normalizedDocumentJson,
            DocumentHash = documentHash,
            SourceHash = sourceHash,
            TargetHash = documentHash,
            MigrationRevision = MigrationRevision,
            CompilerRevision = RuntimeCapabilityContract.CompilerRevision,
            ChangeSetJson = "{\"type\":\"legacy-page-schema-migration\"}",
            DiagnosticsJson = "[]",
            CreatedBy = "application-migration",
            CreatedTime = DateTime.UtcNow
        };

        document.DocumentJson = normalizedDocumentJson;
        document.DocumentHash = documentHash;
        document.SourceHash = sourceHash;
        document.TargetHash = documentHash;
        document.MigrationRevision = MigrationRevision;
        document.Status = "Published";
        document.CurrentRevisionId = revisionEntity.Id;
        document.DiagnosticsJson = "[]";
        document.UpdatedBy = "application-migration";
        document.UpdatedTime = DateTime.UtcNow;
        await migrationDb.Updateable(document).ExecuteCommandAsync(cancellationToken);
        await migrationDb.Insertable(revisionEntity).ExecuteCommandAsync(cancellationToken);

        var modelCode = ResolveModelCode(legacyRow.SchemaJson, legacyRow.ModelCode);
        var compiledArtifactJson = schemaCompiler.CompileSchema(
            page.PageCode,
            page.PageName,
            page.PageType,
            ReadPageParameters(page.PageParametersJson),
            normalizedDocumentJson,
            page.PermissionConfigJson,
            modelCode: modelCode);
        var artifact = await artifactPublisher.PublishAsync(
            migrationDb,
            new ApplicationDataCenterWorkspace(page.TenantId, page.AppCode, "application-migration"),
            document,
            compiledArtifactJson,
            page.PublishedArtifactId,
            cancellationToken);

        page.PublishedArtifactId = artifact.Id;
        page.Status = "Published";
        page.UpdatedBy = "application-migration";
        page.UpdatedTime = DateTime.UtcNow;
        await migrationDb.Updateable(page)
            .UpdateColumns(item => new
            {
                item.PublishedArtifactId,
                item.Status,
                item.UpdatedBy,
                item.UpdatedTime
            })
            .ExecuteCommandAsync(cancellationToken);
    }

    private async Task<int> GetNextRevisionNumberAsync(
        ISqlSugarClient migrationDb,
        ApplicationDesignerDocumentEntity document,
        CancellationToken cancellationToken)
    {
        var latest = (await migrationDb.Queryable<ApplicationDesignerRevisionEntity>()
                .Where(item => !item.IsDeleted && item.DocumentId == document.Id)
                .OrderBy(item => item.RevisionNumber, OrderByType.Desc)
                .Take(1)
                .ToListAsync(cancellationToken))
            .FirstOrDefault();
        return (latest?.RevisionNumber ?? 0) + 1;
    }

    private static string? ResolveModelCode(string schemaJson, string? legacyModelCode)
    {
        if (!string.IsNullOrWhiteSpace(legacyModelCode))
        {
            return legacyModelCode;
        }

        try
        {
            using var schema = JsonDocument.Parse(schemaJson);
            return schema.RootElement.TryGetProperty("runtimeContext", out var runtimeContext) &&
                   runtimeContext.TryGetProperty("modelCode", out var modelCode) &&
                   modelCode.ValueKind == JsonValueKind.String
                ? modelCode.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static List<ApplicationDevelopmentPageParameterDto> ReadPageParameters(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<ApplicationDevelopmentPageParameterDto>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
