using System.Text.Json.Nodes;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Application.ApplicationDevelopmentCenter.Runtime;
using AsterERP.Api.Modules.ApplicationDevelopmentCenter;
using AsterERP.Contracts.ApplicationDesigner;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationDevelopmentCenter;

public sealed class ApplicationDesignerArtifactPublisher(
    ApplicationDevelopmentSchemaValidator schemaValidator,
    ApplicationDesignerDocumentStore? documentStore = null)
{
    private readonly ApplicationDesignerDocumentStore documentBoundary =
        documentStore ?? new ApplicationDesignerDocumentStore(schemaValidator);

    public async Task<ApplicationDesignerRuntimeArtifactEntity> PublishAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        ApplicationDesignerDocumentEntity document,
        string compiledArtifactJson,
        string? previousArtifactId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (db.Ado.Transaction is not null)
        {
            return await PublishCoreAsync(db, workspace, document, compiledArtifactJson, previousArtifactId, cancellationToken);
        }

        await db.Ado.BeginTranAsync();
        try
        {
            var result = await PublishCoreAsync(db, workspace, document, compiledArtifactJson, previousArtifactId, cancellationToken);
            await db.Ado.CommitTranAsync();
            return result;
        }
        catch
        {
            if (db.Ado.Transaction is not null)
            {
                await db.Ado.RollbackTranAsync();
            }

            throw;
        }
    }

    private async Task<ApplicationDesignerRuntimeArtifactEntity> PublishCoreAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        ApplicationDesignerDocumentEntity document,
        string compiledArtifactJson,
        string? previousArtifactId,
        CancellationToken cancellationToken)
    {
        var persistedDocument = await documentBoundary.RequireCurrentAsync(
            db,
            workspace,
            document.PageId,
            cancellationToken);
        if (!string.Equals(persistedDocument.Id, document.Id, StringComparison.Ordinal))
        {
            throw new ValidationException(
                "The publish request does not reference the current DesignerDocument",
                ErrorCodes.ApplicationDevelopmentPageRevisionConflict);
        }

        document = persistedDocument;
        var canonicalArtifact = ApplicationDesignerCanonicalJson.NormalizeObject(compiledArtifactJson);
        var artifact = JsonNode.Parse(canonicalArtifact) as JsonObject
            ?? throw new ValidationException("Runtime Artifact must be a JSON object", ErrorCodes.DesignerSchemaInvalid);
        RuntimeArtifactContractValidator.Validate(artifact);
        var artifactHash = artifact["artifactHash"]?.GetValue<string>();
        var signature = artifact["signature"]?.GetValue<string>();
        var compilerRevision = artifact["compilerVersion"]?.GetValue<string>();
        var revision = artifact["revision"]?.GetValue<int>() ?? 0;
        if (string.IsNullOrWhiteSpace(artifactHash) || string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(compilerRevision) || revision < 1)
        {
            throw new ValidationException("Runtime Artifact metadata is incomplete", ErrorCodes.DesignerSchemaInvalid);
        }
        if (!string.Equals(compilerRevision, RuntimeCapabilityContract.CompilerRevision, StringComparison.Ordinal))
        {
            throw new ValidationException($"Unsupported Runtime Artifact compiler revision: {compilerRevision}", ErrorCodes.DesignerSchemaInvalid);
        }

        var runtimeDocument = artifact["document"]
            ?? throw new ValidationException("Runtime Artifact document is missing", ErrorCodes.DesignerSchemaInvalid);
        var canonicalDocumentJson = ApplicationDesignerCanonicalJson.NormalizeObject(runtimeDocument.ToJsonString(ApplicationDataCenterJson.Options));
        var validatedDocument = schemaValidator.ValidateRuntimeArtifact(canonicalDocumentJson);
        var computedArtifactHash = ApplicationDesignerCanonicalJson.ComputeRuntimeArtifactHash(validatedDocument);
        if (!string.Equals(artifactHash, computedArtifactHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Runtime Artifact hash does not match its canonical content", ErrorCodes.DesignerSchemaInvalid);
        }

        var manifest = artifact["manifestTypes"] as JsonArray
            ?? throw new ValidationException("Runtime Artifact manifest is missing", ErrorCodes.DesignerSchemaInvalid);
        var manifestDeclarations = artifact["manifest"] as JsonArray
            ?? throw new ValidationException("Runtime Artifact manifest declarations are missing", ErrorCodes.DesignerSchemaInvalid);
        ValidateManifestDeclarations(manifest, manifestDeclarations);
        var manifestJson = ApplicationDesignerCanonicalJson.NormalizeRuntimeObject(new JsonObject
        {
            ["types"] = manifest.DeepClone(),
            ["declarations"] = manifestDeclarations.DeepClone()
        }.ToJsonString());
        var manifestHash = ApplicationDesignerCanonicalJson.ComputeHash(manifestJson);
        var expectedSignature = ApplicationDesignerCanonicalJson.ComputeSignature(
            artifact["document"]?["documentId"]?.GetValue<string>() ?? document.Id,
            artifactHash,
            manifestHash,
            compilerRevision,
            revision.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
        if (!string.Equals(signature, expectedSignature, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Runtime Artifact signature does not match its canonical metadata", ErrorCodes.DesignerSchemaInvalid);
        }

        var operationId = BuildPublishOperationId(workspace, document.Id, artifactHash);
        var existing = (await db.Queryable<ApplicationDesignerRuntimeArtifactEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode &&
                           item.DocumentId == document.Id && item.ArtifactHash == artifactHash && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        if (existing is not null)
        {
            EnsurePublishedArtifact(existing);
            var replayRecord = await FindPublishRecordAsync(db, workspace, document.Id, operationId, cancellationToken);
            if (replayRecord is not null)
            {
                ValidatePublishReplay(replayRecord, workspace, document.Id, artifactHash, existing.Id, operationId);
            }

            var replayAudit = replayRecord ?? await InsertPublishRecordWithRaceRecoveryAsync(
                db,
                BuildPublishRecord(
                    workspace,
                    document,
                    existing,
                    operationId,
                    string.IsNullOrWhiteSpace(previousArtifactId) ? existing.SourceArtifactId : previousArtifactId),
                cancellationToken);
            ValidatePublishReplay(replayAudit, workspace, document.Id, artifactHash, existing.Id, operationId);
            await UpdatePublishedDocumentAsync(db, workspace, document, existing.Id, cancellationToken);
            return existing;
        }

        var effectivePreviousArtifactId = string.IsNullOrWhiteSpace(previousArtifactId)
            ? document.PublishedArtifactId
            : previousArtifactId;
        ApplicationDesignerRuntimeArtifactEntity? sourceArtifact = null;
        if (!string.IsNullOrWhiteSpace(effectivePreviousArtifactId))
        {
            sourceArtifact = (await db.Queryable<ApplicationDesignerRuntimeArtifactEntity>()
                    .Where(item => item.TenantId == workspace.TenantId &&
                                   item.AppCode == workspace.AppCode &&
                                   item.Id == effectivePreviousArtifactId &&
                                   !item.IsDeleted)
                    .Take(1)
                    .ToListAsync(cancellationToken))
                .FirstOrDefault()
                ?? throw new ValidationException(
                    $"Source runtime artifact '{effectivePreviousArtifactId}' does not exist in the current workspace",
                    ErrorCodes.DesignerSchemaInvalid);
        }

        var entity = new ApplicationDesignerRuntimeArtifactEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            DocumentId = document.Id,
            RevisionId = document.CurrentRevisionId ?? string.Empty,
            ArtifactJson = canonicalArtifact,
            ArtifactHash = artifactHash,
            SourceHash = document.SourceHash,
            TargetHash = document.DocumentHash,
            ManifestHash = manifestHash,
            ManifestJson = manifestJson,
            SignatureHash = signature,
            RevisionNumber = revision,
            CompilerRevision = compilerRevision,
            // The artifact envelope owns the runtime capability revision. The
            // document migration marker is a deployment-history value and may
            // intentionally differ from the runtime contract revision.
            MigrationRevision = RuntimeCapabilityContract.MigrationRevision,
            SourceArtifactId = sourceArtifact?.Id,
            SourceArtifactHash = sourceArtifact?.ArtifactHash,
            SourceArtifactJson = sourceArtifact?.ArtifactJson,
            Status = "Published",
            DiagnosticsJson = "[]",
            PublishedTime = DateTime.UtcNow,
            CreatedBy = workspace.UserId,
            CreatedTime = DateTime.UtcNow,
            IsDeleted = false
        };
        entity = await InsertArtifactWithRaceRecoveryAsync(db, workspace, entity, cancellationToken);
        EnsurePublishedArtifact(entity);
        var record = await InsertPublishRecordWithRaceRecoveryAsync(
            db,
            BuildPublishRecord(workspace, document, entity, operationId, effectivePreviousArtifactId),
            cancellationToken);
        ValidatePublishReplay(record, workspace, document.Id, artifactHash, entity.Id, operationId);
        await UpdatePublishedDocumentAsync(db, workspace, document, entity.Id, cancellationToken);
        return entity;
    }

    private static async Task<ApplicationDesignerRuntimeArtifactEntity> InsertArtifactWithRaceRecoveryAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        ApplicationDesignerRuntimeArtifactEntity entity,
        CancellationToken cancellationToken)
    {
        try
        {
            await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
            return entity;
        }
        catch (Exception exception) when (IsUniqueConstraintViolation(exception))
        {
            var existing = (await db.Queryable<ApplicationDesignerRuntimeArtifactEntity>()
                    .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode &&
                                   item.DocumentId == entity.DocumentId && item.ArtifactHash == entity.ArtifactHash && !item.IsDeleted)
                    .Take(1)
                    .ToListAsync(cancellationToken))
                .FirstOrDefault();
            if (existing is null)
            {
                throw;
            }

            return existing;
        }
    }

    private static async Task<ApplicationDesignerPublishRecordEntity> InsertPublishRecordWithRaceRecoveryAsync(
        ISqlSugarClient db,
        ApplicationDesignerPublishRecordEntity record,
        CancellationToken cancellationToken)
    {
        try
        {
            await db.Insertable(record).ExecuteCommandAsync(cancellationToken);
            return record;
        }
        catch (Exception exception) when (IsUniqueConstraintViolation(exception))
        {
            var existing = await FindPublishRecordAsync(
                db,
                record.TenantId,
                record.AppCode,
                record.DocumentId,
                record.OperationId!,
                cancellationToken);
            if (existing is null)
            {
                throw;
            }

            return existing;
        }
    }

    private static async Task<ApplicationDesignerPublishRecordEntity?> FindPublishRecordAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        string documentId,
        string operationId,
        CancellationToken cancellationToken) =>
        await FindPublishRecordAsync(db, workspace.TenantId, workspace.AppCode, documentId, operationId, cancellationToken);

    private static async Task<ApplicationDesignerPublishRecordEntity?> FindPublishRecordAsync(
        ISqlSugarClient db,
        string tenantId,
        string appCode,
        string documentId,
        string operationId,
        CancellationToken cancellationToken) =>
        (await db.Queryable<ApplicationDesignerPublishRecordEntity>()
            .Where(item => item.TenantId == tenantId && item.AppCode == appCode && item.DocumentId == documentId &&
                           item.OperationId == operationId && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
        .FirstOrDefault();

    private static ApplicationDesignerPublishRecordEntity BuildPublishRecord(
        ApplicationDataCenterWorkspace workspace,
        ApplicationDesignerDocumentEntity document,
        ApplicationDesignerRuntimeArtifactEntity artifact,
        string operationId,
        string? backupLocation) => new()
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            DocumentId = document.Id,
            PageId = document.PageId,
            RevisionId = artifact.RevisionId,
            ArtifactId = artifact.Id,
            RevisionNumber = artifact.RevisionNumber,
            DocumentHash = document.DocumentHash,
            ArtifactHash = artifact.ArtifactHash,
            CompilerRevision = artifact.CompilerRevision,
            ManifestHash = artifact.ManifestHash,
            ManifestJson = artifact.ManifestJson,
            MigrationRevision = artifact.MigrationRevision,
            SourceArtifactId = artifact.SourceArtifactId,
            SourceArtifactHash = artifact.SourceArtifactHash,
            SourceHash = document.SourceHash,
            TargetHash = document.DocumentHash,
            Status = "Published",
            OperationType = "Publish",
            OperationId = operationId,
            TargetArtifactId = artifact.Id,
            TargetArtifactHash = artifact.ArtifactHash,
            DiagnosticsJson = "[]",
            BackupLocation = backupLocation,
            RollbackRevisionId = document.CurrentRevisionId,
            PublishedTime = artifact.PublishedTime,
            CreatedBy = workspace.UserId,
            CreatedTime = DateTime.UtcNow,
            IsDeleted = false
        };

    private static async Task UpdatePublishedDocumentAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        ApplicationDesignerDocumentEntity document,
        string artifactId,
        CancellationToken cancellationToken)
    {
        document.PublishedArtifactId = artifactId;
        document.Status = "Published";
        document.UpdatedBy = workspace.UserId;
        document.UpdatedTime = DateTime.UtcNow;
        var updated = await db.Updateable(document).UpdateColumns(item => new
        {
            item.PublishedArtifactId,
            item.Status,
            item.UpdatedBy,
            item.UpdatedTime
        }).Where(item => item.Id == document.Id &&
                         item.DocumentHash == document.DocumentHash &&
                         item.CurrentRevisionId == document.CurrentRevisionId &&
                         !item.IsDeleted)
        .ExecuteCommandAsync(cancellationToken);
        if (updated != 1)
        {
            throw new ValidationException(
                "The Designer Document changed while its artifact was being published",
                ErrorCodes.ApplicationDevelopmentPageRevisionConflict);
        }
    }

    private static void EnsurePublishedArtifact(ApplicationDesignerRuntimeArtifactEntity artifact)
    {
        if (!string.Equals(artifact.Status, "Published", StringComparison.Ordinal))
        {
            throw new ValidationException(
                $"Runtime Artifact '{artifact.Id}' exists but is not Published; manual reconciliation is required",
                ErrorCodes.DesignerSchemaInvalid);
        }
    }

    private static void ValidatePublishReplay(
        ApplicationDesignerPublishRecordEntity record,
        ApplicationDataCenterWorkspace workspace,
        string documentId,
        string artifactHash,
        string artifactId,
        string operationId)
    {
        if (!string.Equals(record.TenantId, workspace.TenantId, StringComparison.Ordinal) ||
            !string.Equals(record.AppCode, workspace.AppCode, StringComparison.Ordinal) ||
            !string.Equals(record.DocumentId, documentId, StringComparison.Ordinal) ||
            !string.Equals(record.OperationId, operationId, StringComparison.Ordinal) ||
            !string.Equals(record.OperationType, "Publish", StringComparison.Ordinal) ||
            !string.Equals(record.ArtifactId, artifactId, StringComparison.Ordinal) ||
            !string.Equals(record.ArtifactHash, artifactHash, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(record.TargetArtifactId, artifactId, StringComparison.Ordinal) ||
            !string.Equals(record.TargetArtifactHash, artifactHash, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(record.Status, "Published", StringComparison.Ordinal))
        {
            throw new ValidationException(
                "The deterministic publish operation is already associated with a different or incomplete publish record",
                ErrorCodes.DesignerSchemaInvalid);
        }
    }

    private static string BuildPublishOperationId(
        ApplicationDataCenterWorkspace workspace,
        string documentId,
        string artifactHash) =>
        $"publish:{ApplicationDesignerCanonicalJson.ComputeHash(string.Join("\n", [
            "publish",
            workspace.TenantId,
            workspace.AppCode,
            documentId,
            artifactHash.Trim().ToLowerInvariant()
        ]))}";

    private static bool IsUniqueConstraintViolation(Exception exception)
    {
        var message = exception.ToString();
        return message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("duplicate entry", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("unique index", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateManifestDeclarations(JsonArray manifestTypes, JsonArray declarations)
    {
        if (manifestTypes.Count == 0)
        {
            throw new ValidationException("Runtime Artifact manifest is empty", ErrorCodes.DesignerSchemaInvalid);
        }

        var types = manifestTypes
            .Select(item => item?.GetValue<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
        if (types.Length != manifestTypes.Count || types.Distinct(StringComparer.Ordinal).Count() != types.Length)
        {
            throw new ValidationException("Runtime Artifact manifestTypes is invalid", ErrorCodes.DesignerSchemaInvalid);
        }

        if (declarations.Count == 0)
        {
            throw new ValidationException("Runtime Artifact manifest declarations are empty", ErrorCodes.DesignerSchemaInvalid);
        }

        var declarationTypes = declarations
            .OfType<JsonObject>()
            .Select(item => item["type"]?.GetValue<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
        if (declarationTypes.Length != declarations.Count ||
            declarationTypes.Distinct(StringComparer.Ordinal).Count() != declarationTypes.Length ||
            !types.OrderBy(item => item, StringComparer.Ordinal).SequenceEqual(
                declarationTypes.OrderBy(item => item, StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new ValidationException("Runtime Artifact manifest declarations do not match manifestTypes", ErrorCodes.DesignerSchemaInvalid);
        }
    }
}
