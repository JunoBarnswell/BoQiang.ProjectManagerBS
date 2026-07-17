using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.Ai;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SqlSugar;
using Volo.Abp.AspNetCore.Security.Claims;
using Volo.Abp.Users;

namespace AsterERP.Api.Tests.Ai.KnowledgeGraph;

internal static class KnowledgeGraphTestSupport
{
    public static SqlSugarClient CreateDb(string databasePath) =>
        new(new ConnectionConfig
        {
            ConnectionString = $"Data Source={databasePath}",
            DbType = DbType.Sqlite,
            InitKeyType = InitKeyType.Attribute,
            IsAutoCloseConnection = true
        });

    public static void InitGraphTables(ISqlSugarClient db)
    {
        db.CodeFirst.InitTables<AiKnowledgeSourceEntity, AiKnowledgeDocumentEntity, AiKnowledgeChunkEntity>();
        db.CodeFirst.InitTables<
            AiKnowledgeGraphNodeTypeEntity,
            AiKnowledgeGraphRelationTypeEntity,
            AiKnowledgeGraphNodeEntity,
            AiKnowledgeGraphEdgeEntity,
            AiKnowledgeGraphEvidenceEntity>();
        db.CodeFirst.InitTables<AiKnowledgeGraphBuildJobEntity>();
    }

    public static async Task SeedTypesAsync(
        ISqlSugarClient db,
        IReadOnlyList<string> nodeTypes,
        IReadOnlyList<string> relationTypes)
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

    public static ICurrentUser CreateCurrentUser(params string[] permissions)
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
            permissions,
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

    public static AuthorizationFilterContext CreateAuthorizationContext(ICurrentUser currentUser, string path)
    {
        var services = new ServiceCollection()
            .AddSingleton(currentUser)
            .AddLogging()
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };
        httpContext.Request.Path = path;

        return new AuthorizationFilterContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            []);
    }
}
