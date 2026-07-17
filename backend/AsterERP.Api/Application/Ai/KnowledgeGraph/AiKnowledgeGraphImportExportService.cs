using AsterERP.Api.Modules.Ai;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.Ai.KnowledgeGraph;

public sealed class AiKnowledgeGraphImportExportService(ISqlSugarClient db, AiWorkspaceContext workspaceContext) : IAiKnowledgeGraphImportExportService
{
    public async Task<AiKnowledgeGraphImportResultDto> ImportAsync(AiKnowledgeGraphImportRequest request, CancellationToken cancellationToken)
    {
        var workspace = workspaceContext.Resolve();
        var result = new AiKnowledgeGraphImportResultDto();
        var nodeKeyToId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var mode = NormalizeMode(request.Mode);

        await db.Ado.BeginTranAsync();
        try
        {
            if (mode == "Rebuild")
            {
                await ClearImportScopeAsync(request, cancellationToken);
            }

            foreach (var nodeRequest in request.Nodes)
            {
                var node = await UpsertNodeAsync(workspace, nodeRequest, result, cancellationToken);
                nodeKeyToId[node.NodeKey] = node.Id;
            }

            var existingNodes = await db.Queryable<AiKnowledgeGraphNodeEntity>()
                .Where(item => !item.IsDeleted)
                .ToListAsync(cancellationToken);
            foreach (var node in existingNodes)
            {
                nodeKeyToId.TryAdd(node.NodeKey, node.Id);
            }

            foreach (var edgeRequest in request.Edges)
            {
                var resolved = ResolveEdgeRequest(edgeRequest, nodeKeyToId);
                await UpsertEdgeAsync(workspace, resolved, result, cancellationToken);
            }

            await db.Ado.CommitTranAsync();
        }
        catch
        {
            await db.Ado.RollbackTranAsync();
            throw;
        }

        return result;
    }

    private async Task ClearImportScopeAsync(AiKnowledgeGraphImportRequest request, CancellationToken cancellationToken)
    {
        var sourceId = string.IsNullOrWhiteSpace(request.SourceId) ? null : request.SourceId.Trim();
        var incomingNodeKeys = request.Nodes
            .Where(item => !string.IsNullOrWhiteSpace(item.NodeKey))
            .Select(item => item.NodeKey.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var nodeQuery = db.Queryable<AiKnowledgeGraphNodeEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(sourceId))
        {
            nodeQuery = nodeQuery.Where(item => item.SourceId == sourceId);
        }
        else if (incomingNodeKeys.Count > 0)
        {
            nodeQuery = nodeQuery.Where(item => incomingNodeKeys.Contains(item.NodeKey));
        }
        else
        {
            return;
        }

        var nodes = await nodeQuery.ToListAsync(cancellationToken);
        if (nodes.Count == 0)
        {
            return;
        }

        var nodeIds = nodes.Select(item => item.Id).ToList();
        var edges = await db.Queryable<AiKnowledgeGraphEdgeEntity>()
            .Where(item => !item.IsDeleted && (nodeIds.Contains(item.FromNodeId) || nodeIds.Contains(item.ToNodeId)))
            .ToListAsync(cancellationToken);
        var edgeIds = edges.Select(item => item.Id).ToList();
        var evidence = await db.Queryable<AiKnowledgeGraphEvidenceEntity>()
            .Where(item => !item.IsDeleted && ((item.NodeId != null && nodeIds.Contains(item.NodeId)) || (item.EdgeId != null && edgeIds.Contains(item.EdgeId))))
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var node in nodes)
        {
            node.IsDeleted = true;
            node.DeletedTime = now;
            node.UpdatedTime = now;
        }

        foreach (var edge in edges)
        {
            edge.IsDeleted = true;
            edge.DeletedTime = now;
            edge.UpdatedTime = now;
        }

        foreach (var item in evidence)
        {
            item.IsDeleted = true;
            item.DeletedTime = now;
            item.UpdatedTime = now;
        }

        await db.Updateable(nodes).ExecuteCommandAsync(cancellationToken);
        if (edges.Count > 0)
        {
            await db.Updateable(edges).ExecuteCommandAsync(cancellationToken);
        }

