using AsterERP.Api.Modules.Ai;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.Ai.KnowledgeGraph;

public sealed class AiKnowledgeGraphService(ISqlSugarClient db, AiWorkspaceContext workspaceContext) : IAiKnowledgeGraphService
{
    private const int DefaultGraphLimit = 200;
    private const int MaxGraphLimit = 500;
    private const int MaxEdgeLimit = 1000;
    private const int DefaultDepth = 1;
    private const int MaxDepth = 3;
    private const int MaxPathDepth = 6;
    private const int MaxPathLimit = 20;

    public async Task<AiKnowledgeGraphOverviewDto> GetOverviewAsync(CancellationToken cancellationToken)
    {
        var latestJobs = await db.Queryable<AiKnowledgeGraphBuildJobEntity>()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .Take(1)
            .ToListAsync(cancellationToken);

        var lastNodes = await db.Queryable<AiKnowledgeGraphNodeEntity>()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.UpdatedTime ?? item.CreatedTime, OrderByType.Desc)
            .Take(1)
            .ToListAsync(cancellationToken);

        return new AiKnowledgeGraphOverviewDto
        {
            NodeCount = await db.Queryable<AiKnowledgeGraphNodeEntity>().CountAsync(item => !item.IsDeleted, cancellationToken),
            EdgeCount = await db.Queryable<AiKnowledgeGraphEdgeEntity>().CountAsync(item => !item.IsDeleted, cancellationToken),
            EvidenceCount = await db.Queryable<AiKnowledgeGraphEvidenceEntity>().CountAsync(item => !item.IsDeleted, cancellationToken),
            SourceCount = await db.Queryable<AiKnowledgeSourceEntity>().CountAsync(item => !item.IsDeleted, cancellationToken),
            LatestJob = latestJobs.FirstOrDefault() is { } latest ? AiKnowledgeGraphMapper.MapJob(latest) : null,
            LastUpdatedTime = lastNodes.FirstOrDefault()?.UpdatedTime ?? lastNodes.FirstOrDefault()?.CreatedTime
        };
    }

    public async Task<IReadOnlyList<AiKnowledgeGraphNodeTypeDto>> GetNodeTypesAsync(CancellationToken cancellationToken)
    {
        var rows = await db.Queryable<AiKnowledgeGraphNodeTypeEntity>()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.IsSystem, OrderByType.Desc)
            .OrderBy(item => item.Code)
            .ToListAsync(cancellationToken);
        return rows.Select(AiKnowledgeGraphMapper.MapNodeType).ToList();
    }

    public async Task<IReadOnlyList<AiKnowledgeGraphRelationTypeDto>> GetRelationTypesAsync(CancellationToken cancellationToken)
    {
        var rows = await db.Queryable<AiKnowledgeGraphRelationTypeEntity>()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.IsSystem, OrderByType.Desc)
            .OrderBy(item => item.Code)
            .ToListAsync(cancellationToken);
        return rows.Select(AiKnowledgeGraphMapper.MapRelationType).ToList();
    }

    public async Task<AiKnowledgeGraphResponse> QueryAsync(AiKnowledgeGraphQueryRequest request, CancellationToken cancellationToken)
    {
        var limit = NormalizeLimit(request.Limit);
        var depth = NormalizeDepth(request.Depth, MaxDepth);
        var relationTypes = NormalizeList(request.RelationTypes);
        var sourceIds = NormalizeList(request.SourceIds);
        var nodeTypes = NormalizeList(request.NodeTypes);
        var query = db.Queryable<AiKnowledgeGraphNodeEntity>().Where(item => !item.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(item =>
                item.NodeKey.Contains(keyword) ||
                item.DisplayName.Contains(keyword) ||
                (item.Summary != null && item.Summary.Contains(keyword)));
        }

        if (sourceIds.Count > 0)
        {
            query = query.Where(item => item.SourceId != null && sourceIds.Contains(item.SourceId));
        }

        if (nodeTypes.Count > 0)
        {
            query = query.Where(item => nodeTypes.Contains(item.NodeType));
        }

        var seedNodes = await query
            .OrderBy(item => item.UpdatedTime ?? item.CreatedTime, OrderByType.Desc)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
        var truncated = seedNodes.Count > limit;
        var nodeIds = seedNodes.Take(limit).Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var edgeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await ExpandGraphAsync(nodeIds, edgeIds, "Both", depth, limit, relationTypes, sourceIds, cancellationToken);
        return await BuildGraphResponseAsync(nodeIds, edgeIds, truncated || nodeIds.Count > limit || edgeIds.Count > MaxEdgeLimit, cancellationToken);
    }

    public async Task<AiKnowledgeGraphResponse> GetNeighborhoodAsync(AiKnowledgeGraphNeighborhoodRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NodeId))
        {
            throw new ValidationException("邻域查询缺少节点 Id", ErrorCodes.ParameterInvalid);
        }

        var root = await LoadNodeAsync(request.NodeId.Trim(), cancellationToken);
        var limit = NormalizeLimit(request.Limit);
        var nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { root.Id };
        var edgeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await ExpandGraphAsync(nodeIds, edgeIds, NormalizeDirection(request.Direction), NormalizeDepth(request.Depth, MaxDepth), limit, [], [], cancellationToken);
        return await BuildGraphResponseAsync(nodeIds, edgeIds, nodeIds.Count > limit || edgeIds.Count > MaxEdgeLimit, cancellationToken);
    }

    public async Task<AiKnowledgeGraphPathResponse> FindPathsAsync(AiKnowledgeGraphPathRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FromNodeId) || string.IsNullOrWhiteSpace(request.ToNodeId))
        {
            throw new ValidationException("路径分析缺少起点或终点节点", ErrorCodes.ParameterInvalid);
        }

        await EnsureNodeExistsAsync(request.FromNodeId.Trim(), cancellationToken);
        await EnsureNodeExistsAsync(request.ToNodeId.Trim(), cancellationToken);
        var relationTypes = NormalizeList(request.RelationTypes);
        var maxDepth = NormalizeDepth(request.MaxDepth, MaxPathDepth);
        var limit = Math.Clamp(request.Limit <= 0 ? MaxPathLimit : request.Limit, 1, MaxPathLimit);
        var paths = new List<AiKnowledgeGraphPathDto>();
        var nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var edgeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<PathCursor>();
        queue.Enqueue(new PathCursor(request.FromNodeId.Trim(), [request.FromNodeId.Trim()], []));

        while (queue.Count > 0 && paths.Count < limit)
        {
            var cursor = queue.Dequeue();
            if (cursor.NodeIds.Count - 1 >= maxDepth)
            {
                continue;
            }

            var edges = await QueryEdgesFromFrontierAsync([cursor.NodeId], "Outgoing", relationTypes, [], MaxEdgeLimit, cancellationToken);
            foreach (var edge in edges.OrderBy(item => item.Id))
            {
                if (cursor.NodeIds.Contains(edge.ToNodeId, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                var nextNodes = cursor.NodeIds.Append(edge.ToNodeId).ToList();
                var nextEdges = cursor.EdgeIds.Append(edge.Id).ToList();
                if (string.Equals(edge.ToNodeId, request.ToNodeId, StringComparison.OrdinalIgnoreCase))
                {
                    paths.Add(new AiKnowledgeGraphPathDto { NodeIds = nextNodes, EdgeIds = nextEdges });
                    foreach (var id in nextNodes)
                    {
                        nodeIds.Add(id);
                    }

                    foreach (var id in nextEdges)
                    {
                        edgeIds.Add(id);
                    }

                    continue;
                }

                queue.Enqueue(new PathCursor(edge.ToNodeId, nextNodes, nextEdges));
            }
        }

        var nodes = await LoadMappedNodesAsync(nodeIds, cancellationToken);
        var edgesById = await LoadEdgesAsync(edgeIds, cancellationToken);
        return new AiKnowledgeGraphPathResponse
        {
            Paths = paths,
            Nodes = nodes,
            Edges = edgesById.Select(AiKnowledgeGraphMapper.MapEdge).ToList(),
            Truncated = paths.Count >= limit
        };
    }

    public async Task<AiKnowledgeGraphImpactResponse> AnalyzeImpactAsync(AiKnowledgeGraphImpactRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NodeId))
        {
            throw new ValidationException("影响分析缺少节点 Id", ErrorCodes.ParameterInvalid);
        }

        var root = await LoadNodeAsync(request.NodeId.Trim(), cancellationToken);
        var limit = NormalizeLimit(request.Limit);
        var nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { root.Id };
        var edgeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await ExpandGraphAsync(nodeIds, edgeIds, NormalizeDirection(request.Direction), NormalizeDepth(request.MaxDepth, MaxPathDepth), limit, [], [], cancellationToken);
        var nodes = await LoadMappedNodesAsync(nodeIds.Where(id => !string.Equals(id, root.Id, StringComparison.OrdinalIgnoreCase)), cancellationToken);
        var edges = await LoadEdgesAsync(edgeIds, cancellationToken);
        return new AiKnowledgeGraphImpactResponse
        {
            RootNodeId = root.Id,
            Nodes = nodes,
            Edges = edges.Select(AiKnowledgeGraphMapper.MapEdge).ToList(),
            Truncated = nodeIds.Count > limit || edgeIds.Count > MaxEdgeLimit
        };
    }

    public async Task<AiKnowledgeGraphNodeDto> GetNodeAsync(string id, CancellationToken cancellationToken)
    {
        var entity = await LoadNodeAsync(id, cancellationToken);
        var sources = await LoadSourcesAsync([entity.SourceId], cancellationToken);
        var documents = await LoadDocumentsAsync([entity.DocumentId], cancellationToken);
        return AiKnowledgeGraphMapper.MapNode(entity, sources, documents);
    }

    public async Task<AiKnowledgeGraphNodeDto> CreateNodeAsync(AiKnowledgeGraphNodeUpsertRequest request, CancellationToken cancellationToken)
    {
        var workspace = workspaceContext.Resolve();
        var normalized = NormalizeNodeRequest(request);
        await EnsureNodeTypeExistsAsync(normalized.NodeType, cancellationToken);
        await EnsureSourceDocumentAsync(normalized.SourceId, normalized.DocumentId, cancellationToken);

        var duplicate = await db.Queryable<AiKnowledgeGraphNodeEntity>()
            .AnyAsync(item => !item.IsDeleted && item.NodeKey == normalized.NodeKey, cancellationToken);
        if (duplicate)
        {
            throw new ValidationException($"图谱节点 Key 已存在：{normalized.NodeKey}", ErrorCodes.ParameterInvalid);
        }

        var entity = new AiKnowledgeGraphNodeEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            NodeKey = normalized.NodeKey,
            NodeType = normalized.NodeType,
            DisplayName = normalized.DisplayName,
            Summary = normalized.Summary,
            SourceId = normalized.SourceId,
            DocumentId = normalized.DocumentId,
            MetadataJson = normalized.MetadataJson
        };
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return await GetNodeAsync(entity.Id, cancellationToken);
    }

    public async Task<AiKnowledgeGraphNodeDto> UpdateNodeAsync(string id, AiKnowledgeGraphNodeUpsertRequest request, CancellationToken cancellationToken)
    {
        var entity = await LoadNodeAsync(id, cancellationToken);
        var normalized = NormalizeNodeRequest(request);
        await EnsureNodeTypeExistsAsync(normalized.NodeType, cancellationToken);
        await EnsureSourceDocumentAsync(normalized.SourceId, normalized.DocumentId, cancellationToken);

        var duplicate = await db.Queryable<AiKnowledgeGraphNodeEntity>()
            .AnyAsync(item => !item.IsDeleted && item.Id != entity.Id && item.NodeKey == normalized.NodeKey, cancellationToken);
        if (duplicate)
        {
            throw new ValidationException($"图谱节点 Key 已存在：{normalized.NodeKey}", ErrorCodes.ParameterInvalid);
        }

        entity.NodeKey = normalized.NodeKey;
        entity.NodeType = normalized.NodeType;
        entity.DisplayName = normalized.DisplayName;
        entity.Summary = normalized.Summary;
        entity.SourceId = normalized.SourceId;
        entity.DocumentId = normalized.DocumentId;
        entity.MetadataJson = normalized.MetadataJson;
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return await GetNodeAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteNodeAsync(string id, bool cascade, CancellationToken cancellationToken)
    {
        var entity = await LoadNodeAsync(id, cancellationToken);
        var edges = await db.Queryable<AiKnowledgeGraphEdgeEntity>()
            .Where(item => !item.IsDeleted && (item.FromNodeId == entity.Id || item.ToNodeId == entity.Id))
            .ToListAsync(cancellationToken);
        if (edges.Count > 0 && !cascade)
        {
            throw new ValidationException($"节点存在 {edges.Count} 条关联关系，不能直接删除", ErrorCodes.ParameterInvalid);
        }

        var now = DateTime.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedTime = now;
        entity.UpdatedTime = now;
        foreach (var edge in edges)
        {
            edge.IsDeleted = true;
            edge.DeletedTime = now;
            edge.UpdatedTime = now;
        }

        await db.Ado.BeginTranAsync();
        try
        {
            if (edges.Count > 0)
            {
                await db.Updateable(edges).ExecuteCommandAsync(cancellationToken);
            }

            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
            await db.Ado.CommitTranAsync();
        }
        catch
        {
            await db.Ado.RollbackTranAsync();
            throw;
        }

        return true;
    }

    public async Task<AiKnowledgeGraphEdgeDto> GetEdgeAsync(string id, CancellationToken cancellationToken)
    {
        return AiKnowledgeGraphMapper.MapEdge(await LoadEdgeAsync(id, cancellationToken));
    }

    public async Task<AiKnowledgeGraphEdgeDto> CreateEdgeAsync(AiKnowledgeGraphEdgeUpsertRequest request, CancellationToken cancellationToken)
    {
        var workspace = workspaceContext.Resolve();
        var normalized = NormalizeEdgeRequest(request);
        await EnsureRelationTypeExistsAsync(normalized.RelationType, cancellationToken);
        await EnsureNodeExistsAsync(normalized.FromNodeId, cancellationToken);
        await EnsureNodeExistsAsync(normalized.ToNodeId, cancellationToken);
        await EnsureSourceDocumentAsync(normalized.SourceId, null, cancellationToken);

        var entity = new AiKnowledgeGraphEdgeEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            SourceId = normalized.SourceId,
            FromNodeId = normalized.FromNodeId,
            ToNodeId = normalized.ToNodeId,
            RelationType = normalized.RelationType,
            Weight = normalized.Weight,
            EvidenceText = normalized.EvidenceText,
            MetadataJson = normalized.MetadataJson
        };
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteEdgeEvidenceAsync(entity, cancellationToken);
        return AiKnowledgeGraphMapper.MapEdge(entity);
    }

    public async Task<AiKnowledgeGraphEdgeDto> UpdateEdgeAsync(string id, AiKnowledgeGraphEdgeUpsertRequest request, CancellationToken cancellationToken)
    {
        var entity = await LoadEdgeAsync(id, cancellationToken);
        var normalized = NormalizeEdgeRequest(request);
        await EnsureRelationTypeExistsAsync(normalized.RelationType, cancellationToken);
        await EnsureNodeExistsAsync(normalized.FromNodeId, cancellationToken);
        await EnsureNodeExistsAsync(normalized.ToNodeId, cancellationToken);
        await EnsureSourceDocumentAsync(normalized.SourceId, null, cancellationToken);

        entity.SourceId = normalized.SourceId;
        entity.FromNodeId = normalized.FromNodeId;
        entity.ToNodeId = normalized.ToNodeId;
        entity.RelationType = normalized.RelationType;
        entity.Weight = normalized.Weight;
        entity.EvidenceText = normalized.EvidenceText;
        entity.MetadataJson = normalized.MetadataJson;
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteEdgeEvidenceAsync(entity, cancellationToken);
        return AiKnowledgeGraphMapper.MapEdge(entity);
    }

    public async Task<bool> DeleteEdgeAsync(string id, CancellationToken cancellationToken)
    {
        var entity = await LoadEdgeAsync(id, cancellationToken);
        entity.IsDeleted = true;
        entity.DeletedTime = DateTime.UtcNow;
        entity.UpdatedTime = entity.DeletedTime;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return true;
    }

    private async Task ExpandGraphAsync(
        HashSet<string> nodeIds,
        HashSet<string> edgeIds,
        string direction,
        int depth,
        int limit,
        IReadOnlyList<string> relationTypes,
        IReadOnlyList<string> sourceIds,
        CancellationToken cancellationToken)
    {
        var frontier = nodeIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var level = 0; level < depth && frontier.Count > 0 && nodeIds.Count <= limit && edgeIds.Count <= MaxEdgeLimit; level++)
        {
            var edges = await QueryEdgesFromFrontierAsync(frontier, direction, relationTypes, sourceIds, MaxEdgeLimit - edgeIds.Count + 1, cancellationToken);
            frontier.Clear();
            foreach (var edge in edges)
            {
                edgeIds.Add(edge.Id);
                if (nodeIds.Add(edge.FromNodeId))
                {
                    frontier.Add(edge.FromNodeId);
                }

                if (nodeIds.Add(edge.ToNodeId))
                {
                    frontier.Add(edge.ToNodeId);
                }
            }
        }
    }

    private async Task<IReadOnlyList<AiKnowledgeGraphEdgeEntity>> QueryEdgesFromFrontierAsync(
        IReadOnlyCollection<string> frontier,
        string direction,
        IReadOnlyList<string> relationTypes,
        IReadOnlyList<string> sourceIds,
        int limit,
        CancellationToken cancellationToken)
    {
        if (frontier.Count == 0)
        {
            return [];
        }

        var frontierIds = frontier.ToList();
        var query = db.Queryable<AiKnowledgeGraphEdgeEntity>().Where(item => !item.IsDeleted);
        query = direction switch
        {
            "Incoming" => query.Where(item => frontierIds.Contains(item.ToNodeId)),
            "Outgoing" => query.Where(item => frontierIds.Contains(item.FromNodeId)),
            _ => query.Where(item => frontierIds.Contains(item.FromNodeId) || frontierIds.Contains(item.ToNodeId))
        };

        if (relationTypes.Count > 0)
        {
            query = query.Where(item => relationTypes.Contains(item.RelationType));
        }

        if (sourceIds.Count > 0)
        {
            query = query.Where(item => item.SourceId != null && sourceIds.Contains(item.SourceId));
        }

        return await query.OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .Take(Math.Clamp(limit, 1, MaxEdgeLimit + 1))
            .ToListAsync(cancellationToken);
    }

    private async Task<AiKnowledgeGraphResponse> BuildGraphResponseAsync(
        IReadOnlyCollection<string> nodeIds,
        IReadOnlyCollection<string> edgeIds,
        bool truncated,
        CancellationToken cancellationToken)
    {
        var boundedNodeIds = nodeIds.Take(MaxGraphLimit).ToList();
        var boundedEdgeIds = edgeIds.Take(MaxEdgeLimit).ToList();
        var nodes = await LoadMappedNodesAsync(boundedNodeIds, cancellationToken);
        var edges = await LoadEdgesAsync(boundedEdgeIds, cancellationToken);
        return new AiKnowledgeGraphResponse
        {
            Nodes = nodes,
            Edges = edges.Select(AiKnowledgeGraphMapper.MapEdge).ToList(),
            TotalNodes = nodeIds.Count,
            TotalEdges = edgeIds.Count,
            Truncated = truncated || nodeIds.Count > MaxGraphLimit || edgeIds.Count > MaxEdgeLimit,
            TraceId = Guid.NewGuid().ToString("N")
        };
    }

    private async Task<IReadOnlyList<AiKnowledgeGraphNodeDto>> LoadMappedNodesAsync(IEnumerable<string?> nodeIds, CancellationToken cancellationToken)
    {
        var ids = nodeIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var nodes = await db.Queryable<AiKnowledgeGraphNodeEntity>()
            .Where(item => !item.IsDeleted && ids.Contains(item.Id))
            .ToListAsync(cancellationToken);
        var sources = await LoadSourcesAsync(nodes.Select(item => item.SourceId), cancellationToken);
        var documents = await LoadDocumentsAsync(nodes.Select(item => item.DocumentId), cancellationToken);
        return nodes.Select(item => AiKnowledgeGraphMapper.MapNode(item, sources, documents)).ToList();
    }

    private async Task<IReadOnlyList<AiKnowledgeGraphEdgeEntity>> LoadEdgesAsync(IEnumerable<string> edgeIds, CancellationToken cancellationToken)
    {
        var ids = edgeIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await db.Queryable<AiKnowledgeGraphEdgeEntity>()
            .Where(item => !item.IsDeleted && ids.Contains(item.Id))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, AiKnowledgeSourceEntity>> LoadSourcesAsync(IEnumerable<string?> sourceIds, CancellationToken cancellationToken)
    {
        var ids = sourceIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<string, AiKnowledgeSourceEntity>(StringComparer.OrdinalIgnoreCase);
        }

        var sources = await db.Queryable<AiKnowledgeSourceEntity>()
            .Where(item => !item.IsDeleted && ids.Contains(item.Id))
            .ToListAsync(cancellationToken);
        return sources.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyDictionary<string, AiKnowledgeDocumentEntity>> LoadDocumentsAsync(IEnumerable<string?> documentIds, CancellationToken cancellationToken)
    {
        var ids = documentIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<string, AiKnowledgeDocumentEntity>(StringComparer.OrdinalIgnoreCase);
        }

        var documents = await db.Queryable<AiKnowledgeDocumentEntity>()
            .Where(item => !item.IsDeleted && ids.Contains(item.Id))
            .ToListAsync(cancellationToken);
        return documents.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<AiKnowledgeGraphNodeEntity> LoadNodeAsync(string id, CancellationToken cancellationToken)
    {
        var entity = await db.Queryable<AiKnowledgeGraphNodeEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id, cancellationToken);
        return entity ?? throw new ValidationException("图谱节点不存在", ErrorCodes.ParameterInvalid);
    }

    private async Task<AiKnowledgeGraphEdgeEntity> LoadEdgeAsync(string id, CancellationToken cancellationToken)
    {
        var entity = await db.Queryable<AiKnowledgeGraphEdgeEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id, cancellationToken);
        return entity ?? throw new ValidationException("图谱关系不存在", ErrorCodes.ParameterInvalid);
    }

    private async Task EnsureNodeExistsAsync(string id, CancellationToken cancellationToken)
    {
        var exists = await db.Queryable<AiKnowledgeGraphNodeEntity>().AnyAsync(item => !item.IsDeleted && item.Id == id, cancellationToken);
        if (!exists)
        {
            throw new ValidationException("图谱节点不存在", ErrorCodes.ParameterInvalid);
        }
    }

    private async Task EnsureNodeTypeExistsAsync(string nodeType, CancellationToken cancellationToken)
    {
        var exists = await db.Queryable<AiKnowledgeGraphNodeTypeEntity>().AnyAsync(item => !item.IsDeleted && item.Code == nodeType, cancellationToken);
        if (!exists)
        {
            throw new ValidationException($"图谱节点类型不存在：{nodeType}", ErrorCodes.ParameterInvalid);
        }
    }

    private async Task EnsureRelationTypeExistsAsync(string relationType, CancellationToken cancellationToken)
    {
        var exists = await db.Queryable<AiKnowledgeGraphRelationTypeEntity>().AnyAsync(item => !item.IsDeleted && item.Code == relationType, cancellationToken);
        if (!exists)
        {
            throw new ValidationException($"图谱关系类型不存在：{relationType}", ErrorCodes.ParameterInvalid);
        }
    }

    private async Task EnsureSourceDocumentAsync(string? sourceId, string? documentId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(sourceId))
        {
            var sourceExists = await db.Queryable<AiKnowledgeSourceEntity>().AnyAsync(item => !item.IsDeleted && item.Id == sourceId, cancellationToken);
            if (!sourceExists)
            {
                throw new ValidationException("知识来源不存在", ErrorCodes.ParameterInvalid);
            }
        }

        if (!string.IsNullOrWhiteSpace(documentId))
        {
            var documentExists = await db.Queryable<AiKnowledgeDocumentEntity>().AnyAsync(item => !item.IsDeleted && item.Id == documentId, cancellationToken);
            if (!documentExists)
            {
                throw new ValidationException("知识文档不存在", ErrorCodes.ParameterInvalid);
            }
        }
    }

    private async Task WriteEdgeEvidenceAsync(AiKnowledgeGraphEdgeEntity edge, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(edge.EvidenceText))
        {
            return;
        }

        var workspace = workspaceContext.Resolve();
        var exists = await db.Queryable<AiKnowledgeGraphEvidenceEntity>()
            .AnyAsync(item => !item.IsDeleted && item.EdgeId == edge.Id && item.EvidenceText == edge.EvidenceText, cancellationToken);
        if (exists)
        {
            return;
        }

        await db.Insertable(new AiKnowledgeGraphEvidenceEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            SourceId = edge.SourceId,
            EdgeId = edge.Id,
            EvidenceText = edge.EvidenceText
        }).ExecuteCommandAsync(cancellationToken);
    }

    private static AiKnowledgeGraphNodeUpsertRequest NormalizeNodeRequest(AiKnowledgeGraphNodeUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NodeKey) || string.IsNullOrWhiteSpace(request.NodeType) || string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new ValidationException("图谱节点缺少 Key、类型或名称", ErrorCodes.ParameterInvalid);
        }

        return new AiKnowledgeGraphNodeUpsertRequest
        {
            NodeKey = request.NodeKey.Trim(),
            NodeType = request.NodeType.Trim(),
            DisplayName = request.DisplayName.Trim(),
            Summary = string.IsNullOrWhiteSpace(request.Summary) ? null : request.Summary.Trim(),
            SourceId = string.IsNullOrWhiteSpace(request.SourceId) ? null : request.SourceId.Trim(),
            DocumentId = string.IsNullOrWhiteSpace(request.DocumentId) ? null : request.DocumentId.Trim(),
            MetadataJson = string.IsNullOrWhiteSpace(request.MetadataJson) ? null : request.MetadataJson.Trim()
        };
    }

    private static AiKnowledgeGraphEdgeUpsertRequest NormalizeEdgeRequest(AiKnowledgeGraphEdgeUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FromNodeId) || string.IsNullOrWhiteSpace(request.ToNodeId) || string.IsNullOrWhiteSpace(request.RelationType))
        {
            throw new ValidationException("图谱关系缺少起点、终点或关系类型", ErrorCodes.ParameterInvalid);
        }

        if (request.Weight is < 0 or > 1)
        {
            throw new ValidationException("图谱关系权重必须在 0 到 1 之间", ErrorCodes.ParameterInvalid);
        }

        return new AiKnowledgeGraphEdgeUpsertRequest
        {
            FromNodeId = request.FromNodeId.Trim(),
            ToNodeId = request.ToNodeId.Trim(),
            RelationType = request.RelationType.Trim(),
            Weight = request.Weight,
            EvidenceText = string.IsNullOrWhiteSpace(request.EvidenceText) ? null : request.EvidenceText.Trim(),
            SourceId = string.IsNullOrWhiteSpace(request.SourceId) ? null : request.SourceId.Trim(),
            MetadataJson = string.IsNullOrWhiteSpace(request.MetadataJson) ? null : request.MetadataJson.Trim()
        };
    }

    private static IReadOnlyList<string> NormalizeList(IEnumerable<string>? values) =>
        values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

    private static int NormalizeLimit(int limit) => Math.Clamp(limit <= 0 ? DefaultGraphLimit : limit, 1, MaxGraphLimit);

    private static int NormalizeDepth(int depth, int maxDepth) => Math.Clamp(depth <= 0 ? DefaultDepth : depth, 1, maxDepth);

    private static string NormalizeDirection(string? direction) =>
        string.Equals(direction, "Incoming", StringComparison.OrdinalIgnoreCase)
            ? "Incoming"
            : string.Equals(direction, "Outgoing", StringComparison.OrdinalIgnoreCase)
                ? "Outgoing"
                : "Both";

    private sealed record PathCursor(string NodeId, IReadOnlyList<string> NodeIds, IReadOnlyList<string> EdgeIds);
}
