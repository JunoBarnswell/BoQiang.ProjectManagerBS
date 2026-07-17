using System.Globalization;
using System.Text.Json.Nodes;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ApplicationDevelopmentCenter;
using AsterERP.Api.Modules.System.Menus;
using AsterERP.Contracts.ApplicationDevelopmentCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationDevelopmentCenter;

public sealed class ApplicationDesignerArtifactRollbackService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ApplicationDataCenterWorkspaceResolver workspaceResolver,
    ApplicationDevelopmentSchemaValidator schemaValidator)
{
    public async Task<ApplicationDesignerArtifactRollbackResponse> RollbackAsync(
        string pageId,
        ApplicationDesignerArtifactRollbackRequest request,
        string? traceId,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var normalizedPageId = NormalizeRequired(pageId, "Rollback pageId is required", 128);
        var operationId = NormalizeRequired(request.OperationId, "Rollback operationId is required", 128);
        var artifactId = NormalizeRequired(request.ArtifactId, "Rollback artifactId is required", 128);
        var artifactHash = NormalizeRequired(request.ArtifactHash, "Rollback artifactHash is required", 256);
        var reason = NormalizeRequired(request.Reason, "Rollback reason is required", 1024);
        var existing = await FindAuditAsync(db, operationId, cancellationToken);
        if (existing is not null)
        {
            ValidateReplayIdentity(existing, workspace, normalizedPageId, artifactId, artifactHash);
            if (existing.Status == "RollbackSucceeded")
            {
                return ToResponse(existing);
            }

            throw new ValidationException(
                existing.FailureMessage ?? "The rollback operation has already failed",
                existing.FailureCode is { Length: > 0 } code ? int.Parse(code, CultureInfo.InvariantCulture) : ErrorCodes.DesignerSchemaInvalid);
        }

        try
        {
            var page = await RequirePageAsync(db, workspace, normalizedPageId, cancellationToken);
            var document = await RequireDocumentAsync(db, workspace, page.Id, cancellationToken);
            var target = await RequireTargetArtifactAsync(db, workspace, document, artifactId, artifactHash, cancellationToken);
            ValidateTargetArtifact(target, document);

            var currentArtifact = string.IsNullOrWhiteSpace(document.PublishedArtifactId)
                ? null
                : (await db.Queryable<ApplicationDesignerRuntimeArtifactEntity>()
                    .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode &&
                                   item.DocumentId == document.Id && item.Id == document.PublishedArtifactId &&
                                   item.Status == "Published" && !item.IsDeleted)
                    .Take(1)
                    .ToListAsync(cancellationToken)).FirstOrDefault()
                  ?? throw new ValidationException("Published artifact pointer is invalid; rollback requires manual recovery.", ErrorCodes.DesignerSchemaInvalid);
            if (currentArtifact is not null) ValidateTargetArtifact(currentArtifact, document);
            var ownsTransaction = db.Ado.Transaction is null;
            if (ownsTransaction)
            {
                await db.Ado.BeginTranAsync();
            }

            try
            {
                await ActivateDocumentAsync(db, workspace, document, target, cancellationToken);
                await ActivatePageAsync(db, workspace, page, target.Id, cancellationToken);
                await SyncPublishedMenusAsync(db, workspace, page, target.Id, cancellationToken);
                var audit = BuildSuccessAudit(workspace, page, document, target, target.Id, currentArtifact, operationId, reason, traceId);
                await db.Insertable(audit).ExecuteCommandAsync(cancellationToken);
                if (ownsTransaction)
                {
                    await db.Ado.CommitTranAsync();
                }

                var response = ToResponse(audit);
                return response;
            }
            catch
            {
                if (ownsTransaction && db.Ado.Transaction is not null)
                {
                    await db.Ado.RollbackTranAsync();
                }

                throw;
            }
        }
        catch (Exception exception)
        {
            await WriteFailureAuditAsync(
                db,
                workspace,
                normalizedPageId,
                request,
                traceId,
                exception,
                CancellationToken.None);
            throw;
        }
    }

    private static async Task<ApplicationDesignerPublishRecordEntity?> FindAuditAsync(
        ISqlSugarClient db,
        string operationId,
        CancellationToken cancellationToken) =>
        (await db.Queryable<ApplicationDesignerPublishRecordEntity>()
            .Where(item => item.OperationId == operationId && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
        .FirstOrDefault();

    private static void ValidateReplayIdentity(
        ApplicationDesignerPublishRecordEntity existing,
        ApplicationDataCenterWorkspace workspace,
        string pageId,
        string artifactId,
        string artifactHash)
    {
        if (!string.Equals(existing.TenantId, workspace.TenantId, StringComparison.Ordinal) ||
            !string.Equals(existing.AppCode, workspace.AppCode, StringComparison.Ordinal) ||
            !string.Equals(existing.PageId, pageId, StringComparison.Ordinal) ||
            !string.Equals(existing.ArtifactId, artifactId, StringComparison.Ordinal) ||
            !string.Equals(existing.ArtifactHash, artifactHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException(
                "The rollback operationId is already used for a different tenant, application, page, or artifact",
                ErrorCodes.DesignerSchemaInvalid);
        }
    }

    private static async Task<ApplicationDevelopmentPageEntity> RequirePageAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        string pageId,
        CancellationToken cancellationToken) =>
        (await db.Queryable<ApplicationDevelopmentPageEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode &&
                           item.Id == pageId && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
        .FirstOrDefault()
        ?? throw new ValidationException("The application development page was not found", ErrorCodes.ApplicationDataCenterObjectNotFound);

    private static async Task<ApplicationDesignerDocumentEntity> RequireDocumentAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        string pageId,
        CancellationToken cancellationToken)
    {
        var documents = await db.Queryable<ApplicationDesignerDocumentEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode &&
                           item.PageId == pageId && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        if (documents.Count > 1)
        {
            throw new ValidationException(
                $"Multiple current DesignerDocuments exist for page '{pageId}'.",
                ErrorCodes.DesignerSchemaInvalid);
        }

        return documents.SingleOrDefault()
            ?? throw new ValidationException("The DesignerDocument was not found", ErrorCodes.ApplicationDataCenterObjectNotFound);
    }

    private static async Task<ApplicationDesignerRuntimeArtifactEntity> RequireTargetArtifactAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        ApplicationDesignerDocumentEntity document,
        string artifactId,
        string artifactHash,
        CancellationToken cancellationToken) =>
        (await db.Queryable<ApplicationDesignerRuntimeArtifactEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode &&
                           item.DocumentId == document.Id && item.Id == artifactId &&
                           item.ArtifactHash == artifactHash && item.Status == "Published" && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
        .FirstOrDefault()
        ?? throw new ValidationException("The target Runtime Artifact is not a published artifact in the current document", ErrorCodes.DesignerSchemaInvalid);

    private void ValidateTargetArtifact(
        ApplicationDesignerRuntimeArtifactEntity artifact,
        ApplicationDesignerDocumentEntity document)
    {
        var canonicalArtifact = ApplicationDesignerCanonicalJson.NormalizeObject(artifact.ArtifactJson);
        var artifactObject = JsonNode.Parse(canonicalArtifact) as JsonObject
            ?? throw new ValidationException("The target Runtime Artifact is not a JSON object", ErrorCodes.DesignerSchemaInvalid);
        var artifactHash = artifactObject["artifactHash"]?.GetValue<string>();
        var signature = artifactObject["signature"]?.GetValue<string>();
        var compilerRevision = artifactObject["compilerVersion"]?.GetValue<string>();
        var revision = artifactObject["revision"]?.GetValue<int>() ?? 0;
        var documentNode = artifactObject["document"]
            ?? throw new ValidationException("The target Runtime Artifact document is missing", ErrorCodes.DesignerSchemaInvalid);
        var canonicalDocument = ApplicationDesignerCanonicalJson.NormalizeObject(documentNode.ToJsonString(ApplicationDataCenterJson.Options));
        var validatedDocument = schemaValidator.ValidateRuntimeArtifact(canonicalDocument);
        var computedArtifactHash = ApplicationDesignerCanonicalJson.ComputeRuntimeArtifactHash(validatedDocument);
        if (string.IsNullOrWhiteSpace(artifactHash) || !string.Equals(artifactHash, computedArtifactHash, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(artifactHash, artifact.ArtifactHash, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(artifactObject["document"]?["documentId"]?.GetValue<string>(), document.PageId, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(compilerRevision) || revision < 1)
        {
            throw new ValidationException("The target Runtime Artifact hash or metadata is invalid", ErrorCodes.DesignerSchemaInvalid);
        }

        var manifestTypes = artifactObject["manifestTypes"] as JsonArray
            ?? throw new ValidationException("The target Runtime Artifact manifest is missing", ErrorCodes.DesignerSchemaInvalid);
        var declarations = artifactObject["manifest"] as JsonArray
            ?? throw new ValidationException("The target Runtime Artifact manifest declarations are missing", ErrorCodes.DesignerSchemaInvalid);
        ValidateManifestDeclarations(manifestTypes, declarations);
        var manifestSignatureJson = ApplicationDesignerCanonicalJson.NormalizeRuntimeObject(new JsonObject
        {
            ["types"] = manifestTypes.DeepClone(),
            ["declarations"] = declarations.DeepClone()
        }.ToJsonString());
        var manifestHash = ApplicationDesignerCanonicalJson.ComputeHash(manifestSignatureJson);
        var manifestJson = ApplicationDesignerCanonicalJson.NormalizeRuntimeObject(new JsonObject
        {
            ["types"] = manifestTypes.DeepClone(),
            ["declarations"] = declarations.DeepClone()
        }.ToJsonString());
        if (!string.Equals(manifestHash, artifact.ManifestHash, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(manifestJson, ApplicationDesignerCanonicalJson.NormalizeRuntimeObject(artifact.ManifestJson), StringComparison.Ordinal) ||
            !string.Equals(artifactObject["migrationRevision"]?.GetValue<string>(), artifact.MigrationRevision, StringComparison.Ordinal) ||
            !string.Equals(ApplicationDesignerCanonicalJson.ComputeSignature(
                document.PageId,
                artifactHash,
                manifestHash,
                compilerRevision,
                revision.ToString(CultureInfo.InvariantCulture)), signature, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("The target Runtime Artifact signature or manifest is invalid", ErrorCodes.DesignerSchemaInvalid);
        }
    }

    private static async Task ActivateDocumentAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        ApplicationDesignerDocumentEntity document,
        ApplicationDesignerRuntimeArtifactEntity artifact,
        CancellationToken cancellationToken)
    {
        document.PublishedArtifactId = artifact.Id;
        document.Status = "Published";
        document.UpdatedBy = workspace.UserId;
        document.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(document).UpdateColumns(item => new
        {
            item.PublishedArtifactId,
            item.Status,
            item.UpdatedBy,
            item.UpdatedTime
        }).ExecuteCommandAsync(cancellationToken);
    }

    private static async Task ActivatePageAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        ApplicationDevelopmentPageEntity page,
        string artifactId,
        CancellationToken cancellationToken)
    {
        page.PublishedArtifactId = artifactId;
        page.Status = "Published";
        page.UpdatedBy = workspace.UserId;
        page.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(page).UpdateColumns(item => new
        {
            item.PublishedArtifactId,
            item.Status,
            item.UpdatedBy,
            item.UpdatedTime
        }).ExecuteCommandAsync(cancellationToken);
    }

    private static async Task SyncPublishedMenusAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        ApplicationDevelopmentPageEntity page,
        string artifactId,
        CancellationToken cancellationToken)
    {
        var menus = await db.Queryable<SystemMenuEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode &&
                           item.PageCode == page.PageCode && item.ScopeType == "ApplicationRuntime" && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var menu in menus)
        {
            if (menu.ArtifactId == artifactId)
            {
                continue;
            }

            menu.ArtifactId = artifactId;
            menu.UpdatedBy = workspace.UserId;
            menu.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(menu).UpdateColumns(item => new
            {
                item.ArtifactId,
                item.UpdatedBy,
                item.UpdatedTime
            }).ExecuteCommandAsync(cancellationToken);
        }
    }

    private static ApplicationDesignerPublishRecordEntity BuildSuccessAudit(
        ApplicationDataCenterWorkspace workspace,
        ApplicationDevelopmentPageEntity page,
        ApplicationDesignerDocumentEntity document,
        ApplicationDesignerRuntimeArtifactEntity target,
        string publishedArtifactId,
        ApplicationDesignerRuntimeArtifactEntity? previousArtifact,
        string operationId,
        string reason,
        string? traceId) => new()
    {
        TenantId = workspace.TenantId,
        AppCode = workspace.AppCode,
        DocumentId = document.Id,
        PageId = page.Id,
        RevisionId = target.RevisionId,
        ArtifactId = target.Id,
        RevisionNumber = target.RevisionNumber,
        DocumentHash = target.TargetHash,
        ArtifactHash = target.ArtifactHash,
        CompilerRevision = target.CompilerRevision,
        ManifestHash = target.ManifestHash,
        ManifestJson = target.ManifestJson,
        MigrationRevision = target.MigrationRevision,
        SourceArtifactId = previousArtifact?.Id,
        SourceArtifactHash = previousArtifact?.ArtifactHash,
        SourceHash = target.SourceHash,
        TargetHash = target.TargetHash,
        Status = "RollbackSucceeded",
        DiagnosticsJson = "[]",
        BackupLocation = previousArtifact?.Id,
        RollbackRevisionId = target.RevisionId,
        PublishedTime = DateTime.UtcNow,
        OperationType = "Rollback",
        OperationId = operationId,
        TargetArtifactId = target.Id,
        TargetArtifactHash = target.ArtifactHash,
        OperatorUserId = workspace.UserId,
        TraceId = traceId,
        RollbackReason = reason,
        CreatedBy = workspace.UserId,
        CreatedTime = DateTime.UtcNow,
        IsDeleted = false
    };

    private static async Task WriteFailureAuditAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        string pageId,
        ApplicationDesignerArtifactRollbackRequest request,
        string? traceId,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var operationId = request.OperationId?.Trim();
        if (string.IsNullOrWhiteSpace(operationId) || await FindAuditAsync(db, operationId, cancellationToken) is not null)
        {
            return;
        }

        var failure = new ApplicationDesignerPublishRecordEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            DocumentId = pageId,
            PageId = pageId,
            ArtifactId = request.ArtifactId?.Trim() ?? string.Empty,
            ArtifactHash = request.ArtifactHash?.Trim() ?? string.Empty,
            TargetArtifactId = request.ArtifactId?.Trim(),
            TargetArtifactHash = request.ArtifactHash?.Trim(),
            Status = "RollbackFailed",
            OperationType = "Rollback",
            OperationId = operationId,
            OperatorUserId = workspace.UserId,
            TraceId = traceId,
            RollbackReason = request.Reason?.Trim(),
            FailureCode = exception is ValidationException validation
                ? validation.Code.ToString(CultureInfo.InvariantCulture)
                : "rollback.failed",
            FailureMessage = exception.Message,
            DiagnosticsJson = "[]",
            CreatedBy = workspace.UserId,
            CreatedTime = DateTime.UtcNow,
            IsDeleted = false
        };
        var auditDb = db.Ado.Transaction is null ? db : db.CopyNew();
        var ownsTransaction = auditDb.Ado.Transaction is null;
        try
        {
            if (ownsTransaction)
            {
                await auditDb.Ado.BeginTranAsync();
            }

            await auditDb.Insertable(failure).ExecuteCommandAsync(cancellationToken);
            if (ownsTransaction)
            {
                await auditDb.Ado.CommitTranAsync();
            }
        }
        catch
        {
            if (ownsTransaction && auditDb.Ado.Transaction is not null)
            {
                await auditDb.Ado.RollbackTranAsync();
            }

            throw;
        }
        finally
        {
            if (!ReferenceEquals(auditDb, db) && auditDb is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    private static ApplicationDesignerArtifactRollbackResponse ToResponse(ApplicationDesignerPublishRecordEntity audit) => new()
    {
        AuditId = audit.Id,
        DocumentId = audit.DocumentId,
        PageId = audit.PageId ?? string.Empty,
        ArtifactId = audit.ArtifactId,
        ArtifactHash = audit.ArtifactHash,
        PreviousArtifactId = audit.SourceArtifactId ?? string.Empty,
        PublishedArtifactId = audit.ArtifactId,
        Status = "Succeeded"
    };

    private static string NormalizeRequired(string? value, string message, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maxLength)
        {
            throw new ValidationException(message, ErrorCodes.DesignerSchemaInvalid);
        }

        return normalized;
    }

    private static void ValidateManifestDeclarations(JsonArray manifestTypes, JsonArray declarations)
    {
        if (manifestTypes.Count == 0)
        {
            throw new ValidationException("Runtime Artifact manifest is empty", ErrorCodes.DesignerSchemaInvalid);
        }

        var types = manifestTypes.Select(item => item?.GetValue<string>()).Where(item => !string.IsNullOrWhiteSpace(item)).ToArray();
        if (types.Length != manifestTypes.Count || types.Distinct(StringComparer.Ordinal).Count() != types.Length)
        {
            throw new ValidationException("Runtime Artifact manifestTypes is invalid", ErrorCodes.DesignerSchemaInvalid);
        }

        if (declarations.Count == 0)
        {
            throw new ValidationException("Runtime Artifact manifest declarations are empty", ErrorCodes.DesignerSchemaInvalid);
        }

        var declarationTypes = declarations.OfType<JsonObject>().Select(item => item["type"]?.GetValue<string>()).Where(item => !string.IsNullOrWhiteSpace(item)).ToArray();
        if (declarationTypes.Length != declarations.Count || declarationTypes.Distinct(StringComparer.Ordinal).Count() != declarationTypes.Length ||
            !types.OrderBy(item => item, StringComparer.Ordinal).SequenceEqual(declarationTypes.OrderBy(item => item, StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new ValidationException("Runtime Artifact manifest declarations do not match manifestTypes", ErrorCodes.DesignerSchemaInvalid);
        }
    }
}
