using AsterERP.Api.Application.Ai;
using AsterERP.Api.Application.Ai.KnowledgeGraph;
using AsterERP.Contracts.Ai;
using AsterERP.Shared.Exceptions;
using Xunit;

namespace AsterERP.Api.Tests.Ai.KnowledgeGraph;

public sealed class AiKnowledgeGraphImportExportTests : IDisposable
{
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"astererp-kg-import-export-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task ExportAsync_AppliesFiltersAndEvidenceFlag()
    {
        using var db = KnowledgeGraphTestSupport.CreateDb(databasePath);
        KnowledgeGraphTestSupport.InitGraphTables(db);
        await KnowledgeGraphTestSupport.SeedTypesAsync(db, ["entity", "document"], ["depends", "mentions"]);
        var service = CreateService(db);

        await service.ImportAsync(new AiKnowledgeGraphImportRequest
        {
            SourceId = "source-1",
            Nodes =
            [
                new() { NodeKey = "entity:order", NodeType = "entity", DisplayName = "订单", SourceId = "source-1" },
                new() { NodeKey = "entity:invoice", NodeType = "entity", DisplayName = "发票", SourceId = "source-1" },
                new() { NodeKey = "document:manual", NodeType = "document", DisplayName = "手册", SourceId = "source-1" }
            ],
            Edges =
            [
                new()
                {
                    FromNodeKey = "entity:order",
                    ToNodeKey = "entity:invoice",
                    RelationType = "depends",
                    SourceId = "source-1",
                    EvidenceText = "订单生成发票"
                },
                new()
                {
                    FromNodeKey = "document:manual",
                    ToNodeKey = "entity:order",
                    RelationType = "mentions",
                    SourceId = "source-1",
                    EvidenceText = "手册提到订单"
                }
            ]
        }, CancellationToken.None);

        await service.ImportAsync(new AiKnowledgeGraphImportRequest
        {
            SourceId = "source-2",
            Nodes =
            [
                new() { NodeKey = "entity:vendor", NodeType = "entity", DisplayName = "供应商", SourceId = "source-2" },
                new() { NodeKey = "entity:contract", NodeType = "entity", DisplayName = "合同", SourceId = "source-2" }
            ],
            Edges =
            [
                new()
                {
                    FromNodeKey = "entity:vendor",
                    ToNodeKey = "entity:contract",
                    RelationType = "depends",
                    SourceId = "source-2",
                    EvidenceText = "供应商关联合同"
                }
            ]
        }, CancellationToken.None);

        var filtered = await service.ExportAsync(new AiKnowledgeGraphExportRequest
        {
            SourceIds = ["source-1"],
            NodeTypes = ["entity"],
            RelationTypes = ["depends"],
            IncludeEvidence = false
        }, CancellationToken.None);
        var withEvidence = await service.ExportAsync(new AiKnowledgeGraphExportRequest
        {
            SourceIds = ["source-1"],
            NodeTypes = ["entity"],
            RelationTypes = ["depends"],
            IncludeEvidence = true
        }, CancellationToken.None);

        Assert.Equal(2, filtered.Nodes.Count);
        Assert.Single(filtered.Edges);
        Assert.Empty(filtered.Evidence);
        Assert.Equal("depends", filtered.Edges[0].RelationType);
        Assert.All(filtered.Nodes, node => Assert.Equal("source-1", node.SourceId));
        Assert.Single(withEvidence.Evidence);
    }

    [Fact]
    public async Task ImportAsync_RejectsUnknownTypeContracts()
    {
        using var db = KnowledgeGraphTestSupport.CreateDb(databasePath);
        KnowledgeGraphTestSupport.InitGraphTables(db);
        await KnowledgeGraphTestSupport.SeedTypesAsync(db, ["entity"], ["depends"]);
        var service = CreateService(db);

        await Assert.ThrowsAsync<ValidationException>(() => service.ImportAsync(new AiKnowledgeGraphImportRequest
        {
            Nodes =
            [
                new() { NodeKey = "unknown:node", NodeType = "unknown", DisplayName = "非法节点" }
            ]
        }, CancellationToken.None));

        await Assert.ThrowsAsync<ValidationException>(() => service.ImportAsync(new AiKnowledgeGraphImportRequest
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
                    RelationType = "unknown"
                }
            ]
        }, CancellationToken.None));
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

    private static AiKnowledgeGraphImportExportService CreateService(SqlSugar.ISqlSugarClient db) =>
        new(db, new AiWorkspaceContext(KnowledgeGraphTestSupport.CreateCurrentUser("*")));
}
