using System.Security.Claims;
using System.Text.Json;
using AsterERP.Api.Application.ApplicationConsole;
using AsterERP.Api.Application.Auth;
using AsterERP.Api.Modules.Ai.Flowise;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Api.Modules.Platform;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Shared;
using Hangfire;
using Microsoft.AspNetCore.Http;
using SqlSugar;
using Volo.Abp.Security.Claims;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseScheduleExecutionRunner(
    ISqlSugarClient db,
    IFlowiseExecutionService executionService,
    IHttpContextAccessor httpContextAccessor,
    IFlowiseScheduleScheduler scheduler,
    IDataPermissionFilterRegistrar dataPermissionFilterRegistrar,
    ApplicationDatabaseBindingResolver databaseBindingResolver,
    IApplicationDatabaseConnectionFactory connectionFactory,
    ApplicationDatabaseSchemaInitializer schemaInitializer)
{
    public async Task ExecuteAsync(FlowiseScheduleExecutionJobArgs args)
    {
        var previousContext = httpContextAccessor.HttpContext;
        var context = new DefaultHttpContext
        {
            User = CreateSchedulePrincipal(args)
        };
        httpContextAccessor.HttpContext = context;
        try
        {
            using var filterScope = await dataPermissionFilterRegistrar.RegisterAsync(CancellationToken.None);
            var record = await db.Queryable<FlowiseScheduleRecordEntity>()
                .Where(item => !item.IsDeleted && item.Id == args.ScheduleRecordId && item.TenantId == args.TenantId && item.AppCode == args.AppCode && item.OwnerUserId == args.OwnerUserId)
                .FirstAsync();
            if (record is null)
            {
                return;
            }

            var now = DateTime.UtcNow;
            if (!record.Enabled || record.EndDate is not null && record.EndDate.Value <= now)
            {
                await scheduler.ApplyAsync(record);
                return;
            }

            await EnsureApplicationDatabaseBaselineAsync(record);
            var chatflow = await db.Queryable<FlowiseChatFlowEntity>()
                .Where(item => !item.IsDeleted && item.Id == record.TargetId)
                .FirstAsync();
            if (chatflow is null)
            {
                await scheduler.ApplyAsync(record);
                return;
            }

            var request = new FlowiseExecutionStartRequest
            {
                ResourceId = chatflow.Id,
                Question = record.DefaultInput,
                InputJson = BuildInputJson(record, now),
                Form = ReadForm(record.DefaultForm)
            };
            await executionService.StartAsync(request, CancellationToken.None);
        }
        finally
        {
            httpContextAccessor.HttpContext = previousContext;
        }
    }

    private async Task EnsureApplicationDatabaseBaselineAsync(FlowiseScheduleRecordEntity record)
    {
        var tenantApp = (await db.Queryable<SystemTenantAppEntity>()
            .Where(item =>
                item.TenantId == record.TenantId &&
                item.AppCode == record.AppCode &&
                !item.IsDeleted &&
                item.Status == "Enabled")
            .Take(1)
            .ToListAsync())
            .FirstOrDefault();
        var owner = (await db.Queryable<SystemUserEntity>()
            .Where(item => item.Id == record.OwnerUserId && !item.IsDeleted)
            .Take(1)
            .ToListAsync())
            .FirstOrDefault();
        if (tenantApp is null || owner is null)
        {
            return;
        }

        var binding = databaseBindingResolver.Resolve(tenantApp.ConfigJson, record.TenantId, record.AppCode)
            ?? throw new InvalidOperationException($"应用数据库绑定不存在：{record.TenantId}/{record.AppCode}");
        using var appDb = new DisposableApplicationDb(connectionFactory.Create(binding));
        await schemaInitializer.EnsureBaselineAsync(
            appDb.Client,
            record.TenantId,
            record.AppCode,
            owner,
            CancellationToken.None,
            tenantApp.ConfigJson);
    }

    private static ClaimsPrincipal CreateSchedulePrincipal(FlowiseScheduleExecutionJobArgs args)
    {
        var claims = new[]
        {
            new Claim(AbpClaimTypes.UserId, args.OwnerUserId),
            new Claim(AbpClaimTypes.UserName, args.OwnerUserId),
            new Claim(AsterErpClaimTypes.UserId, args.OwnerUserId),
            new Claim(AsterErpClaimTypes.TenantId, args.TenantId),
            new Claim(AsterErpClaimTypes.AppCode, args.AppCode),
            new Claim(AsterErpClaimTypes.DataScope, "SELF"),
            new Claim(AsterErpClaimTypes.PermissionCode, PermissionCodes.FlowiseRun),
            new Claim(AsterErpClaimTypes.PermissionCode, PermissionCodes.FlowiseView)
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, AsterErpClaimsPrincipalFactory.AuthenticationType, AbpClaimTypes.UserName, AbpClaimTypes.Role));
    }

    private static string BuildInputJson(FlowiseScheduleRecordEntity record, DateTime scheduledAt) =>
        JsonSerializer.Serialize(new
        {
            chatType = "schedule",
            scheduledAt,
            form = ReadForm(record.DefaultForm)
        });

    private static Dictionary<string, object?> ReadForm(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(rawJson) ??
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
