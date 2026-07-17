using System.Text.Json;
using System.Text.Json.Nodes;
using AsterERP.Api.Modules.AsterScene;
using AsterERP.Contracts.AsterScene;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.AsterScene;

public sealed class AsterSceneDocumentService(
    ISqlSugarClient db,
    AsterSceneWorkspaceContext workspaceContext)
{
    public async Task<GridPageResult<AsterSceneProjectDto>> GetProjectsAsync(
        AsterSceneGridQuery query,
        CancellationToken cancellationToken = default)
    {
        _ = workspaceContext.Resolve();
        var dbQuery = db.Queryable<AsterSceneProjectEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dbQuery = dbQuery.Where(item =>
                item.ProjectName.Contains(keyword) ||
                item.ProjectCode.Contains(keyword) ||
                (item.Description != null && item.Description.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = NormalizeStatus(query.Status);
            dbQuery = dbQuery.Where(item => item.Status == status);
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery
            .OrderBy(item => item.UpdatedTime, OrderByType.Desc)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(Math.Max(query.PageIndex, 1), Math.Clamp(query.PageSize, 1, 100), total, cancellationToken);

        return new GridPageResult<AsterSceneProjectDto>
        {
            Total = total.Value,
            Items = rows.Select(MapProject).ToList()
        };
    }

    public async Task<AsterSceneProjectDto> CreateProjectAsync(
        AsterSceneCreateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var name = NormalizeRequired(request.ProjectName, "Project name is required.");
        var clientMutationId = NormalizeRequired(request.ClientMutationId, "clientMutationId is required.");
        if (!string.IsNullOrWhiteSpace(clientMutationId))
        {
            var existing = await db.Queryable<AsterSceneProjectEntity>()
                .FirstAsync(item =>
                    !item.IsDeleted &&
                    item.OwnerUserId == workspace.UserId &&
                    item.CreateClientMutationId == clientMutationId,
                    cancellationToken);
            if (existing is not null)
            {
                return MapProject(existing);
            }
        }

        var project = new AsterSceneProjectEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            ProjectCode = await GenerateProjectCodeAsync(cancellationToken),
            ProjectName = name,
            Description = NormalizeOptional(request.Description),
            Visibility = NormalizeVisibility(request.Visibility),
            Status = "Draft",
            CreateClientMutationId = clientMutationId,
            CreatedBy = workspace.UserId
        };
        var documentJson = await BuildInitialDocumentJsonAsync(request.TemplateCode, project.Id, project.ProjectName, cancellationToken);
        project.DocumentHash = AsterSceneDocumentKernel.ComputeHash(documentJson);

        var document = new AsterSceneDocumentEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            ProjectId = project.Id,
            Revision = 1,
            DocumentJson = documentJson,
            DocumentHash = project.DocumentHash,
            IsCurrent = true,
            SaveSource = "Create",
            SavedBy = workspace.UserId,
            CreatedBy = workspace.UserId,
            ClientMutationId = clientMutationId
        };

        await ExecuteInTransactionAsync(async () =>
        {
            await db.Insertable(project).ExecuteCommandAsync(cancellationToken);
            await db.Insertable(document).ExecuteCommandAsync(cancellationToken);
        });

        return MapProject(project);
    }

    public async Task<AsterSceneProjectDto> UpdateProjectAsync(
        string projectId,
        AsterSceneUpdateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var name = NormalizeRequired(request.ProjectName, "Project name is required.");
        _ = NormalizeRequired(request.ClientMutationId, "clientMutationId is required.");
        var visibility = NormalizeVisibility(request.Visibility);
        var coverAssetId = NormalizeOptional(request.CoverAssetId);
        var project = await RequireProjectAsync(projectId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(coverAssetId))
        {
            await EnsureCoverAssetBelongsToProjectAsync(project.Id, coverAssetId, cancellationToken);
        }

        var projectChanged = project.ProjectName != name ||
                             project.Description != NormalizeOptional(request.Description) ||
                             project.Visibility != visibility ||
                             project.CoverAssetId != coverAssetId;
        project.ProjectName = name;
        project.Description = NormalizeOptional(request.Description);
        project.Visibility = visibility;
        project.CoverAssetId = coverAssetId;

        var activePublish = string.IsNullOrWhiteSpace(project.CurrentPublishCode)
            ? null
            : await db.Queryable<AsterScenePublishVersionEntity>()
                .FirstAsync(item =>
                    !item.IsDeleted &&
                    item.ProjectId == project.Id &&
                    item.PublishCode == project.CurrentPublishCode &&
                    item.Status == "Active",
                    cancellationToken);
        var publishChanged = activePublish is not null && activePublish.Visibility != visibility;
        if (activePublish is not null)
        {
            activePublish.Visibility = visibility;
            activePublish.UpdatedTime = DateTime.UtcNow;
            activePublish.UpdatedBy = workspace.UserId;
        }

        var publicWork = await db.Queryable<AsterScenePublicWorkEntity>()
            .FirstAsync(item => !item.IsDeleted && item.ProjectId == project.Id, cancellationToken);
        var publicWorkChanged = SyncPublicWorkSnapshot(publicWork, project, visibility);

        if (projectChanged || publishChanged || publicWorkChanged)
        {
            await ExecuteInTransactionAsync(async () =>
            {
                if (projectChanged)
                {
                    project.UpdatedBy = workspace.UserId;
                    project.UpdatedTime = DateTime.UtcNow;
                    await db.Updateable(project).ExecuteCommandAsync(cancellationToken);
                }

                if (publishChanged && activePublish is not null)
                {
                    await db.Updateable(activePublish).ExecuteCommandAsync(cancellationToken);
                }

                if (publicWorkChanged && publicWork is not null)
                {
                    publicWork.UpdatedBy = workspace.UserId;
                    publicWork.UpdatedTime = DateTime.UtcNow;
                    await db.Updateable(publicWork).ExecuteCommandAsync(cancellationToken);
                }
            });
        }

        return MapProject(project);
    }

    public async Task<AsterSceneDocumentDto> GetDocumentAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        var project = await RequireProjectAsync(projectId, cancellationToken);
        var document = await RequireCurrentDocumentAsync(project.Id, cancellationToken);
        return new AsterSceneDocumentDto
        {
            Project = MapProject(project),
            Document = AsterSceneDocumentKernel.ParseJson(document.DocumentJson),
            Revision = document.Revision,
            DocumentHash = document.DocumentHash,
            SavedAt = document.SavedAt
        };
    }

    public async Task<GridPageResult<AsterSceneDocumentVersionDto>> GetDocumentVersionsAsync(
        string projectId,
        AsterSceneGridQuery query,
        CancellationToken cancellationToken = default)
    {
        var project = await RequireProjectAsync(projectId, cancellationToken);
        var dbQuery = db.Queryable<AsterSceneDocumentEntity>()
            .Where(item => !item.IsDeleted && item.ProjectId == project.Id);

        var total = new RefAsync<int>();
        var rows = await dbQuery
            .OrderBy(item => item.Revision, OrderByType.Desc)
            .ToPageListAsync(Math.Max(query.PageIndex, 1), Math.Clamp(query.PageSize, 1, 100), total, cancellationToken);
        return new GridPageResult<AsterSceneDocumentVersionDto>
        {
            Total = total.Value,
            Items = rows.Select(MapDocumentVersion).ToList()
        };
    }

    public async Task<AsterSceneDocumentDto> GetDocumentVersionAsync(
        string projectId,
        int revision,
        CancellationToken cancellationToken = default)
    {
        var project = await RequireProjectAsync(projectId, cancellationToken);
        var document = await RequireDocumentVersionAsync(project.Id, revision, cancellationToken);
        return new AsterSceneDocumentDto
        {
            Project = MapProject(project),
            Document = AsterSceneDocumentKernel.ParseJson(document.DocumentJson),
            Revision = document.Revision,
            DocumentHash = document.DocumentHash,
            SavedAt = document.SavedAt
        };
    }

    public async Task<AsterSceneSaveDocumentResponse> SaveDocumentAsync(
        string projectId,
        AsterSceneSaveDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var clientMutationId = NormalizeRequired(request.ClientMutationId, "clientMutationId is required.");
        var normalizedJson = AsterSceneDocumentKernel.NormalizeJson(request.Document);
        var computedHash = AsterSceneDocumentKernel.ComputeHash(normalizedJson);
        if (!string.Equals(computedHash, request.DocumentHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("documentHash does not match the submitted SceneDocument payload.", ErrorCodes.AsterSceneDocumentInvalid);
        }

        var validation = AsterSceneDocumentKernel.Validate(request.Document);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Errors[0].Message, ErrorCodes.AsterSceneDocumentInvalid);
        }

        var project = await RequireProjectAsync(projectId, cancellationToken);
        var existingMutation = await db.Queryable<AsterSceneDocumentEntity>()
            .FirstAsync(item =>
                !item.IsDeleted &&
                item.ProjectId == project.Id &&
                item.ClientMutationId == clientMutationId,
                cancellationToken);
        if (existingMutation is not null)
        {
            if (!string.Equals(existingMutation.DocumentHash, computedHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException("clientMutationId was already used for a different SceneDocument payload.", ErrorCodes.AsterSceneDocumentConflict);
            }

            return new AsterSceneSaveDocumentResponse
            {
                ProjectId = project.Id,
                Revision = existingMutation.Revision,
                DocumentHash = existingMutation.DocumentHash,
                SavedAt = existingMutation.SavedAt,
                ClientMutationId = clientMutationId
            };
        }

        var currentDocument = await RequireCurrentDocumentAsync(project.Id, cancellationToken);
        if (request.ExpectedRevision != project.CurrentRevision)
        {
            throw new ValidationException("SceneDocument revision conflict. Reload before saving.", ErrorCodes.AsterSceneDocumentConflict);
        }

        var nextRevision = project.CurrentRevision + 1;
        var nextDocument = new AsterSceneDocumentEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            ProjectId = project.Id,
            Revision = nextRevision,
            DocumentJson = normalizedJson,
            DocumentHash = computedHash,
            IsCurrent = true,
            SaveSource = NormalizeSaveSource(request.SaveSource),
            SavedBy = workspace.UserId,
            CreatedBy = workspace.UserId,
            ClientMutationId = clientMutationId
        };

        await ExecuteInTransactionAsync(async () =>
        {
            currentDocument.IsCurrent = false;
            currentDocument.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(currentDocument).ExecuteCommandAsync(cancellationToken);

            project.CurrentRevision = nextRevision;
            project.DocumentHash = computedHash;
            project.Status = project.Status == "Archived" ? "Archived" : "Draft";
            project.UpdatedBy = workspace.UserId;
            project.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(project).ExecuteCommandAsync(cancellationToken);

            await db.Insertable(nextDocument).ExecuteCommandAsync(cancellationToken);
        });

        return new AsterSceneSaveDocumentResponse
        {
            ProjectId = project.Id,
            Revision = nextRevision,
            DocumentHash = computedHash,
            SavedAt = nextDocument.SavedAt,
            ClientMutationId = clientMutationId
        };
    }

    public async Task<AsterSceneSaveDocumentResponse> RestoreDocumentVersionAsync(
        string projectId,
        int revision,
        AsterSceneRestoreDocumentVersionRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var clientMutationId = NormalizeRequired(request.ClientMutationId, "clientMutationId is required.");
        var project = await RequireProjectAsync(projectId, cancellationToken);
        var targetDocument = await RequireDocumentVersionAsync(project.Id, revision, cancellationToken);
        var restoreSaveSource = BuildRestoreSaveSource(revision);
        var existingMutation = await db.Queryable<AsterSceneDocumentEntity>()
            .FirstAsync(item =>
                !item.IsDeleted &&
                item.ProjectId == project.Id &&
                item.ClientMutationId == clientMutationId,
                cancellationToken);
        if (existingMutation is not null)
        {
            if (!string.Equals(existingMutation.SaveSource, restoreSaveSource, StringComparison.Ordinal) ||
                !string.Equals(existingMutation.DocumentHash, targetDocument.DocumentHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException("clientMutationId was already used for a different SceneDocument operation.", ErrorCodes.AsterSceneDocumentConflict);
            }

            return new AsterSceneSaveDocumentResponse
            {
                ProjectId = project.Id,
                Revision = existingMutation.Revision,
                DocumentHash = existingMutation.DocumentHash,
                SavedAt = existingMutation.SavedAt,
                ClientMutationId = clientMutationId
            };
        }

        if (request.ExpectedRevision != project.CurrentRevision)
        {
            throw new ValidationException("SceneDocument revision conflict. Reload before restoring.", ErrorCodes.AsterSceneDocumentConflict);
        }

        var currentDocument = await RequireCurrentDocumentAsync(project.Id, cancellationToken);
        var nextRevision = project.CurrentRevision + 1;
        var restoredDocument = new AsterSceneDocumentEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            ProjectId = project.Id,
            Revision = nextRevision,
            DocumentJson = targetDocument.DocumentJson,
            DocumentHash = targetDocument.DocumentHash,
            IsCurrent = true,
            SaveSource = restoreSaveSource,
            SavedBy = workspace.UserId,
            CreatedBy = workspace.UserId,
            ClientMutationId = clientMutationId
        };

        await ExecuteInTransactionAsync(async () =>
        {
            currentDocument.IsCurrent = false;
            currentDocument.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(currentDocument).ExecuteCommandAsync(cancellationToken);

            project.CurrentRevision = nextRevision;
            project.DocumentHash = restoredDocument.DocumentHash;
            project.Status = project.Status == "Archived" ? "Archived" : "Draft";
            project.UpdatedBy = workspace.UserId;
            project.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(project).ExecuteCommandAsync(cancellationToken);

            await db.Insertable(restoredDocument).ExecuteCommandAsync(cancellationToken);
        });

        return new AsterSceneSaveDocumentResponse
        {
            ProjectId = project.Id,
            Revision = nextRevision,
            DocumentHash = restoredDocument.DocumentHash,
            SavedAt = restoredDocument.SavedAt,
            ClientMutationId = clientMutationId
        };
    }

    public Task<AsterSceneValidationResultDto> ValidateDocumentAsync(JsonElement document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(AsterSceneDocumentKernel.Validate(document));
    }

    public async Task DeleteProjectAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var project = await RequireProjectAsync(projectId, cancellationToken);
        var hasActivePublish = await db.Queryable<AsterScenePublishVersionEntity>()
            .AnyAsync(item => !item.IsDeleted && item.ProjectId == project.Id && item.Status == "Active", cancellationToken);
        if (hasActivePublish)
        {
            throw new ValidationException("Published projects must be archived or rolled back before deletion.", ErrorCodes.StateChangeNotAllowed);
        }

        project.IsDeleted = true;
        project.DeletedBy = workspace.UserId;
        project.DeletedTime = DateTime.UtcNow;
        await db.Updateable(project).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<AsterSceneProjectEntity> RequireProjectAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var project = await db.Queryable<AsterSceneProjectEntity>()
            .FirstAsync(item =>
                !item.IsDeleted &&
                item.Id == projectId &&
                item.TenantId == workspace.TenantId &&
                item.AppCode == workspace.AppCode,
                cancellationToken);
        if (project is null)
        {
            throw new NotFoundException("AsterScene project was not found.", ErrorCodes.AsterSceneProjectNotFound);
        }

        if (project.OwnerUserId != workspace.UserId)
        {
            throw new ValidationException("You can only operate your own AsterScene project.", ErrorCodes.PermissionDenied);
        }

        return project;
    }

    public async Task<AsterSceneDocumentEntity> RequireCurrentDocumentAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        var document = await db.Queryable<AsterSceneDocumentEntity>()
            .FirstAsync(item => !item.IsDeleted && item.ProjectId == projectId && item.IsCurrent, cancellationToken);
        if (document is null)
        {
            throw new NotFoundException("Current SceneDocument was not found.", ErrorCodes.AsterSceneProjectNotFound);
        }

        return document;
    }

    public async Task<AsterSceneDocumentEntity> RequireDocumentVersionAsync(
        string projectId,
        int revision,
        CancellationToken cancellationToken = default)
    {
        var document = await db.Queryable<AsterSceneDocumentEntity>()
            .FirstAsync(item => !item.IsDeleted && item.ProjectId == projectId && item.Revision == revision, cancellationToken);
        if (document is null)
        {
            throw new NotFoundException("SceneDocument revision was not found.", ErrorCodes.AsterSceneProjectNotFound);
        }

        return document;
    }

    public static AsterSceneProjectDto MapProject(AsterSceneProjectEntity entity)
    {
        return new AsterSceneProjectDto
        {
            Id = entity.Id,
            ProjectCode = entity.ProjectCode,
            ProjectName = entity.ProjectName,
            Description = entity.Description,
            Visibility = entity.Visibility,
            Status = entity.Status,
            CurrentRevision = entity.CurrentRevision,
            DocumentHash = entity.DocumentHash,
            CoverAssetId = entity.CoverAssetId,
            CurrentPublishCode = entity.CurrentPublishCode,
            PublishedVersion = entity.PublishedVersion,
            CreatedTime = entity.CreatedTime,
            UpdatedTime = entity.UpdatedTime
        };
    }

    public static AsterSceneDocumentVersionDto MapDocumentVersion(AsterSceneDocumentEntity entity)
    {
        return new AsterSceneDocumentVersionDto
        {
            ProjectId = entity.ProjectId,
            Revision = entity.Revision,
            DocumentHash = entity.DocumentHash,
            IsCurrent = entity.IsCurrent,
            SaveSource = entity.SaveSource,
            SavedBy = entity.SavedBy,
            SavedAt = entity.SavedAt,
            ClientMutationId = entity.ClientMutationId
        };
    }

    private async Task EnsureCoverAssetBelongsToProjectAsync(
        string projectId,
        string coverAssetId,
        CancellationToken cancellationToken)
    {
        var exists = await db.Queryable<AsterSceneAssetEntity>()
            .AnyAsync(item => !item.IsDeleted && item.ProjectId == projectId && item.Id == coverAssetId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("AsterScene cover asset was not found.", ErrorCodes.AsterSceneAssetNotFound);
        }
    }

    private static bool SyncPublicWorkSnapshot(
        AsterScenePublicWorkEntity? publicWork,
        AsterSceneProjectEntity project,
        string visibility)
    {
        if (publicWork is null)
        {
            return false;
        }

        var nextStatus = visibility == "Private" ? "Private" : "Published";
        var changed = publicWork.Title != project.ProjectName ||
                      publicWork.Summary != project.Description ||
                      publicWork.CoverAssetId != project.CoverAssetId ||
                      publicWork.Visibility != visibility ||
                      publicWork.Status != nextStatus;
        publicWork.Title = project.ProjectName;
        publicWork.Summary = project.Description;
        publicWork.CoverAssetId = project.CoverAssetId;
        publicWork.Visibility = visibility;
        publicWork.Status = nextStatus;
        return changed;
    }

    private async Task<string> GenerateProjectCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var code = $"AS{DateTime.UtcNow:yyyyMMdd}{Random.Shared.Next(100000, 999999)}";
            var exists = await db.Queryable<AsterSceneProjectEntity>()
                .AnyAsync(item => !item.IsDeleted && item.ProjectCode == code, cancellationToken);
            if (!exists)
            {
                return code;
            }
        }

        return $"AS{Guid.NewGuid():N}"[..18].ToUpperInvariant();
    }

    private async Task<string> BuildInitialDocumentJsonAsync(
        string? templateCode,
        string projectId,
        string projectName,
        CancellationToken cancellationToken)
    {
        var normalizedTemplateCode = NormalizeOptional(templateCode);
        if (string.IsNullOrWhiteSpace(normalizedTemplateCode))
        {
            return AsterSceneDocumentKernel.CreateDefaultDocumentJson(projectId, projectName);
        }

        var template = await db.Queryable<AsterScenePublicWorkEntity>()
            .FirstAsync(item =>
                !item.IsDeleted &&
                item.Visibility == "Template" &&
                item.Status == "Published" &&
                (item.Slug == normalizedTemplateCode || item.PublishCode == normalizedTemplateCode),
                cancellationToken);
        if (template is null)
        {
            throw new NotFoundException("AsterScene template was not found.", ErrorCodes.AsterScenePublicWorkNotFound);
        }

        var publish = await db.Queryable<AsterScenePublishVersionEntity>()
            .FirstAsync(item => !item.IsDeleted && item.Id == template.PublishVersionId && item.Status == "Active", cancellationToken);
        if (publish is null)
        {
            throw new NotFoundException("AsterScene template manifest was not found.", ErrorCodes.AsterScenePublishNotFound);
        }

        var manifest = JsonSerializer.Deserialize<AsterSceneRuntimeManifestDto>(
            publish.RuntimeManifestJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (manifest is null)
        {
            throw new ValidationException("RuntimeManifest is invalid.", ErrorCodes.AsterScenePublishNotFound);
        }

        var node = JsonNode.Parse(manifest.Document.GetRawText())?.AsObject() ??
                   throw new ValidationException("Template SceneDocument is invalid.", ErrorCodes.AsterSceneDocumentInvalid);
        var meta = node["meta"]?.AsObject() ?? new JsonObject();
        meta.Remove("schemaVersion");
        meta["product"] = "AsterScene";
        meta["title"] = projectName;
        meta["updatedAt"] = DateTime.UtcNow;
        node["meta"] = meta;

        var identity = node["identity"]?.AsObject() ?? new JsonObject();
        identity["projectId"] = projectId;
        identity["documentId"] = $"doc_{Guid.NewGuid():N}";
        if (identity["locale"] is null)
        {
            identity["locale"] = "zh-CN";
        }
        node["identity"] = identity;

        node["revision"] = 1;
        var publishNode = node["publish"]?.AsObject() ?? new JsonObject();
        publishNode["visibility"] = "Private";
        publishNode["slug"] = null;
        node["publish"] = publishNode;
        return node.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private async Task ExecuteInTransactionAsync(Func<Task> action)
    {
        var ownsTransaction = db.Ado.Transaction is null;
        try
        {
            if (ownsTransaction)
            {
                await db.Ado.BeginTranAsync();
            }

            await action();

            if (ownsTransaction)
            {
                await db.Ado.CommitTranAsync();
            }
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

    private static string NormalizeRequired(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException(message, ErrorCodes.ParameterInvalid);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeVisibility(string? value)
    {
        var normalized = NormalizeOptional(value) ?? "Private";
        return normalized.Equals("Public", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("Unlisted", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("Template", StringComparison.OrdinalIgnoreCase)
            ? normalized[..1].ToUpperInvariant() + normalized[1..].ToLowerInvariant()
            : "Private";
    }

    private static string NormalizeStatus(string value)
    {
        var normalized = value.Trim();
        return normalized[..1].ToUpperInvariant() + normalized[1..].ToLowerInvariant();
    }

    private static string NormalizeSaveSource(string? value)
    {
        var normalized = NormalizeOptional(value) ?? "Manual";
        return normalized.Length > 32 ? normalized[..32] : normalized;
    }

    private static string BuildRestoreSaveSource(int revision)
    {
        return $"RestoreRevision:{revision}";
    }
}
