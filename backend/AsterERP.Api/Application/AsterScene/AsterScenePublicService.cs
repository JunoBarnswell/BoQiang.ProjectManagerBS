using System.Text.Json;
using System.Text.Json.Nodes;
using AsterERP.Api.Modules.AsterScene;
using AsterERP.Contracts.AsterScene;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.AsterScene;

public sealed class AsterScenePublicService(
    ISqlSugarClient db,
    AsterSceneWorkspaceContext workspaceContext)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public async Task<GridPageResult<AsterScenePublicWorkDto>> ExploreAsync(
        AsterSceneGridQuery query,
        CancellationToken cancellationToken = default)
    {
        var dbQuery = db.Queryable<AsterScenePublicWorkEntity>()
            .Where(item => !item.IsDeleted && item.Status == "Published" && item.Visibility != "Private");
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dbQuery = dbQuery.Where(item =>
                item.Title.Contains(keyword) ||
                item.Slug.Contains(keyword) ||
                item.CreatorHandle.Contains(keyword) ||
                (item.Summary != null && item.Summary.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(query.CreatorHandle))
        {
            var handle = query.CreatorHandle.Trim().ToLowerInvariant();
            dbQuery = dbQuery.Where(item => item.CreatorHandle == handle);
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery
            .OrderBy(item => item.PublishedAt, OrderByType.Desc)
            .ToPageListAsync(Math.Max(query.PageIndex, 1), Math.Clamp(query.PageSize, 1, 100), total, cancellationToken);
        return new GridPageResult<AsterScenePublicWorkDto>
        {
            Total = total.Value,
            Items = rows.Select(MapWork).ToList()
        };
    }

    public async Task<GridPageResult<AsterScenePublicWorkDto>> GetTemplatesAsync(
        AsterSceneGridQuery query,
        CancellationToken cancellationToken = default)
    {
        query.Status = "Published";
        var dbQuery = db.Queryable<AsterScenePublicWorkEntity>()
            .Where(item => !item.IsDeleted && item.Status == "Published" && item.Visibility == "Template");
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dbQuery = dbQuery.Where(item => item.Title.Contains(keyword) || item.Slug.Contains(keyword));
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery
            .OrderBy(item => item.PublishedAt, OrderByType.Desc)
            .ToPageListAsync(Math.Max(query.PageIndex, 1), Math.Clamp(query.PageSize, 1, 100), total, cancellationToken);
        return new GridPageResult<AsterScenePublicWorkDto>
        {
            Total = total.Value,
            Items = rows.Select(MapWork).ToList()
        };
    }

    public async Task<AsterScenePublicWorkDto> GetWorkBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var work = await RequirePublishedWorkBySlugAsync(slug, cancellationToken);
        return MapWork(work);
    }

    public async Task<AsterSceneCreatorProfileDto> GetCreatorProfileAsync(
        string handle,
        CancellationToken cancellationToken = default)
    {
        var normalizedHandle = NormalizeHandle(handle);
        var profile = await db.Queryable<AsterSceneCreatorProfileEntity>()
            .FirstAsync(item => !item.IsDeleted && item.Handle == normalizedHandle && item.Status == "Active", cancellationToken);
        if (profile is null)
        {
            throw new NotFoundException("AsterScene creator profile was not found.", ErrorCodes.AsterScenePublicWorkNotFound);
        }

        return new AsterSceneCreatorProfileDto
        {
            Handle = profile.Handle,
            DisplayName = profile.DisplayName,
            Bio = profile.Bio,
            AvatarUrl = profile.AvatarUrl,
            WorksCount = profile.WorksCount,
            FollowersCount = profile.FollowersCount
        };
    }

    public async Task<AsterSceneRuntimeEventResponse> RecordRuntimeEventAsync(
        AsterSceneRuntimeEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var publishCode = NormalizeBoundedRequired(request.PublishCode, "publishCode is required.", 80);
        var eventType = NormalizeEventType(request.EventType);
        var sceneId = NormalizeBoundedOptional(request.SceneId, 128);
        var hotspotId = NormalizeBoundedOptional(request.HotspotId, 128);
        var clientEventId = NormalizeBoundedRequired(request.ClientEventId, "clientEventId is required.", 128);
        var publish = await db.Queryable<AsterScenePublishVersionEntity>()
            .FirstAsync(item => !item.IsDeleted && item.PublishCode == publishCode && item.Status == "Active", cancellationToken);
        if (publish is null || publish.Visibility == "Private")
        {
            throw new NotFoundException("AsterScene public runtime was not found.", ErrorCodes.AsterScenePublishNotFound);
        }

        var work = await db.Queryable<AsterScenePublicWorkEntity>()
            .FirstAsync(item =>
                !item.IsDeleted &&
                item.PublishVersionId == publish.Id &&
                item.Status == "Published" &&
                item.Visibility != "Private",
                cancellationToken);
        if (work is null)
        {
            throw new NotFoundException("AsterScene public work was not found.", ErrorCodes.AsterScenePublicWorkNotFound);
        }

        var idempotencyKey = BuildRuntimeEventIdempotencyKey(publishCode, clientEventId);
        var metadataJson = BuildRuntimeEventMetadataJson(publishCode, eventType, sceneId, hotspotId, clientEventId);
        var existing = await db.Queryable<AsterSceneUsageLedgerEntity>()
            .FirstAsync(item => !item.IsDeleted && item.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            EnsureRuntimeEventReplayMatches(existing.MetadataJson, metadataJson);
            return MapRuntimeEvent(existing, publishCode, eventType, sceneId, hotspotId, clientEventId);
        }

        var occurredAt = DateTime.UtcNow;
        var ledger = new AsterSceneUsageLedgerEntity
        {
            TenantId = publish.TenantId,
            AppCode = publish.AppCode,
            OwnerUserId = work.CreatorUserId,
            ProjectId = publish.ProjectId,
            UsageType = "runtime-event",
            Quantity = 1,
            Unit = "event",
            Direction = "Debit",
            SourceType = "publish",
            SourceId = publish.PublishCode,
            IdempotencyKey = idempotencyKey,
            OccurredAt = occurredAt,
            MetadataJson = metadataJson,
            CreatedBy = "runtime"
        };
        await db.Insertable(ledger).ExecuteCommandAsync(cancellationToken);
        return MapRuntimeEvent(ledger, publishCode, eventType, sceneId, hotspotId, clientEventId);
    }

    public async Task<AsterScenePublicWorkDto> ReactAsync(
        string workId,
        string reactionType,
        AsterSceneReactionRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var normalizedReaction = NormalizeReaction(reactionType);
        var clientMutationId = NormalizeRequired(request.ClientMutationId, "clientMutationId is required.");
        var work = await RequirePublishedWorkByIdAsync(workId, cancellationToken);
        var replay = await db.Queryable<AsterSceneCommunityReactionEntity>()
            .FirstAsync(item =>
                item.WorkId == work.Id &&
                item.UserId == workspace.UserId &&
                item.ClientMutationId == clientMutationId,
                cancellationToken);
        if (replay is not null)
        {
            return MapWork(work);
        }

        var existing = await db.Queryable<AsterSceneCommunityReactionEntity>()
            .FirstAsync(item =>
                !item.IsDeleted &&
                item.WorkId == work.Id &&
                item.UserId == workspace.UserId &&
                item.ReactionType == normalizedReaction,
                cancellationToken);

        await ExecuteInTransactionAsync(async () =>
        {
            if (existing is not null)
            {
                existing.IsDeleted = true;
                existing.DeletedBy = workspace.UserId;
                existing.DeletedTime = DateTime.UtcNow;
                existing.ClientMutationId = clientMutationId;
                await db.Updateable(existing).ExecuteCommandAsync(cancellationToken);
                if (normalizedReaction == "Like")
                {
                    work.LikeCount = Math.Max(0, work.LikeCount - 1);
                }
                else
                {
                    work.FavoriteCount = Math.Max(0, work.FavoriteCount - 1);
                }
            }
            else
            {
                await db.Insertable(new AsterSceneCommunityReactionEntity
                {
                    TenantId = work.TenantId,
                    AppCode = work.AppCode,
                    WorkId = work.Id,
                    UserId = workspace.UserId,
                    ReactionType = normalizedReaction,
                    ClientMutationId = clientMutationId,
                    CreatedBy = workspace.UserId
                }).ExecuteCommandAsync(cancellationToken);

                if (normalizedReaction == "Like")
                {
                    work.LikeCount += 1;
                }
                else
                {
                    work.FavoriteCount += 1;
                }
            }

            work.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(work).ExecuteCommandAsync(cancellationToken);
        });

        return MapWork(work);
    }

    public async Task<AsterSceneRemixResponse> RemixAsync(
        string workId,
        AsterSceneRemixRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var clientMutationId = NormalizeRequired(request.ClientMutationId, "clientMutationId is required.");
        var existingRemix = await db.Queryable<AsterSceneRemixEntity>()
            .FirstAsync(item =>
                !item.IsDeleted &&
                item.UserId == workspace.UserId &&
                item.ClientMutationId == clientMutationId,
                cancellationToken);
        if (existingRemix is not null)
        {
            var existingProject = await db.Queryable<AsterSceneProjectEntity>()
                .FirstAsync(item => !item.IsDeleted && item.Id == existingRemix.TargetProjectId, cancellationToken);
            return new AsterSceneRemixResponse
            {
                SourceWorkId = existingRemix.SourceWorkId,
                Project = AsterSceneDocumentService.MapProject(existingProject)
            };
        }

        var work = await RequirePublishedWorkByIdAsync(workId, cancellationToken);
        var publish = await db.Queryable<AsterScenePublishVersionEntity>()
            .FirstAsync(item => !item.IsDeleted && item.Id == work.PublishVersionId, cancellationToken);
        if (publish is null)
        {
            throw new NotFoundException("Published RuntimeManifest was not found.", ErrorCodes.AsterScenePublishNotFound);
        }

        var manifest = JsonSerializer.Deserialize<AsterSceneRuntimeManifestDto>(publish.RuntimeManifestJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (manifest is null)
        {
            throw new ValidationException("RuntimeManifest is invalid.", ErrorCodes.AsterScenePublishNotFound);
        }

        EnsureRemixAllowed(manifest.Document, manifest.CapabilityPolicy);

        var projectName = NormalizeRequired(request.ProjectName, "Project name is required.");
        var project = new AsterSceneProjectEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            ProjectCode = await GenerateProjectCodeAsync(cancellationToken),
            ProjectName = projectName,
            Description = $"Remix from {work.Title}",
            Visibility = "Private",
            Status = "Draft",
            CurrentRevision = 1,
            CreateClientMutationId = clientMutationId,
            CreatedBy = workspace.UserId
        };
        var remixAssets = BuildRemixAssetCopies(
            manifest.Document,
            manifest.AssetVariants,
            workspace,
            project.Id,
            clientMutationId);
        var documentJson = CreateRemixDocumentJson(manifest.Document, project.Id, projectName, remixAssets.AssetIdMap, remixAssets.AssetRuntimeUrlMap);
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
            SaveSource = "Remix",
            SavedBy = workspace.UserId,
            ClientMutationId = clientMutationId,
            CreatedBy = workspace.UserId
        };
        var remix = new AsterSceneRemixEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            SourceWorkId = work.Id,
            SourceProjectId = work.ProjectId,
            TargetProjectId = project.Id,
            UserId = workspace.UserId,
            ClientMutationId = clientMutationId,
            CreatedBy = workspace.UserId
        };

        await ExecuteInTransactionAsync(async () =>
        {
            await db.Insertable(project).ExecuteCommandAsync(cancellationToken);
            if (remixAssets.Assets.Count > 0)
            {
                await db.Insertable(remixAssets.Assets).ExecuteCommandAsync(cancellationToken);
                await db.Insertable(remixAssets.Versions).ExecuteCommandAsync(cancellationToken);
                if (remixAssets.UsageLedgers.Count > 0)
                {
                    await db.Insertable(remixAssets.UsageLedgers).ExecuteCommandAsync(cancellationToken);
                }
            }

            await db.Insertable(document).ExecuteCommandAsync(cancellationToken);
            await db.Insertable(remix).ExecuteCommandAsync(cancellationToken);
            work.RemixCount += 1;
            work.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(work).ExecuteCommandAsync(cancellationToken);
        });

        return new AsterSceneRemixResponse
        {
            SourceWorkId = work.Id,
            Project = AsterSceneDocumentService.MapProject(project)
        };
    }

    public static AsterScenePublicWorkDto MapWork(AsterScenePublicWorkEntity entity)
    {
        return new AsterScenePublicWorkDto
        {
            Id = entity.Id,
            Slug = entity.Slug,
            Title = entity.Title,
            Summary = entity.Summary,
            CoverAssetId = entity.CoverAssetId,
            CreatorHandle = entity.CreatorHandle,
            Visibility = entity.Visibility,
            Status = entity.Status,
            ViewCount = entity.ViewCount,
            LikeCount = entity.LikeCount,
            FavoriteCount = entity.FavoriteCount,
            RemixCount = entity.RemixCount,
            PublishedAt = entity.PublishedAt,
            PublishCode = entity.PublishCode
        };
    }

    private static AsterSceneRuntimeEventResponse MapRuntimeEvent(
        AsterSceneUsageLedgerEntity entity,
        string publishCode,
        string eventType,
        string? sceneId,
        string? hotspotId,
        string clientEventId)
    {
        return new AsterSceneRuntimeEventResponse
        {
            LedgerId = entity.Id,
            PublishCode = publishCode,
            EventType = eventType,
            SceneId = sceneId,
            HotspotId = hotspotId,
            ClientEventId = clientEventId,
            OccurredAt = entity.OccurredAt
        };
    }

    private static string BuildRuntimeEventIdempotencyKey(string publishCode, string clientEventId)
    {
        return $"runtime-event:{publishCode}:{clientEventId}";
    }

    private static string BuildRuntimeEventMetadataJson(
        string publishCode,
        string eventType,
        string? sceneId,
        string? hotspotId,
        string clientEventId)
    {
        return JsonSerializer.Serialize(new
        {
            publishCode,
            eventType,
            sceneId,
            hotspotId,
            clientEventId
        }, JsonOptions);
    }

    private static void EnsureRuntimeEventReplayMatches(string? existingMetadataJson, string requestedMetadataJson)
    {
        if (!string.Equals(existingMetadataJson, requestedMetadataJson, StringComparison.Ordinal))
        {
            throw new ValidationException("clientEventId was already used for a different runtime event.", ErrorCodes.AsterSceneUsageLedgerConflict);
        }
    }

    private static string NormalizeEventType(string value)
    {
        var normalized = NormalizeBoundedRequired(value, "eventType is required.", 64).ToLowerInvariant();
        foreach (var ch in normalized)
        {
            if (!char.IsAsciiLetterOrDigit(ch) && ch is not '-' and not '_' and not '.')
            {
                throw new ValidationException("eventType contains unsupported characters.", ErrorCodes.ParameterInvalid);
            }
        }

        return normalized;
    }

    private static string NormalizeBoundedRequired(string? value, string message, int maxLength)
    {
        var normalized = NormalizeRequired(value, message);
        if (normalized.Length > maxLength)
        {
            throw new ValidationException(message, ErrorCodes.ParameterInvalid);
        }

        return normalized;
    }

    private static string? NormalizeBoundedOptional(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized is not null && normalized.Length > maxLength)
        {
            throw new ValidationException("Runtime event field is too long.", ErrorCodes.ParameterInvalid);
        }

        return normalized;
    }

    private async Task<AsterScenePublicWorkEntity> RequirePublishedWorkBySlugAsync(
        string slug,
        CancellationToken cancellationToken)
    {
        var normalizedSlug = NormalizeSlug(slug);
        var work = await db.Queryable<AsterScenePublicWorkEntity>()
            .FirstAsync(item =>
                !item.IsDeleted &&
                item.Slug == normalizedSlug &&
                item.Status == "Published" &&
                item.Visibility != "Private",
                cancellationToken);
        if (work is null)
        {
            throw new NotFoundException("AsterScene public work was not found.", ErrorCodes.AsterScenePublicWorkNotFound);
        }

        return work;
    }

    private async Task<AsterScenePublicWorkEntity> RequirePublishedWorkByIdAsync(
        string workId,
        CancellationToken cancellationToken)
    {
        var work = await db.Queryable<AsterScenePublicWorkEntity>()
            .FirstAsync(item =>
                !item.IsDeleted &&
                item.Id == workId &&
                item.Status == "Published" &&
                item.Visibility != "Private",
                cancellationToken);
        if (work is null)
        {
            throw new NotFoundException("AsterScene public work was not found.", ErrorCodes.AsterScenePublicWorkNotFound);
        }

        return work;
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

    private static (
        Dictionary<string, string> AssetIdMap,
        Dictionary<string, string> AssetRuntimeUrlMap,
        List<AsterSceneAssetEntity> Assets,
        List<AsterSceneAssetVersionEntity> Versions,
        List<AsterSceneUsageLedgerEntity> UsageLedgers) BuildRemixAssetCopies(
            JsonElement sourceDocument,
            JsonElement assetVariants,
            AsterSceneWorkspace workspace,
            string projectId,
            string clientMutationId)
    {
        var idMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var runtimeUrlMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var assets = new List<AsterSceneAssetEntity>();
        var versions = new List<AsterSceneAssetVersionEntity>();
        var usageLedgers = new List<AsterSceneUsageLedgerEntity>();

        if (!sourceDocument.TryGetProperty("assets", out var documentAssets) ||
            documentAssets.ValueKind != JsonValueKind.Array)
        {
            return (idMap, runtimeUrlMap, assets, versions, usageLedgers);
        }

        foreach (var documentAsset in documentAssets.EnumerateArray())
        {
            if (!documentAsset.TryGetProperty("id", out var idElement) ||
                idElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var sourceAssetId = idElement.GetString();
            if (string.IsNullOrWhiteSpace(sourceAssetId) || idMap.ContainsKey(sourceAssetId))
            {
                continue;
            }

            var sourceVariants = GetManifestVariants(assetVariants, sourceAssetId);
            var primaryVariant = sourceVariants.FirstOrDefault();
            var runtimeUrl = ReadString(primaryVariant, "runtimeUrl") ??
                             ReadString(primaryVariant, "sourceUrl") ??
                             ReadString(documentAsset, "url") ??
                             string.Empty;
            if (string.IsNullOrWhiteSpace(runtimeUrl))
            {
                continue;
            }

            var targetAssetId = Guid.NewGuid().ToString("N");
            idMap[sourceAssetId] = targetAssetId;
            runtimeUrlMap[sourceAssetId] = runtimeUrl;

            var contentType = ReadString(primaryVariant, "contentType");
            var sizeBytes = ReadNullableInt64(primaryVariant, "sizeBytes");
            var checksum = ReadString(primaryVariant, "checksum");
            var assetType = ReadString(documentAsset, "kind") ?? InferAssetType(runtimeUrl, contentType);
            assets.Add(new AsterSceneAssetEntity
            {
                Id = targetAssetId,
                TenantId = workspace.TenantId,
                AppCode = workspace.AppCode,
                ProjectId = projectId,
                OwnerUserId = workspace.UserId,
                AssetCode = $"RMX{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
                AssetType = assetType,
                FileName = ResolveFileName(runtimeUrl, sourceAssetId),
                Status = "Ready",
                CurrentVersion = Math.Max(1, sourceVariants.Select(item => ReadNullableInt32(item, "version") ?? 1).DefaultIfEmpty(1).Max()),
                SourceUrl = ReadString(primaryVariant, "sourceUrl") ?? runtimeUrl,
                RuntimeUrl = runtimeUrl,
                ContentType = contentType,
                SizeBytes = sizeBytes,
                Checksum = checksum,
                MetadataJson = JsonSerializer.Serialize(new
                {
                    source = "remix",
                    sourceAssetId,
                    copiedFromPublishedManifest = true
                }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                ClientMutationId = $"{clientMutationId}:{sourceAssetId}",
                CreatedBy = workspace.UserId
            });

            var variantsToCopy = sourceVariants.Count == 0
                ? [primaryVariant]
                : sourceVariants;
            foreach (var variant in variantsToCopy)
            {
                var variantUrl = ReadString(variant, "runtimeUrl") ??
                                 ReadString(variant, "sourceUrl") ??
                                 runtimeUrl;
                versions.Add(new AsterSceneAssetVersionEntity
                {
                    TenantId = workspace.TenantId,
                    AppCode = workspace.AppCode,
                    ProjectId = projectId,
                    OwnerUserId = workspace.UserId,
                    AssetId = targetAssetId,
                    Version = ReadNullableInt32(variant, "version") ?? 1,
                    VariantType = ReadString(variant, "variantType") ?? "original",
                    Url = variantUrl,
                    ContentType = ReadString(variant, "contentType") ?? contentType,
                    SizeBytes = ReadNullableInt64(variant, "sizeBytes") ?? sizeBytes,
                    Checksum = ReadString(variant, "checksum") ?? checksum,
                    MetadataJson = JsonSerializer.Serialize(new
                    {
                        source = "remix",
                        sourceAssetId,
                        sourceAssetVersionId = ReadString(variant, "assetVersionId")
                    }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                    Status = "Ready",
                    CreatedBy = workspace.UserId
                });
            }

            var copiedBytes = sizeBytes.GetValueOrDefault();
            if (copiedBytes > 0)
            {
                usageLedgers.Add(new AsterSceneUsageLedgerEntity
                {
                    TenantId = workspace.TenantId,
                    AppCode = workspace.AppCode,
                    OwnerUserId = workspace.UserId,
                    ProjectId = projectId,
                    UsageType = "storage",
                    Quantity = copiedBytes,
                    Unit = "bytes",
                    Direction = "Debit",
                    SourceType = "remix-asset",
                    SourceId = targetAssetId,
                    IdempotencyKey = $"remix-asset-storage:{targetAssetId}",
                    CreatedBy = workspace.UserId
                });
            }
        }

        return (idMap, runtimeUrlMap, assets, versions, usageLedgers);
    }

    private static string CreateRemixDocumentJson(
        JsonElement sourceDocument,
        string projectId,
        string projectName,
        IReadOnlyDictionary<string, string> assetIdMap,
        IReadOnlyDictionary<string, string> assetRuntimeUrlMap)
    {
        var node = JsonNode.Parse(sourceDocument.GetRawText())?.AsObject() ??
                   throw new ValidationException("Source SceneDocument cannot be remixed.", ErrorCodes.AsterSceneDocumentInvalid);
        RewriteAssetReferences(node, assetIdMap, assetRuntimeUrlMap);

        var meta = node["meta"]?.AsObject() ?? new JsonObject();
        meta.Remove("schemaVersion");
        meta["product"] = "AsterScene";
        meta["title"] = projectName;
        meta["updatedAt"] = DateTime.UtcNow;
        node["meta"] = meta;

        var identity = node["identity"]?.AsObject() ?? new JsonObject();
        identity["projectId"] = projectId;
        identity["documentId"] = $"doc_{Guid.NewGuid():N}";
        node["identity"] = identity;

        node["revision"] = 1;
        var publish = node["publish"]?.AsObject() ?? new JsonObject();
        publish["visibility"] = "Private";
        publish["slug"] = null;
        node["publish"] = publish;
        return node.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static void RewriteAssetReferences(
        JsonObject document,
        IReadOnlyDictionary<string, string> assetIdMap,
        IReadOnlyDictionary<string, string> assetRuntimeUrlMap)
    {
        if (assetIdMap.Count == 0)
        {
            return;
        }

        if (document["assets"] is JsonArray assets)
        {
            foreach (var assetNode in assets.OfType<JsonObject>())
            {
                var sourceId = assetNode["id"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(sourceId) && assetIdMap.TryGetValue(sourceId, out var targetId))
                {
                    assetNode["id"] = targetId;
                    if (assetRuntimeUrlMap.TryGetValue(sourceId, out var runtimeUrl))
                    {
                        assetNode["url"] = runtimeUrl;
                    }
                }
            }
        }

        RewriteNestedAssetReferences(document, assetIdMap);
    }

    private static void RewriteNestedAssetReferences(JsonNode? node, IReadOnlyDictionary<string, string> assetIdMap)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj.ToList())
                {
                    if (property.Value is JsonValue value &&
                        IsAssetIdProperty(property.Key) &&
                        value.TryGetValue<string>(out var sourceId) &&
                        assetIdMap.TryGetValue(sourceId, out var targetId))
                    {
                        obj[property.Key] = targetId;
                    }
                    else
                    {
                        RewriteNestedAssetReferences(property.Value, assetIdMap);
                    }
                }

                break;
            case JsonArray array:
                for (var index = 0; index < array.Count; index++)
                {
                    var item = array[index];
                    if (item is JsonValue value &&
                        value.TryGetValue<string>(out var sourceId) &&
                        assetIdMap.TryGetValue(sourceId, out var targetId))
                    {
                        array[index] = targetId;
                    }
                    else
                    {
                        RewriteNestedAssetReferences(item, assetIdMap);
                    }
                }

                break;
        }
    }

    private static bool IsAssetIdProperty(string propertyName)
    {
        return propertyName.Equals("assetId", StringComparison.OrdinalIgnoreCase) ||
               propertyName.EndsWith("AssetId", StringComparison.OrdinalIgnoreCase);
    }

    private static List<JsonElement> GetManifestVariants(JsonElement assetVariants, string sourceAssetId)
    {
        if (assetVariants.ValueKind != JsonValueKind.Object ||
            !assetVariants.TryGetProperty(sourceAssetId, out var variants) ||
            variants.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return variants.EnumerateArray().Select(item => item.Clone()).ToList();
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }

    private static long? ReadNullableInt64(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.TryGetInt64(out var value) ? value : null;
    }

    private static int? ReadNullableInt32(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.TryGetInt32(out var value) ? value : null;
    }

    private static string InferAssetType(string runtimeUrl, string? contentType)
    {
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return "image";
            }

            if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            {
                return "video";
            }

            if (contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
            {
                return "audio";
            }
        }

        var path = runtimeUrl.Split('?')[0].ToLowerInvariant();
        if (path.EndsWith(".glb", StringComparison.Ordinal) || path.EndsWith(".gltf", StringComparison.Ordinal))
        {
            return "model";
        }

        return "document";
    }

    private static string ResolveFileName(string runtimeUrl, string fallback)
    {
        var path = runtimeUrl.Split('?')[0].TrimEnd('/');
        var slashIndex = path.LastIndexOf('/');
        var fileName = slashIndex >= 0 ? path[(slashIndex + 1)..] : path;
        fileName = Uri.UnescapeDataString(fileName);
        return string.IsNullOrWhiteSpace(fileName) ? fallback : fileName;
    }

    private static string NormalizeReaction(string value)
    {
        if (value.Equals("favorite", StringComparison.OrdinalIgnoreCase))
        {
            return "Favorite";
        }

        if (value.Equals("like", StringComparison.OrdinalIgnoreCase))
        {
            return "Like";
        }

        throw new ValidationException("Unsupported reaction type.", ErrorCodes.ParameterInvalid);
    }

    private static void EnsureRemixAllowed(JsonElement document, JsonElement capabilityPolicy)
    {
        if (capabilityPolicy.ValueKind == JsonValueKind.Object &&
            capabilityPolicy.TryGetProperty("allowRemix", out var allowRemix) &&
            allowRemix.ValueKind == JsonValueKind.False)
        {
            throw new ValidationException("This work does not allow Remix.", ErrorCodes.PermissionDenied);
        }

        if (document.TryGetProperty("publish", out var publish) &&
            publish.TryGetProperty("license", out var licenseElement) &&
            licenseElement.ValueKind == JsonValueKind.String)
        {
            var license = licenseElement.GetString() ?? string.Empty;
            if (license.Contains("no-remix", StringComparison.OrdinalIgnoreCase) ||
                license.Contains("all-rights-reserved", StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException("This work license does not allow Remix.", ErrorCodes.PermissionDenied);
            }
        }
    }

    private static string NormalizeHandle(string value)
    {
        var normalized = NormalizeRequired(value, "Creator handle is required.").ToLowerInvariant();
        if (normalized.Length > 64)
        {
            throw new ValidationException("Creator handle is too long.", ErrorCodes.ParameterInvalid);
        }

        return normalized;
    }

    private static string NormalizeSlug(string value)
    {
        var normalized = NormalizeRequired(value, "Work slug is required.").ToLowerInvariant();
        if (normalized.Length > 120)
        {
            throw new ValidationException("Work slug is too long.", ErrorCodes.ParameterInvalid);
        }

        return normalized;
    }

    private static string NormalizeRequired(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException(message, ErrorCodes.ParameterInvalid);
        }

        return value.Trim();
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
}