        if (evidence.Count > 0)
        {
            await db.Updateable(evidence).ExecuteCommandAsync(cancellationToken);
        }
    }

    public async Task<AiKnowledgeGraphExportDto> ExportAsync(AiKnowledgeGraphExportRequest request, CancellationToken cancellationToken)
    {
        var sourceIds = NormalizeList(request.SourceIds);
        var nodeTypes = NormalizeList(request.NodeTypes);
        var relationTypes = NormalizeList(request.RelationTypes);
        var nodeQuery = db.Queryable<AiKnowledgeGraphNodeEntity>().Where(item => !item.IsDeleted);
        if (sourceIds.Count > 0)
        {
            nodeQuery = nodeQuery.Where(item => item.SourceId != null && sourceIds.Contains(item.SourceId));
        }

        if (nodeTypes.Count > 0)
        {
            nodeQuery = nodeQuery.Where(item => nodeTypes.Contains(item.NodeType));
        }

        var nodes = await nodeQuery.OrderBy(item => item.NodeKey).Take(1000).ToListAsync(cancellationToken);
        var nodeIds = nodes.Select(item => item.Id).ToList();
        var edgeQuery = db.Queryable<AiKnowledgeGraphEdgeEntity>()
            .Where(item => !item.IsDeleted && nodeIds.Contains(item.FromNodeId) && nodeIds.Contains(item.ToNodeId));
        if (relationTypes.Count > 0)
        {
            edgeQuery = edgeQuery.Where(item => relationTypes.Contains(item.RelationType));
        }

        var edges = await edgeQuery.OrderBy(item => item.RelationType).Take(2000).ToListAsync(cancellationToken);
        var edgeIds = edges.Select(edge => edge.Id).ToList();
        var evidence = request.IncludeEvidence
            ? await db.Queryable<AiKnowledgeGraphEvidenceEntity>()
                .Where(item => !item.IsDeleted && ((item.NodeId != null && nodeIds.Contains(item.NodeId)) || (item.EdgeId != null && edgeIds.Contains(item.EdgeId))))
                .Take(2000)
                .ToListAsync(cancellationToken)
            : [];

        var sources = await LoadSourcesAsync(nodes.Select(item => item.SourceId), cancellationToken);
        var documents = await LoadDocumentsAsync(nodes.Select(item => item.DocumentId), cancellationToken);
        return new AiKnowledgeGraphExportDto
        {
            Nodes = nodes.Select(item => AiKnowledgeGraphMapper.MapNode(item, sources, documents)).ToList(),
            Edges = edges.Select(AiKnowledgeGraphMapper.MapEdge).ToList(),
            Evidence = evidence.Select(AiKnowledgeGraphMapper.MapEvidence).ToList(),
            ExportedAt = DateTime.UtcNow
        };
    }

    private async Task<AiKnowledgeGraphNodeEntity> UpsertNodeAsync(
        AiWorkspace workspace,
        AiKnowledgeGraphNodeUpsertRequest request,
        AiKnowledgeGraphImportResultDto result,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NodeKey) || string.IsNullOrWhiteSpace(request.NodeType) || string.IsNullOrWhiteSpace(request.DisplayName))
        {
            result.SkippedCount++;
            throw new ValidationException("导入节点缺少 Key、类型或名称", ErrorCodes.ParameterInvalid);
        }

        var nodeType = request.NodeType.Trim();
        var typeExists = await db.Queryable<AiKnowledgeGraphNodeTypeEntity>()
            .AnyAsync(item => !item.IsDeleted && item.Code == nodeType, cancellationToken);
        if (!typeExists)
        {
            throw new ValidationException($"图谱节点类型不存在：{nodeType}", ErrorCodes.ParameterInvalid);
        }

        var nodeKey = request.NodeKey.Trim();
        var existing = await db.Queryable<AiKnowledgeGraphNodeEntity>()
            .FirstAsync(item => !item.IsDeleted && item.NodeKey == nodeKey, cancellationToken);
        var entity = existing ?? new AiKnowledgeGraphNodeEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            NodeKey = nodeKey
        };
        entity.NodeType = nodeType;
        entity.DisplayName = request.DisplayName.Trim();
        entity.Summary = string.IsNullOrWhiteSpace(request.Summary) ? null : request.Summary.Trim();
        entity.SourceId = string.IsNullOrWhiteSpace(request.SourceId) ? null : request.SourceId.Trim();
        entity.DocumentId = string.IsNullOrWhiteSpace(request.DocumentId) ? null : request.DocumentId.Trim();
        entity.MetadataJson = string.IsNullOrWhiteSpace(request.MetadataJson) ? null : request.MetadataJson.Trim();
        entity.UpdatedTime = existing is null ? null : DateTime.UtcNow;

        if (existing is null)
        {
            await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
            result.CreatedCount++;
        }
        else
        {
            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
            result.UpdatedCount++;
        }

        return entity;
    }

    private async Task UpsertEdgeAsync(
        AiWorkspace workspace,
        AiKnowledgeGraphEdgeUpsertRequest request,
        AiKnowledgeGraphImportResultDto result,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FromNodeId) || string.IsNullOrWhiteSpace(request.ToNodeId) || string.IsNullOrWhiteSpace(request.RelationType))
        {
            result.SkippedCount++;
            throw new ValidationException("导入关系缺少起点、终点或关系类型", ErrorCodes.ParameterInvalid);
        }

        var relationType = request.RelationType.Trim();
        var relationExists = await db.Queryable<AiKnowledgeGraphRelationTypeEntity>()
            .AnyAsync(item => !item.IsDeleted && item.Code == relationType, cancellationToken);
        if (!relationExists)
        {
            throw new ValidationException($"图谱关系类型不存在：{relationType}", ErrorCodes.ParameterInvalid);
        }

        var existing = await db.Queryable<AiKnowledgeGraphEdgeEntity>()
            .FirstAsync(item =>
                !item.IsDeleted &&
                item.FromNodeId == request.FromNodeId &&
                item.ToNodeId == request.ToNodeId &&
                item.RelationType == relationType,
                cancellationToken);
        var entity = existing ?? new AiKnowledgeGraphEdgeEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            FromNodeId = request.FromNodeId.Trim(),
            ToNodeId = request.ToNodeId.Trim(),
            RelationType = relationType
        };
        entity.SourceId = string.IsNullOrWhiteSpace(request.SourceId) ? null : request.SourceId.Trim();
        entity.Weight = request.Weight is < 0 or > 1 ? 1 : request.Weight;
        entity.EvidenceText = string.IsNullOrWhiteSpace(request.EvidenceText) ? null : request.EvidenceText.Trim();
        entity.MetadataJson = string.IsNullOrWhiteSpace(request.MetadataJson) ? null : request.MetadataJson.Trim();
        entity.UpdatedTime = existing is null ? null : DateTime.UtcNow;

        if (existing is null)
        {
            await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
            result.CreatedCount++;
        }
        else
        {
            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
            result.UpdatedCount++;
        }

        if (!string.IsNullOrWhiteSpace(entity.EvidenceText))
        {
            var evidenceExists = await db.Queryable<AiKnowledgeGraphEvidenceEntity>()
                .AnyAsync(item => !item.IsDeleted && item.EdgeId == entity.Id && item.EvidenceText == entity.EvidenceText, cancellationToken);
            if (evidenceExists)
            {
                return;
            }

            await db.Insertable(new AiKnowledgeGraphEvidenceEntity
            {
                TenantId = workspace.TenantId,
                AppCode = workspace.AppCode,
                OwnerUserId = workspace.UserId,
                SourceId = entity.SourceId,
                EdgeId = entity.Id,
                EvidenceText = entity.EvidenceText
            }).ExecuteCommandAsync(cancellationToken);
        }
    }

    private static AiKnowledgeGraphEdgeUpsertRequest ResolveEdgeRequest(
        AiKnowledgeGraphEdgeImportRequest request,
        IReadOnlyDictionary<string, string> nodeKeyToId)
    {
        var fromNodeId = string.IsNullOrWhiteSpace(request.FromNodeId)
            ? ResolveNodeKey(request.FromNodeKey, nodeKeyToId, "起点")
            : request.FromNodeId.Trim();
        var toNodeId = string.IsNullOrWhiteSpace(request.ToNodeId)
            ? ResolveNodeKey(request.ToNodeKey, nodeKeyToId, "终点")
            : request.ToNodeId.Trim();
        return new AiKnowledgeGraphEdgeUpsertRequest
        {
            FromNodeId = fromNodeId,
            ToNodeId = toNodeId,
            RelationType = request.RelationType,
            Weight = request.Weight,
            EvidenceText = request.EvidenceText,
            SourceId = request.SourceId,
            MetadataJson = request.MetadataJson
        };
    }

    private static string ResolveNodeKey(string? nodeKey, IReadOnlyDictionary<string, string> nodeKeyToId, string label)
    {
        if (string.IsNullOrWhiteSpace(nodeKey) || !nodeKeyToId.TryGetValue(nodeKey.Trim(), out var nodeId))
        {
            throw new ValidationException($"导入关系无法解析{label}节点 Key", ErrorCodes.ParameterInvalid);
        }

        return nodeId;
    }

    private async Task<IReadOnlyDictionary<string, AiKnowledgeSourceEntity>> LoadSourcesAsync(IEnumerable<string?> sourceIds, CancellationToken cancellationToken)
    {
        var ids = sourceIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<string, AiKnowledgeSourceEntity>(StringComparer.OrdinalIgnoreCase);
        }

        var sources = await db.Queryable<AiKnowledgeSourceEntity>().Where(item => !item.IsDeleted && ids.Contains(item.Id)).ToListAsync(cancellationToken);
        return sources.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyDictionary<string, AiKnowledgeDocumentEntity>> LoadDocumentsAsync(IEnumerable<string?> documentIds, CancellationToken cancellationToken)
    {
        var ids = documentIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<string, AiKnowledgeDocumentEntity>(StringComparer.OrdinalIgnoreCase);
        }

        var documents = await db.Queryable<AiKnowledgeDocumentEntity>().Where(item => !item.IsDeleted && ids.Contains(item.Id)).ToListAsync(cancellationToken);
        return documents.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> NormalizeList(IEnumerable<string>? values) =>
        values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

    private static string NormalizeMode(string? mode) =>
        string.Equals(mode, "Rebuild", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(mode, "Replace", StringComparison.OrdinalIgnoreCase)
            ? "Rebuild"
            : "Upsert";
}
