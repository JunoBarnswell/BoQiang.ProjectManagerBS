using System.Reflection;
using AsterERP.Api.Controllers;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace AsterERP.Api.Tests.Ai.KnowledgeGraph;

public sealed class AiKnowledgeGraphPermissionTests
{
    [Fact]
    public void ControllerActions_DeclareGraphPermissionCodes()
    {
        var expectations = new Dictionary<string, string>
        {
            [nameof(AiKnowledgeGraphController.GetOverviewAsync)] = PermissionCodes.AiKnowledgeGraphView,
            [nameof(AiKnowledgeGraphController.GetNodeTypesAsync)] = PermissionCodes.AiKnowledgeGraphView,
            [nameof(AiKnowledgeGraphController.GetRelationTypesAsync)] = PermissionCodes.AiKnowledgeGraphView,
            [nameof(AiKnowledgeGraphController.QueryAsync)] = PermissionCodes.AiKnowledgeGraphSearch,
            [nameof(AiKnowledgeGraphController.GetNeighborhoodAsync)] = PermissionCodes.AiKnowledgeGraphSearch,
            [nameof(AiKnowledgeGraphController.FindPathsAsync)] = PermissionCodes.AiKnowledgeGraphSearch,
            [nameof(AiKnowledgeGraphController.AnalyzeImpactAsync)] = PermissionCodes.AiKnowledgeGraphSearch,
            [nameof(AiKnowledgeGraphController.GetNodeAsync)] = PermissionCodes.AiKnowledgeGraphView,
            [nameof(AiKnowledgeGraphController.CreateNodeAsync)] = PermissionCodes.AiKnowledgeGraphEdit,
            [nameof(AiKnowledgeGraphController.UpdateNodeAsync)] = PermissionCodes.AiKnowledgeGraphEdit,
            [nameof(AiKnowledgeGraphController.DeleteNodeAsync)] = PermissionCodes.AiKnowledgeGraphEdit,
            [nameof(AiKnowledgeGraphController.GetEdgeAsync)] = PermissionCodes.AiKnowledgeGraphView,
            [nameof(AiKnowledgeGraphController.CreateEdgeAsync)] = PermissionCodes.AiKnowledgeGraphEdit,
            [nameof(AiKnowledgeGraphController.UpdateEdgeAsync)] = PermissionCodes.AiKnowledgeGraphEdit,
            [nameof(AiKnowledgeGraphController.DeleteEdgeAsync)] = PermissionCodes.AiKnowledgeGraphEdit,
            [nameof(AiKnowledgeGraphController.ReindexAsync)] = PermissionCodes.AiKnowledgeGraphReindex,
            [nameof(AiKnowledgeGraphController.GetJobAsync)] = PermissionCodes.AiKnowledgeGraphView,
            [nameof(AiKnowledgeGraphController.ImportAsync)] = PermissionCodes.AiKnowledgeGraphImport,
            [nameof(AiKnowledgeGraphController.ExportAsync)] = PermissionCodes.AiKnowledgeGraphExport
        };

        foreach (var expectation in expectations)
        {
            var method = typeof(AiKnowledgeGraphController)
                .GetMethod(expectation.Key, BindingFlags.Public | BindingFlags.Instance);
            var permission = method?.GetCustomAttribute<PermissionAttribute>();

            Assert.NotNull(permission);
            Assert.Equal(expectation.Value, permission.Code);
        }
    }

    [Fact]
    public async Task PermissionAttribute_Returns403WhenGraphPermissionIsMissing()
    {
        var user = KnowledgeGraphTestSupport.CreateCurrentUser(PermissionCodes.AiKnowledgeGraphView);
        var context = KnowledgeGraphTestSupport.CreateAuthorizationContext(user, "/api/ai/knowledge/graph/nodes");
        var permission = new PermissionAttribute(PermissionCodes.AiKnowledgeGraphEdit);

        await permission.OnAuthorizationAsync(context);

        var result = Assert.IsType<JsonResult>(context.Result);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task PermissionAttribute_AllowsWildcardGraphAccess()
    {
        var user = KnowledgeGraphTestSupport.CreateCurrentUser("*");
        var context = KnowledgeGraphTestSupport.CreateAuthorizationContext(user, "/api/ai/knowledge/graph/nodes");
        var permission = new PermissionAttribute(PermissionCodes.AiKnowledgeGraphEdit);

        await permission.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }
}
