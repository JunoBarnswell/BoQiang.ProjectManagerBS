using AsterERP.Api.Application.Ai;
using AsterERP.Api.Application.Ai.KnowledgeGraph;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.Ai;
using AsterERP.Contracts.Ai;
using Microsoft.AspNetCore.Http;
using SqlSugar;
using Volo.Abp.AspNetCore.Security.Claims;
using Volo.Abp.Users;
using Xunit;

namespace AsterERP.Api.Tests.Ai.KnowledgeGraph;

public sealed class AiKnowledgeGraphServiceTests : IDisposable
{
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"astererp-kg-tests-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task ImportAsync_UpsertsNodesEdgesAndDeduplicatesEvidence()
    {
        using var db = CreateDb();
        InitGraphTables(db);
        await SeedTypesAsync(db, ["entity"], ["depends"]);
        var service = new AiKnowledgeGraphImportExportService(db, new AiWorkspaceContext(CreateCurrentUser()));
        var request = new AiKnowledgeGraphImportRequest
        {
            Nodes =
            [
                new() { NodeKey = "entity:order", NodeType = "entity", DisplayName = "订单" },
                new() { NodeKey = "entity:invoice", NodeType = "entity", DisplayName = "发票" }
            ],
            Edges =
            [
                new()
                {
                    FromNodeKey = "entity:order",
                    ToNodeKey = "entity:invoice",
                    RelationType = "depends",
                    EvidenceText = "订单生成发票"
                }
            ]
        };

        var first = await service.ImportAsync(request, CancellationToken.None);
        var second = await service.ImportAsync(request, CancellationToken.None);
        var exported = await service.ExportAsync(new AiKnowledgeGraphExportRequest(), CancellationToken.None);
        var evidenceCount = await db.Queryable<AiKnowledgeGraphEvidenceEntity>().CountAsync(item => !item.IsDeleted);

        Assert.Equal(3, first.CreatedCount);
        Assert.Equal(3, second.UpdatedCount);
        Assert.Equal(2, exported.Nodes.Count);
        Assert.Single(exported.Edges);
        Assert.Single(exported.Evidence);
        Assert.Equal(1, evidenceCount);
    }

    [Fact]
    public async Task ImportAsync_RebuildClearsScopedNodesEdgesAndEvidence()
    {
        using var db = CreateDb();
        InitGraphTables(db);
        await SeedTypesAsync(db, ["entity"], ["depends"]);
        var service = new AiKnowledgeGraphImportExportService(db, new AiWorkspaceContext(CreateCurrentUser()));
        await service.ImportAsync(new AiKnowledgeGraphImportRequest
        {
            SourceId = "source-1",
            Nodes =
            [
                new() { NodeKey = "entity:old-a", NodeType = "entity", DisplayName = "旧节点 A", SourceId = "source-1" },
                new() { NodeKey = "entity:old-b", NodeType = "entity", DisplayName = "旧节点 B", SourceId = "source-1" }
            ],
            Edges =
            [
                new()
                {
                    FromNodeKey = "entity:old-a",
                    ToNodeKey = "entity:old-b",
                    RelationType = "depends",
                    SourceId = "source-1",
                    EvidenceText = "旧证据"
                }
            ]
        }, CancellationToken.None);

        await service.ImportAsync(new AiKnowledgeGraphImportRequest
        {
            SourceId = "source-1",
            Mode = "Rebuild",
            Nodes =
            [
                new() { NodeKey = "entity:new", NodeType = "entity", DisplayName = "新节点", SourceId = "source-1" }
            ]
        }, CancellationToken.None);

        var activeNodes = await db.Queryable<AiKnowledgeGraphNodeEntity>().Where(item => !item.IsDeleted).ToListAsync();
        var activeEdges = await db.Queryable<AiKnowledgeGraphEdgeEntity>().Where(item => !item.IsDeleted).ToListAsync();
        var activeEvidence = await db.Queryable<AiKnowledgeGraphEvidenceEntity>().Where(item => !item.IsDeleted).ToListAsync();

        Assert.Single(activeNodes);
        Assert.Equal("entity:new", activeNodes[0].NodeKey);
        Assert.Empty(activeEdges);
        Assert.Empty(activeEvidence);
    }

