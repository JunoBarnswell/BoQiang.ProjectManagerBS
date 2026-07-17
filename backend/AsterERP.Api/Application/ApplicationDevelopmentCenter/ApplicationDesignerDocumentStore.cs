using System.Text.Json.Nodes;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Modules.ApplicationDevelopmentCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationDevelopmentCenter;

public sealed class ApplicationDesignerDocumentStore(ApplicationDevelopmentSchemaValidator schemaValidator)
{
    public async Task<ApplicationDesignerDocumentSaveResult> SaveAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        string pageId,
        string versionId,
        string documentJson,
        string? expectedHash,
        string? sourceHash,
        string changeSetJson,
        CancellationToken cancellationToken = default)
    {
        var canonicalJson = ApplicationDesignerCanonicalJson.NormalizeDocument(documentJson);
        var document = schemaValidator.ValidateDraft(canonicalJson);
        var documentId = ReadDocumentId(document, pageId);
        document["documentId"] = documentId;
        canonicalJson = ApplicationDesignerCanonicalJson.NormalizeDocument(document.ToJsonString());
        schemaValidator.ValidateDraft(canonicalJson);
        var documentHash = ApplicationDesignerCanonicalJson.ComputeDocumentHash(canonicalJson);

        var existing = await FindDocumentAsync(db, workspace, pageId, cancellationToken);
        if (existing is not null && string.Equals(existing.DocumentHash, documentHash, StringComparison.Ordinal))
        {
            return new ApplicationDesignerDocumentSaveResult(existing, false, existing.CurrentRevisionId, existing.DocumentHash);
        }

        if (existing is not null && !string.IsNullOrWhiteSpace(expectedHash) &&
            !string.Equals(existing.DocumentHash, expectedHash, StringComparison.Ordinal))
        {
            throw new ValidationException(
                "Designer Document changed since the editor loaded it",
                ErrorCodes.ApplicationDevelopmentPageRevisionConflict);
        }

        if (existing is not null && string.IsNullOrWhiteSpace(expectedHash))
        {
            throw new ValidationException(
                "Saving an existing Designer Document requires the hash observed by the editor",
                ErrorCodes.ApplicationDevelopmentPageRevisionConflict);
        }

        var ownsTransaction = db.Ado.Transaction is null;
        if (ownsTransaction)
        {
            await db.Ado.BeginTranAsync();
        }

        try
        {
            var observedHash = existing?.DocumentHash;
            var isNewDocument = existing is null;
            existing ??= new ApplicationDesignerDocumentEntity
            {
                TenantId = workspace.TenantId,
                AppCode = workspace.AppCode,
                PageId = pageId,
                VersionId = versionId,
                CreatedBy = workspace.UserId,
                CreatedTime = DateTime.UtcNow,
                IsDeleted = false
            };
            var nextRevision = await db.Queryable<ApplicationDesignerRevisionEntity>()
                .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && item.DocumentId == existing.Id && !item.IsDeleted)
                .MaxAsync(item => (int?)item.RevisionNumber, cancellationToken) ?? 0;
            var revisionNumber = nextRevision + 1;
            document["revision"] = revisionNumber;
            canonicalJson = ApplicationDesignerCanonicalJson.NormalizeDocument(document.ToJsonString());
            schemaValidator.ValidateDraft(canonicalJson);
            documentHash = ApplicationDesignerCanonicalJson.ComputeDocumentHash(canonicalJson);
            existing.VersionId = versionId;
            existing.DocumentJson = canonicalJson;
            existing.DocumentHash = documentHash;
            existing.SourceHash = string.IsNullOrWhiteSpace(sourceHash) ? documentHash : sourceHash.Trim();
            existing.TargetHash = documentHash;
            existing.MigrationRevision = "latest";
            existing.Status = "Draft";
            existing.DiagnosticsJson = "[]";
            existing.UpdatedBy = workspace.UserId;
            existing.UpdatedTime = DateTime.UtcNow;
            var revision = new ApplicationDesignerRevisionEntity
            {
                TenantId = workspace.TenantId,
                AppCode = workspace.AppCode,
                DocumentId = existing.Id,
                RevisionNumber = revisionNumber,
                DocumentJson = canonicalJson,
                DocumentHash = documentHash,
                SourceHash = existing.SourceHash,
                TargetHash = documentHash,
                MigrationRevision = "latest",
                CompilerRevision = string.Empty,
                ManifestHash = string.Empty,
                ManifestJson = "{}",
                SourceArtifactHash = null,
                ChangeSetJson = CanonicalChangeSet(changeSetJson),
                DiagnosticsJson = "[]",
                CreatedBy = workspace.UserId,
                CreatedTime = DateTime.UtcNow,
                IsDeleted = false
            };

            // Allocate the revision id before persistence so a newly inserted
            // document already points at its only valid current revision.
            if (isNewDocument)
            {
                existing.CurrentRevisionId = revision.Id;
                await db.Insertable(existing).ExecuteCommandAsync(cancellationToken);
                await db.Insertable(revision).ExecuteCommandAsync(cancellationToken);
            }
            else
            {
                // Existing documents are updated with the hash observed before the
                // revision was built. This is the database-side compare-and-swap.
                // A stale editor therefore cannot create an orphan current revision
                // or overwrite a newer document.
                await db.Insertable(revision).ExecuteCommandAsync(cancellationToken);
                existing.CurrentRevisionId = revision.Id;
                var updated = await db.Updateable(existing)
                    .UpdateColumns(item => new
                    {
                        item.VersionId,
                        item.DocumentJson,
                        item.DocumentHash,
                        item.SourceHash,
                        item.TargetHash,
                        item.MigrationRevision,
                        item.Status,
                        item.CurrentRevisionId,
                        item.DiagnosticsJson,
                        item.UpdatedBy,
                        item.UpdatedTime
                    })
                    .Where(item => item.Id == existing.Id &&
                                   item.DocumentHash == observedHash &&
                                   !item.IsDeleted)
                    .ExecuteCommandAsync(cancellationToken);
                if (updated != 1)
                {
                    await db.Deleteable<ApplicationDesignerRevisionEntity>()
                        .Where(item => item.Id == revision.Id)
                        .ExecuteCommandAsync(cancellationToken);
                    throw new ValidationException(
                        "Designer Document changed since the editor loaded it",
                        ErrorCodes.ApplicationDevelopmentPageRevisionConflict);
                }
            }


            if (ownsTransaction)
            {
                await db.Ado.CommitTranAsync();
            }

            return new ApplicationDesignerDocumentSaveResult(existing, true, revision.Id, documentHash);
        }
        catch (Exception exception) when (IsUniqueConstraintViolation(exception))
        {
            if (ownsTransaction && db.Ado.Transaction is not null)
            {
                await db.Ado.RollbackTranAsync();
            }

            throw new ValidationException(
                "Designer Document was created or changed concurrently",
                ErrorCodes.ApplicationDevelopmentPageRevisionConflict);
        }
        catch
        {
            if (ownsTransaction)
            {
                await db.Ado.RollbackTranAsync();
            }

            throw;
        }
    }

    public async Task<ApplicationDesignerDocumentEntity?> FindDocumentAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        string pageId,
        CancellationToken cancellationToken = default)
    {
        var documents = await db.Queryable<ApplicationDesignerDocumentEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && item.PageId == pageId && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        if (documents.Count > 1)
        {
            throw new ValidationException(
                $"Multiple current DesignerDocuments exist for page '{pageId}'.",
                ErrorCodes.DesignerSchemaInvalid);
        }

        return documents.SingleOrDefault();
    }

    public async Task<ApplicationDesignerDocumentEntity> RequireCurrentAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        string pageId,
        CancellationToken cancellationToken = default)
    {
        var document = await FindDocumentAsync(db, workspace, pageId, cancellationToken)
            ?? throw new ValidationException(
                $"Designer Document is missing for page '{pageId}'.",
                ErrorCodes.DesignerSchemaInvalid);
        await RequireCurrentRevisionAsync(db, workspace, document, cancellationToken);
        return document;
    }

    public async Task<ApplicationDesignerRevisionEntity> RequireCurrentRevisionAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        ApplicationDesignerDocumentEntity document,
        CancellationToken cancellationToken = default)
    {
        var canonicalJson = ApplicationDesignerCanonicalJson.NormalizeDocument(document.DocumentJson);
        if (!string.Equals(canonicalJson, document.DocumentJson, StringComparison.Ordinal) ||
            !string.Equals(ApplicationDesignerCanonicalJson.ComputeDocumentHash(canonicalJson), document.DocumentHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Designer Document canonical JSON or hash is invalid", ErrorCodes.DesignerSchemaInvalid);
        }

        if (string.IsNullOrWhiteSpace(document.CurrentRevisionId))
        {
            throw new ValidationException("Designer Document current revision is missing", ErrorCodes.DesignerSchemaInvalid);
        }

        var revisions = await db.Queryable<ApplicationDesignerRevisionEntity>()
            .Where(item => item.TenantId == workspace.TenantId &&
                           item.AppCode == workspace.AppCode &&
                           item.Id == document.CurrentRevisionId &&
                           !item.IsDeleted)
            .ToListAsync(cancellationToken);
        if (revisions.Count != 1)
        {
            throw new ValidationException("Designer Document current revision is missing or duplicated", ErrorCodes.DesignerSchemaInvalid);
        }

        var revision = revisions[0];
        var documentNode = JsonNode.Parse(canonicalJson)?.AsObject()
            ?? throw new ValidationException("Designer Document must be a JSON object", ErrorCodes.DesignerSchemaInvalid);
        var documentRevision = documentNode["revision"]?.GetValue<int>() ?? 0;
        if (!string.Equals(revision.DocumentId, document.Id, StringComparison.Ordinal) ||
            !string.Equals(revision.DocumentJson, document.DocumentJson, StringComparison.Ordinal) ||
            !string.Equals(revision.DocumentHash, document.DocumentHash, StringComparison.OrdinalIgnoreCase) ||
            revision.RevisionNumber != documentRevision)
        {
            throw new ValidationException("Designer Document current revision is inconsistent", ErrorCodes.DesignerSchemaInvalid);
        }

        return revision;
    }

    private static string ReadDocumentId(JsonObject document, string fallback) =>
        document["documentId"]?.GetValue<string>()?.Trim() is { Length: > 0 } id ? id : fallback;

    private static string CanonicalChangeSet(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "{}";
        }

        return ApplicationDesignerCanonicalJson.NormalizeObject(value);
    }

    private static bool IsUniqueConstraintViolation(Exception exception) =>
        exception.ToString().Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) ||
        exception.ToString().Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
        exception.ToString().Contains("duplicate entry", StringComparison.OrdinalIgnoreCase);
}
