using AsterERP.Api.Application.Ai;
using AsterERP.Api.Application.Ai.Agent;
using AsterERP.Api.Application.Ai.Tools;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.Ai;
using AsterERP.Api.Modules.System.Announcements;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using SqlSugar;
using Volo.Abp.AspNetCore.Security.Claims;
using Volo.Abp.Users;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class AiAgentExecutionServiceTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"astererp-ai-agent-tests-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task ExecuteAsync_moves_user_manual_tasks_to_waiting_user_without_blocking_plan()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<AiTaskPlanEntity, AiTaskPlanItemEntity, AiTaskPlanEventEntity, AiAuditEventEntity>();

        var plan = new AiTaskPlanEntity
        {
            TenantId = "tenant-system",
            AppCode = "SYSTEM",
            OwnerUserId = "admin",
            ConversationId = "conv-001",
            Title = "AI 重构验收计划",
            Goal = "人工验收",
            Status = AiTaskPlanConstants.PlanStatus.Approved,
            ApprovedBy = "admin",
            ApprovedAt = DateTime.UtcNow
        };
        await db.Insertable(plan).ExecuteCommandAsync();

        var taskTypes = new[]
        {
            AiTaskPlanConstants.TaskType.Review,
            AiTaskPlanConstants.TaskType.Test,
            AiTaskPlanConstants.TaskType.Manual
        };
        var items = Enumerable.Range(1, 3)
            .Select(index => new AiTaskPlanItemEntity
            {
                TenantId = plan.TenantId,
                AppCode = plan.AppCode,
                OwnerUserId = plan.OwnerUserId,
                ConversationId = plan.ConversationId,
                PlanId = plan.Id,
                Title = $"人工验收任务 {index}",
                Description = "人工验收",
                OwnerType = AiTaskPlanConstants.OwnerType.User,
                TaskType = taskTypes[index - 1],
                Status = AiTaskPlanConstants.ItemStatus.Pending,
                SortOrder = index
            })
            .ToArray();
        await db.Insertable(items).ExecuteCommandAsync();

        var service = new AiAgentExecutionService(
            db,
            new AiTaskPlanGuard(),
            new AiTaskPlanEventWriter(db, new AiWorkspaceContext(CreateCurrentUser())),
            CreateToolExecutor(db));

        var result = await service.ExecuteAsync(
            plan.Id,
            "run-001",
            userInstruction: "执行已批准计划，仅推进人工验收任务，不执行写入操作。");

        var reloadedPlan = await db.Queryable<AiTaskPlanEntity>().FirstAsync(item => item.Id == plan.Id);
        var reloadedItems = await db.Queryable<AiTaskPlanItemEntity>().Where(item => item.PlanId == plan.Id).ToListAsync();
        var events = await db.Queryable<AiTaskPlanEventEntity>().Where(item => item.PlanId == plan.Id).ToListAsync();

        Assert.Equal(AiTaskPlanConstants.PlanStatus.PartialCompleted, result.PlanStatus);
        Assert.Equal(AiTaskPlanConstants.PlanStatus.PartialCompleted, reloadedPlan.Status);
        Assert.All(reloadedItems, item =>
        {
            Assert.Equal(AiTaskPlanConstants.ItemStatus.WaitingUser, item.Status);
            Assert.Null(item.BlockedReason);
            Assert.Null(item.ErrorCode);
            Assert.Null(item.ErrorMessage);
        });
        Assert.Contains(events, item => item.EventName == AiTaskPlanConstants.Event.ExecutionQueueBuilt);
        Assert.Equal(3, events.Count(item => item.EventName == AiTaskPlanConstants.Event.TaskWaitingUser));
    }

    [Fact]
    public async Task Published_announcement_status_filter_uses_sql_translatable_nullable_comparisons()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<SystemAnnouncementEntity>();
        var now = DateTime.UtcNow;

        SystemAnnouncementEntity[] announcements =
        [
            new SystemAnnouncementEntity
            {
                Title = "published",
                Content = "published",
                Status = "Published",
                ExpiresAt = null,
                PublishedAt = now.AddMinutes(-10)
            },
            new SystemAnnouncementEntity
            {
                Title = "expired",
                Content = "expired",
                Status = "Published",
                ExpiresAt = now.AddMinutes(-1),
                PublishedAt = now.AddMinutes(-20)
            }
        ];
        await db.Insertable(announcements).ExecuteCommandAsync();

        var published = await db.Queryable<SystemAnnouncementEntity>()
            .Where(item => item.Status == "Published" && (item.ExpiresAt == null || item.ExpiresAt > now))
            .ToListAsync();
        var expired = await db.Queryable<SystemAnnouncementEntity>()
            .Where(item => item.Status == "Published" && item.ExpiresAt != null && item.ExpiresAt <= now)
            .ToListAsync();

        Assert.Single(published);
        Assert.Equal("published", published[0].Title);
        Assert.Single(expired);
        Assert.Equal("expired", expired[0].Title);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
        catch (IOException)
        {
        }
    }

    private SqlSugarClient CreateDb() =>
        new(new ConnectionConfig
        {
            ConnectionString = $"Data Source={_databasePath}",
            DbType = DbType.Sqlite,
            InitKeyType = InitKeyType.Attribute,
            IsAutoCloseConnection = true
        });

    private static AiKernelFunctionService CreateToolExecutor(ISqlSugarClient db)
    {
        var currentUser = CreateCurrentUser();
        return new AiKernelFunctionService(
            db,
            new AiWorkspaceContext(currentUser),
            new AiKernelFunctionCatalog(Array.Empty<IAiKernelFunction>()),
            new AiKernelFunctionArgumentNormalizer(),
            new AiKernelFunctionPermissionFilter(currentUser),
            new AiKernelFunctionArgumentRedactor(),
            LoggerFactory.Create(_ => { }),
            Array.Empty<IFunctionInvocationFilter>(),
            Array.Empty<IAutoFunctionInvocationFilter>(),
            Array.Empty<IPromptRenderFilter>());
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