    [Fact]
    public async Task ReindexAsync_BuildsGraphFromKnowledgeChunks()
    {
        using var db = CreateDb();
        InitGraphTables(db);
        db.CodeFirst.InitTables<AiKnowledgeSourceEntity, AiKnowledgeDocumentEntity, AiKnowledgeChunkEntity>();
        await SeedTypesAsync(db, ["source", "document", "chunk", "term"], ["contains", "mentions"]);
        await db.Insertable(new AiKnowledgeSourceEntity
        {
            Id = "source-1",
            TenantId = "tenant-system",
            AppCode = "SYSTEM",
            OwnerUserId = "admin",
            SourceCode = "purchase",
            SourceName = "采购知识库"
        }).ExecuteCommandAsync();
        await db.Insertable(new AiKnowledgeDocumentEntity
        {
            Id = "doc-1",
            TenantId = "tenant-system",
            AppCode = "SYSTEM",
            OwnerUserId = "admin",
            SourceId = "source-1",
            DocumentName = "采购审批说明",
            ContentType = "text/plain"
        }).ExecuteCommandAsync();
        await db.Insertable(new AiKnowledgeChunkEntity
        {
            Id = "chunk-1",
            TenantId = "tenant-system",
            AppCode = "SYSTEM",
            OwnerUserId = "admin",
            SourceId = "source-1",
            DocumentId = "doc-1",
            ChunkIndex = 1,
            Content = "采购审批 workflow approval purchase order"
        }).ExecuteCommandAsync();

        var workspaceContext = new AiWorkspaceContext(CreateCurrentUser());
        var importExportService = new AiKnowledgeGraphImportExportService(db, workspaceContext);
        var service = new AiKnowledgeGraphBuildService(db, workspaceContext, importExportService);
        var job = await service.ReindexAsync(new AiKnowledgeGraphBuildRequest { SourceId = "source-1" }, CancellationToken.None);

        var nodeCount = await db.Queryable<AiKnowledgeGraphNodeEntity>().CountAsync(item => !item.IsDeleted);
        var edgeCount = await db.Queryable<AiKnowledgeGraphEdgeEntity>().CountAsync(item => !item.IsDeleted);

        Assert.Equal("Completed", job.Status);
        Assert.Equal(100, job.Progress);
        Assert.True(nodeCount >= 4);
        Assert.True(edgeCount >= 2);
        Assert.True(job.CreatedCount >= nodeCount);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
        catch (IOException)
        {
        }
    }

    private SqlSugarClient CreateDb() =>
        new(new ConnectionConfig
        {
            ConnectionString = $"Data Source={databasePath}",
            DbType = DbType.Sqlite,
            InitKeyType = InitKeyType.Attribute,
            IsAutoCloseConnection = true
        });

    private static void InitGraphTables(ISqlSugarClient db)
    {
        db.CodeFirst.InitTables<
            AiKnowledgeGraphNodeTypeEntity,
            AiKnowledgeGraphRelationTypeEntity,
            AiKnowledgeGraphNodeEntity,
            AiKnowledgeGraphEdgeEntity,
            AiKnowledgeGraphEvidenceEntity>();
        db.CodeFirst.InitTables<AiKnowledgeGraphBuildJobEntity>();
    }

    private static async Task SeedTypesAsync(ISqlSugarClient db, IReadOnlyList<string> nodeTypes, IReadOnlyList<string> relationTypes)
    {
        foreach (var nodeType in nodeTypes)
        {
            await db.Insertable(new AiKnowledgeGraphNodeTypeEntity
            {
                TenantId = "tenant-system",
                AppCode = "SYSTEM",
                Code = nodeType,
                Name = nodeType,
                IsSystem = true
            }).ExecuteCommandAsync();
        }

        foreach (var relationType in relationTypes)
        {
            await db.Insertable(new AiKnowledgeGraphRelationTypeEntity
            {
                TenantId = "tenant-system",
                AppCode = "SYSTEM",
                Code = relationType,
                Name = relationType,
                IsSystem = true
            }).ExecuteCommandAsync();
        }
    }

    private static ICurrentUser CreateCurrentUser()
    {
        var principal = AsterErpClaimsPrincipalFactory.Create(new ResolvedAuthenticatedUser(
            "admin",
            "admin",
            "tenant-system",
            "默认租户",
            "SYSTEM",
            "系统管理",
            "root",
            "system-admin",
            ["role-id-admin"],
            ["admin"],
            ["*"],
            "ALL",
            true,
            true,
            true,
            "平台管理员"));
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };
        return new CurrentUser(new HttpContextCurrentPrincipalAccessor(httpContextAccessor));
    }
}
