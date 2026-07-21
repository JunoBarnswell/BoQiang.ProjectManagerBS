using AsterERP.Api.Modules.System.Dicts;
using AsterERP.Api.Modules.System.CodeRules;
using AsterERP.Api.Modules.System.Menus;
using AsterERP.Api.Modules.System.Organizations;
using AsterERP.Api.Modules.System.Parameters;
using AsterERP.Api.Modules.System.Permissions;
using AsterERP.Api.Modules.System.Roles;
using AsterERP.Api.Modules.System.ScheduledJobs;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Api.Modules.Workflows;
using AsterERP.Api.Modules.Platform;
using AsterERP.Api.Modules.Runtime;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Application.ApplicationConsole;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Shared;
using System.Text.Json;
using System.Text.Json.Nodes;
using SqlSugar;
using Microsoft.Extensions.Options;

namespace AsterERP.Api.Infrastructure.Abp.DevelopmentSeed;

public sealed class DevelopmentSeedDataService(
    ISqlSugarClient db,
    ILogger<DevelopmentSeedDataService> logger,
    IPasswordHashService passwordHashService,
    IOptions<DevelopmentSeedOptions> options) : IDevelopmentSeedService
{
    private readonly IReadOnlyDictionary<string, string> userPasswords = options.Value.UserPasswords;
    private Dictionary<string, SystemMenuEntity>? menuCache;
    private Dictionary<string, SystemDataModelEntity>? dataModelCache;

    public Task SeedAsync(CancellationToken cancellationToken = default)
    {
        RunSeedStep("permission-codes", SeedPermissionCodes);
        RunSeedStep("platform-foundation", SeedPlatformFoundation);
        RunSeedStep("organizations", SeedOrganizations);
        RunSeedStep("roles", SeedRoles);
        RunSeedStep("users", SeedUsers);
        RunSeedStep("user-roles", SeedUserRoles);
        RunSeedStep("user-tenant-memberships", SeedUserTenantMemberships);
        RunSeedStep("user-app-roles", SeedUserAppRoles);
        RunSeedStep("role-permissions", SeedRolePermissions);
        RunSeedStep("data-models", SeedDataModels);
        RunSeedStep("menus", SeedMenus);
        RunSeedStep("dicts", SeedDicts);
        RunSeedStep("code-rules", SeedCodeRules);
        RunSeedStep("parameters", SeedParameters);
        RunSeedStep("scheduled-jobs", SeedScheduledJobs);
        RunSeedStep("workflow-notifications", SeedWorkflowNotifications);

        logger.LogInformation("Database seed completed");
        return Task.CompletedTask;
    }

    private void RunSeedStep(string stepName, Action action)
    {
        logger.LogInformation("Development seed step {StepName} started", stepName);
        action();
        logger.LogInformation("Development seed step {StepName} completed", stepName);
    }

    private void SeedPermissionCodes()
    {
        var codes = new[]
        {
            new SystemPermissionCodeEntity { ModuleName = "Auth", PermissionCode = "system:auth:me", PermissionName = "Session Me" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:dict:query", PermissionName = "Dict Query" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:dict:add", PermissionName = "Dict Add" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:dict:edit", PermissionName = "Dict Edit" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:dict:delete", PermissionName = "Dict Delete" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:menu:query", PermissionName = "Menu Query" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:menu:add", PermissionName = "Menu Add" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:menu:edit", PermissionName = "Menu Edit" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:menu:delete", PermissionName = "Menu Delete" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:role:query", PermissionName = "Role Query" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:role:add", PermissionName = "Role Add" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:role:edit", PermissionName = "Role Edit" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:role:delete", PermissionName = "Role Delete" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:role:grant", PermissionName = "Role Grant" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:user:query", PermissionName = "User Query" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:user:add", PermissionName = "User Add" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:user:edit", PermissionName = "User Edit" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:user:delete", PermissionName = "User Delete" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:user:grant-role", PermissionName = "User Grant Role" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:user:reset-password", PermissionName = "User Reset Password" },
            new SystemPermissionCodeEntity { ModuleName = "Organization", PermissionCode = "system:dept:query", PermissionName = "Department Query" },
            new SystemPermissionCodeEntity { ModuleName = "Organization", PermissionCode = "system:dept:add", PermissionName = "Department Add" },
            new SystemPermissionCodeEntity { ModuleName = "Organization", PermissionCode = "system:dept:edit", PermissionName = "Department Edit" },
            new SystemPermissionCodeEntity { ModuleName = "Organization", PermissionCode = "system:dept:delete", PermissionName = "Department Delete" },
            new SystemPermissionCodeEntity { ModuleName = "Organization", PermissionCode = "system:position:query", PermissionName = "Position Query" },
            new SystemPermissionCodeEntity { ModuleName = "Organization", PermissionCode = "system:position:add", PermissionName = "Position Add" },
            new SystemPermissionCodeEntity { ModuleName = "Organization", PermissionCode = "system:position:edit", PermissionName = "Position Edit" },
            new SystemPermissionCodeEntity { ModuleName = "Organization", PermissionCode = "system:position:delete", PermissionName = "Position Delete" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:parameter:query", PermissionName = "Parameter Query" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:parameter:add", PermissionName = "Parameter Add" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:parameter:edit", PermissionName = "Parameter Edit" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:parameter:delete", PermissionName = "Parameter Delete" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:abp-setting:query", PermissionName = "ABP Infrastructure Setting Query" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:abp-setting:edit", PermissionName = "ABP Infrastructure Setting Edit" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:abp-setting:test", PermissionName = "ABP Infrastructure Setting Test" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:code-rule:query", PermissionName = "Code Rule Query" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:code-rule:generate", PermissionName = "Code Rule Generate" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:operation-log:query", PermissionName = "Operation Log Query" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:login-log:query", PermissionName = "Login Log Query" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:online-user:query", PermissionName = "Online User Query" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:online-user:kick", PermissionName = "Online User Kick" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:scheduled-job:query", PermissionName = "Scheduled Job Query" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:scheduled-job:add", PermissionName = "Scheduled Job Add" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:scheduled-job:edit", PermissionName = "Scheduled Job Edit" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:scheduled-job:delete", PermissionName = "Scheduled Job Delete" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:scheduled-job:trigger", PermissionName = "Scheduled Job Trigger" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:scheduled-job:log", PermissionName = "Scheduled Job Log" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:announcement:query", PermissionName = "Announcement Query" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:announcement:add", PermissionName = "Announcement Add" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:announcement:edit", PermissionName = "Announcement Edit" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:announcement:delete", PermissionName = "Announcement Delete" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:announcement:publish", PermissionName = "Announcement Publish" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:announcement:withdraw", PermissionName = "Announcement Withdraw" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:announcement:top", PermissionName = "Announcement Top" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:file:query", PermissionName = "File Query" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:file:upload", PermissionName = "File Upload" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:file:preview", PermissionName = "File Preview" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:file:download", PermissionName = "File Download" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:file:delete", PermissionName = "File Delete" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:print:query", PermissionName = "Print Center Query" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:print:add", PermissionName = "Print Template Add" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:print:edit", PermissionName = "Print Template Edit" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:print:delete", PermissionName = "Print Template Delete" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:print:publish", PermissionName = "Print Template Publish" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:print:use", PermissionName = "Print Template Use" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:print:default", PermissionName = "Print Template Set Default" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:excel:manage", PermissionName = "Excel Manage" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:notification:broadcast", PermissionName = "Notification Broadcast" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:query-view:query", PermissionName = "Query View Query" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:query-view:export", PermissionName = "Query View Export" },
            new SystemPermissionCodeEntity { ModuleName = "System", PermissionCode = "system:query-view:task", PermissionName = "Query View Task" },
            new SystemPermissionCodeEntity { ModuleName = "Runtime", PermissionCode = "runtime:data:query", PermissionName = "Runtime Data Query" },
            new SystemPermissionCodeEntity { ModuleName = "Runtime", PermissionCode = ApplicationRuntimeDataModelCatalog.RuntimeConfigurationPermission, PermissionName = "Runtime Configuration Query" },
            new SystemPermissionCodeEntity { ModuleName = "Runtime", PermissionCode = "runtime:grid-view:save-user", PermissionName = "Runtime Grid View Save User" },
            new SystemPermissionCodeEntity { ModuleName = "Runtime", PermissionCode = "runtime:grid-view:save-tenant", PermissionName = "Runtime Grid View Save Tenant" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:model:query", PermissionName = "Workflow Model Query" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:model:add", PermissionName = "Workflow Model Add" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:model:edit", PermissionName = "Workflow Model Edit" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:model:delete", PermissionName = "Workflow Model Delete" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:model:publish", PermissionName = "Workflow Model Publish" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:model:suspend", PermissionName = "Workflow Model Suspend" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:deployment:query", PermissionName = "Workflow Deployment Query" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:deployment:resource", PermissionName = "Workflow Deployment Resource" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:binding:query", PermissionName = "Workflow Binding Query" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:binding:edit", PermissionName = "Workflow Binding Edit" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:binding:delete", PermissionName = "Workflow Binding Delete" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:form:query", PermissionName = "Workflow Form Query" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:draft:query", PermissionName = "Workflow Draft Query" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:draft:edit", PermissionName = "Workflow Draft Edit" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:draft:delete", PermissionName = "Workflow Draft Delete" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:draft:submit", PermissionName = "Workflow Draft Submit" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:category:query", PermissionName = "Workflow Category Query" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:category:edit", PermissionName = "Workflow Category Edit" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:category:delete", PermissionName = "Workflow Category Delete" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:instance:query", PermissionName = "Workflow Instance Query" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:instance:start", PermissionName = "Workflow Instance Start" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:instance:withdraw", PermissionName = "Workflow Instance Withdraw" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:instance:terminate", PermissionName = "Workflow Instance Terminate" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:instance:variable", PermissionName = "Workflow Instance Variable" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:task:query", PermissionName = "Workflow Task Query" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:task:claim", PermissionName = "Workflow Task Claim" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:task:approve", PermissionName = "Workflow Task Approve" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:task:transfer", PermissionName = "Workflow Task Transfer" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:task:delegate", PermissionName = "Workflow Task Delegate" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:task:attachment", PermissionName = "Workflow Task Attachment" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:task:comment", PermissionName = "Workflow Task Comment" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:history:query", PermissionName = "Workflow History Query" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:report:query", PermissionName = "Workflow Report Query" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:participant:query", PermissionName = "Workflow Participant Query" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:delegation:query", PermissionName = "Workflow Delegation Query" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:delegation:edit", PermissionName = "Workflow Delegation Edit" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:delegation:delete", PermissionName = "Workflow Delegation Delete" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:calendar:query", PermissionName = "Workflow Calendar Query" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:calendar:edit", PermissionName = "Workflow Calendar Edit" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:calendar:delete", PermissionName = "Workflow Calendar Delete" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:notification:channel:query", PermissionName = "Workflow Notification Channel Query" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:notification:channel:edit", PermissionName = "Workflow Notification Channel Edit" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:notification:channel:delete", PermissionName = "Workflow Notification Channel Delete" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:notification:template:query", PermissionName = "Workflow Notification Template Query" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:notification:template:edit", PermissionName = "Workflow Notification Template Edit" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:notification:template:delete", PermissionName = "Workflow Notification Template Delete" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:notification:rule:query", PermissionName = "Workflow Notification Rule Query" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:notification:rule:edit", PermissionName = "Workflow Notification Rule Edit" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:notification:rule:delete", PermissionName = "Workflow Notification Rule Delete" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:notification:task:query", PermissionName = "Workflow Notification Task Query" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:notification:task:send", PermissionName = "Workflow Notification Task Send" },
            new SystemPermissionCodeEntity { ModuleName = "Workflow", PermissionCode = "workflow:notification:log:query", PermissionName = "Workflow Notification Log Query" },
            new SystemPermissionCodeEntity { ModuleName = "Auth", PermissionCode = "system:auth:workspace", PermissionName = "Auth Workspace" },
            new SystemPermissionCodeEntity { ModuleName = "Platform", PermissionCode = "platform:tenant:query", PermissionName = "Tenant Query" },
            new SystemPermissionCodeEntity { ModuleName = "Platform", PermissionCode = "platform:tenant:add", PermissionName = "Tenant Add" },
            new SystemPermissionCodeEntity { ModuleName = "Platform", PermissionCode = "platform:tenant:edit", PermissionName = "Tenant Edit" },
            new SystemPermissionCodeEntity { ModuleName = "Platform", PermissionCode = "platform:tenant:enable", PermissionName = "Tenant Enable" },
            new SystemPermissionCodeEntity { ModuleName = "Platform", PermissionCode = "platform:tenant:disable", PermissionName = "Tenant Disable" },
            new SystemPermissionCodeEntity { ModuleName = "Platform", PermissionCode = "platform:tenant:delete", PermissionName = "Tenant Delete" },
            new SystemPermissionCodeEntity { ModuleName = "Platform", PermissionCode = "platform:application:query", PermissionName = "Application Query" },
            new SystemPermissionCodeEntity { ModuleName = "Platform", PermissionCode = "platform:application:add", PermissionName = "Application Add" },
            new SystemPermissionCodeEntity { ModuleName = "Platform", PermissionCode = "platform:application:edit", PermissionName = "Application Edit" },
            new SystemPermissionCodeEntity { ModuleName = "Platform", PermissionCode = "platform:application:enable", PermissionName = "Application Enable" },
            new SystemPermissionCodeEntity { ModuleName = "Platform", PermissionCode = "platform:application:disable", PermissionName = "Application Disable" },
            new SystemPermissionCodeEntity { ModuleName = "Platform", PermissionCode = "platform:application:delete", PermissionName = "Application Delete" },
            new SystemPermissionCodeEntity { ModuleName = "Platform", PermissionCode = "platform:application:enter", PermissionName = "Application Enter Backend" },
            new SystemPermissionCodeEntity { ModuleName = "Platform", PermissionCode = "platform:application:publish", PermissionName = "Application Publish" },
            new SystemPermissionCodeEntity { ModuleName = "Platform", PermissionCode = "platform:application:publish-task", PermissionName = "Application Publish Task" },
            new SystemPermissionCodeEntity { ModuleName = "Platform", PermissionCode = "platform:application:publish-log", PermissionName = "Application Publish Log" },
            new SystemPermissionCodeEntity { ModuleName = "Platform", PermissionCode = "platform:application:publish-artifact-download", PermissionName = "Application Publish Artifact Download" },
            new SystemPermissionCodeEntity { ModuleName = "Platform", PermissionCode = "platform:application:publish-artifact-delete", PermissionName = "Application Publish Artifact Delete" },
            new SystemPermissionCodeEntity { ModuleName = "Platform", PermissionCode = "platform:tenant-app:query", PermissionName = "Tenant App Query" },
            new SystemPermissionCodeEntity { ModuleName = "Platform", PermissionCode = "platform:tenant-app:install", PermissionName = "Tenant App Install" },
            new SystemPermissionCodeEntity { ModuleName = "Platform", PermissionCode = "platform:tenant-app:enable", PermissionName = "Tenant App Enable" },
            new SystemPermissionCodeEntity { ModuleName = "Platform", PermissionCode = "platform:tenant-app:disable", PermissionName = "Tenant App Disable" },
            new SystemPermissionCodeEntity { ModuleName = "Platform", PermissionCode = "platform:tenant-app:uninstall", PermissionName = "Tenant App Uninstall" },
            new SystemPermissionCodeEntity { ModuleName = "Platform", PermissionCode = "platform:user-tenant:query", PermissionName = "User Tenant Query" },
            new SystemPermissionCodeEntity { ModuleName = "Platform", PermissionCode = "platform:user-tenant:edit", PermissionName = "User Tenant Edit" },
            new SystemPermissionCodeEntity { ModuleName = "Platform", PermissionCode = "platform:user-app-role:query", PermissionName = "User App Role Query" },
            new SystemPermissionCodeEntity { ModuleName = "Platform", PermissionCode = "platform:user-app-role:edit", PermissionName = "User App Role Edit" },
            new SystemPermissionCodeEntity { ModuleName = "Tenant", PermissionCode = "tenant:app:query", PermissionName = "Tenant App Query" },
            new SystemPermissionCodeEntity { ModuleName = "Tenant", PermissionCode = "tenant:app:install", PermissionName = "Tenant App Install" },
            new SystemPermissionCodeEntity { ModuleName = "Tenant", PermissionCode = "tenant:app:enable", PermissionName = "Tenant App Enable" },
            new SystemPermissionCodeEntity { ModuleName = "Tenant", PermissionCode = "tenant:app:disable", PermissionName = "Tenant App Disable" }
        };

        codes = codes
            .Concat(BuildApplicationDataCenterPermissionSeedCodes())
            .Concat(BuildImPermissionSeedCodes())
            .Concat(BuildProjectManagementPermissionSeedCodes())
            .GroupBy(item => item.PermissionCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        logger.LogInformation("Development seed permission-codes prepared {Count} codes", codes.Length);
        var permissionCodes = codes.Select(item => item.PermissionCode).ToArray();
        logger.LogInformation("Development seed permission-codes querying existing codes");
        var existingByCode = db.Queryable<SystemPermissionCodeEntity>()
            .Where(item => permissionCodes.Contains(item.PermissionCode))
            .ToList()
            .ToDictionary(item => item.PermissionCode, StringComparer.OrdinalIgnoreCase);
        logger.LogInformation("Development seed permission-codes found {Count} existing codes", existingByCode.Count);
        var inserts = new List<SystemPermissionCodeEntity>();

        foreach (var code in codes)
        {
            if (!existingByCode.ContainsKey(code.PermissionCode))
            {
                inserts.Add(code);
            }
        }

        if (inserts.Count > 0)
        {
            logger.LogInformation("Development seed permission-codes inserting {Count} codes", inserts.Count);
            db.Insertable(inserts).ExecuteCommand();
        }

        RetireLegacyProtectionPermissionCodes();
        RetireMainDatabaseApplicationConsolePermissionCodes();
    }

    private static IEnumerable<SystemPermissionCodeEntity> BuildApplicationDataCenterPermissionSeedCodes()
    {
        return PermissionCodes.AppDataCenterPermissionCodes.Select(code => new SystemPermissionCodeEntity
        {
            ModuleName = "Application Data Center",
            PermissionCode = code,
            PermissionName = BuildPermissionDisplayName(code)
        });
    }

    private static IEnumerable<SystemPermissionCodeEntity> BuildImPermissionSeedCodes()
    {
        return new[]
        {
            PermissionCodes.ImConversationView,
            PermissionCodes.ImConversationCreate,
            PermissionCodes.ImMessageSend,
            PermissionCodes.ImMessageRead,
            PermissionCodes.ImUserSearch
        }.Select(code => new SystemPermissionCodeEntity
        {
            ModuleName = "IM",
            PermissionCode = code,
            PermissionName = BuildPermissionDisplayName(code)
        });
    }

    private static IEnumerable<SystemPermissionCodeEntity> BuildProjectManagementPermissionSeedCodes()
    {
        return ProjectManagementPlatformPermissionCatalog.Definitions.Select(definition => new SystemPermissionCodeEntity
        {
            ModuleName = "ProjectManagement",
            PermissionCode = definition.PermissionCode,
            PermissionName = definition.PermissionName
        });
    }

    private static string BuildPermissionDisplayName(string permissionCode)
    {
        return permissionCode
            .Replace("app:data-center:", "Data Center ", StringComparison.OrdinalIgnoreCase)
            .Replace(":", " ", StringComparison.Ordinal);
    }

    private void SeedPlatformFoundation()
    {
        UpsertTenant("tenant-system", "SYSTEM", "默认租户", "默认", "Enabled", null, "平台管理员", "400-800-0000", null);
        UpsertTenant("tenant-a", "TENANT_A", "客户A", "客户A", "Enabled", null, "客户A管理员", "13800000001", null);
        UpsertTenant("tenant-b", "TENANT_B", "客户B", "客户B", "Enabled", null, "客户B管理员", "13800000002", null);

        UpsertApplication("SYSTEM", "系统管理", "Platform", "Settings", "/home", "/home", null, "Enabled", "1.0.0", "平台系统管理应用");
        UpsertApplication("WMS", "仓储管理系统", "Business", "Boxes", "/console", "/console", "/runtime", "Enabled", "1.0.0", "仓储管理应用");
        UpsertApplication("MES", "制造执行系统", "Business", "Factory", "/console", "/console", "/runtime", "Enabled", "1.0.0", "制造执行应用");

        UpsertTenantApp("tenant-system", "SYSTEM", "Enabled", "AsterERP 系统管理", null, null, "#1677ff", null, null);
        UpsertTenantApp("tenant-a", "WMS", "Enabled", "客户A WMS", null, null, "#1677ff", null, ResolveDevelopmentTenantAppConfig("tenant-a", "WMS", "客户A WMS 应用库", "wms.db"));
        UpsertTenantApp(
            "tenant-a",
            "MES",
            "Enabled",
            "客户A MES",
            null,
            null,
            "#16a34a",
            null,
            ResolveDevelopmentTenantAppConfig(
                "tenant-a",
                "MES",
                "客户A MES mes11 应用库",
                "mes11.db",
                ApplicationShellCapabilityResolver.AiCapability,
                ApplicationShellCapabilityResolver.SystemAdministrationCapability,
                ApplicationShellCapabilityResolver.WorkflowCapability));
        UpsertTenantApp("tenant-b", "WMS", "Enabled", "客户B WMS", null, null, "#7c3aed", null, ResolveDevelopmentTenantAppConfig("tenant-b", "WMS", "客户B WMS 应用库", "wms.db"));
    }

    private void RetireMainDatabaseApplicationConsolePermissionCodes()
    {
        var appPermissionCodes = PermissionCodes.AppConsolePermissionCodes.ToArray();
        var existingPermissionCodes = db.Queryable<SystemPermissionCodeEntity>()
            .Where(item => appPermissionCodes.Contains(item.PermissionCode) && !item.IsDeleted)
            .ToList();
        if (existingPermissionCodes.Count == 0)
        {
            return;
        }

        var permissionCodeIds = existingPermissionCodes.Select(item => item.Id).ToArray();
        var rolePermissions = db.Queryable<SystemRolePermissionEntity>()
            .Where(item => permissionCodeIds.Contains(item.PermissionCodeId) && !item.IsDeleted)
            .ToList();
        var now = DateTime.UtcNow;

        foreach (var rolePermission in rolePermissions)
        {
            rolePermission.IsDeleted = true;
            rolePermission.DeletedTime = now;
            rolePermission.UpdatedTime = now;
        }

        foreach (var permissionCode in existingPermissionCodes)
        {
            permissionCode.IsDeleted = true;
            permissionCode.IsEnabled = false;
            permissionCode.DeletedTime = now;
            permissionCode.UpdatedTime = now;
        }

        if (rolePermissions.Count > 0)
        {
            db.Updateable(rolePermissions)
                .UpdateColumns(item => new { item.IsDeleted, item.DeletedTime, item.UpdatedTime })
                .ExecuteCommand();
        }

        db.Updateable(existingPermissionCodes)
            .UpdateColumns(item => new { item.IsDeleted, item.IsEnabled, item.DeletedTime, item.UpdatedTime })
            .ExecuteCommand();

        logger.LogInformation(
            "Development seed retired {PermissionCount} main database application-console permission codes and {RolePermissionCount} role links",
            existingPermissionCodes.Count,
            rolePermissions.Count);
    }

    private void SeedRoles()
    {
        UpsertRole("admin", "System Admin", "ALL", true, "tenant-system", "SYSTEM");
        UpsertRole("menu_manager", "Menu Manager", "ALL", true, "tenant-system", "SYSTEM");
        UpsertRole("user_manager", "User Manager", "DEPT_AND_CHILD", true, "tenant-system", "SYSTEM");
        UpsertRole("org_manager", "Organization Manager", "DEPT_AND_CHILD", true, "tenant-system", "SYSTEM");
        UpsertRole("finance", "Finance Manager", "DEPT_AND_CHILD", true, "tenant-system", "SYSTEM");
        UpsertRole("auditor", "Auditor", "SELF", true, "tenant-system", "SYSTEM");
        UpsertRole("wf_starter", "Workflow Starter", "SELF", true, "tenant-system", "SYSTEM");
        UpsertRole("wf_user_approver", "Workflow User Approver", "SELF", true, "tenant-system", "SYSTEM");
        UpsertRole("wf_role_approver", "Workflow Role Approver", "SELF", true, "tenant-system", "SYSTEM");
        UpsertRole("wf_dept_approver", "Workflow Department Approver", "DEPT", true, "tenant-system", "SYSTEM");
        UpsertRole("wf_delegate", "Workflow Delegate", "SELF", true, "tenant-system", "SYSTEM");
        UpsertRole("wf_no_permission", "Workflow No Permission", "SELF", true, "tenant-system", "SYSTEM");
        UpsertRole("wms_admin", "WMS Admin", "ALL", true, "tenant-a", "WMS");
        UpsertRole("mes_admin", "MES Admin", "ALL", true, "tenant-a", "MES");
        UpsertRole("wms_auditor", "WMS Auditor", "SELF", true, "tenant-b", "WMS");
    }

    private void SeedUsers()
    {
        UpsertUser("admin", "System Admin", GetRequiredPassword("admin"), "root", "general-manager", null, null, true, "Enabled");
        UpsertUser("menu_manager", "Menu Manager", GetRequiredPassword("menu_manager"), "root", "system-admin", null, null, false, "Enabled");
        UpsertUser("user_manager", "User Manager", GetRequiredPassword("user_manager"), "root", "hr-admin", null, null, false, "Enabled");
        UpsertUser("org_manager", "Organization Manager", GetRequiredPassword("org_manager"), "root", "hr-admin", null, null, false, "Enabled");
        UpsertUser("finance", "Finance Manager", GetRequiredPassword("finance"), "finance", "finance-manager", null, null, false, "Enabled");
        UpsertUser("auditor", "Auditor", GetRequiredPassword("auditor"), "audit", "audit-manager", null, null, false, "Enabled");
        UpsertUser("wf_starter", "Workflow Starter", GetRequiredPassword("wf_starter"), "sales", "system-admin", null, "wf_starter@example.local", false, "Enabled");
        UpsertUser("wf_user_approver", "Workflow User Approver", GetRequiredPassword("wf_user_approver"), "root", "system-admin", null, "wf_user_approver@example.local", false, "Enabled");
        UpsertUser("wf_role_approver", "Workflow Role Approver", GetRequiredPassword("wf_role_approver"), "root", "system-admin", null, "wf_role_approver@example.local", false, "Enabled");
        UpsertUser("wf_dept_approver", "Workflow Department Approver", GetRequiredPassword("wf_dept_approver"), "finance", "finance-manager", null, "wf_dept_approver@example.local", false, "Enabled");
        UpsertUser("wf_delegate", "Workflow Delegate", GetRequiredPassword("wf_delegate"), "audit", "audit-manager", null, "wf_delegate@example.local", false, "Enabled");
        UpsertUser("wf_no_permission", "Workflow No Permission", GetRequiredPassword("wf_no_permission"), "root", "system-admin", null, "wf_no_permission@example.local", false, "Enabled");
    }

    private string GetRequiredPassword(string userName) =>
        userPasswords.TryGetValue(userName, out var password) && !string.IsNullOrWhiteSpace(password)
            ? password
            : throw new InvalidOperationException($"DevelopmentSeed:UserPasswords must define '{userName}' in Development/Testing.");

    private void SeedUserRoles()
    {
        EnsureUserRole("admin", "admin");
        EnsureUserRole("menu_manager", "menu_manager");
        EnsureUserRole("user_manager", "user_manager");
        EnsureUserRole("org_manager", "org_manager");
        EnsureUserRole("finance", "finance");
        EnsureUserRole("auditor", "auditor");
        EnsureUserRole("wf_starter", "wf_starter");
        EnsureUserRole("wf_user_approver", "wf_user_approver");
        EnsureUserRole("wf_role_approver", "wf_role_approver");
        EnsureUserRole("wf_dept_approver", "wf_dept_approver");
        EnsureUserRole("wf_delegate", "wf_delegate");
        EnsureUserRole("wf_no_permission", "wf_no_permission");
    }

    private void SeedUserTenantMemberships()
    {
        EnsureUserTenantMembership("admin", "tenant-system", "root", "general-manager", true, true, "Enabled");
        EnsureUserTenantMembership("admin", "tenant-a", "root", "general-manager", true, false, "Enabled");
        EnsureUserTenantMembership("admin", "tenant-b", "audit", "audit-manager", true, false, "Enabled");
        EnsureUserTenantMembership("auditor", "tenant-b", "audit", "audit-manager", false, true, "Enabled");
        EnsureUserTenantMembership("wf_starter", "tenant-system", "sales", "system-admin", false, true, "Enabled");
        EnsureUserTenantMembership("wf_user_approver", "tenant-system", "root", "system-admin", false, true, "Enabled");
        EnsureUserTenantMembership("wf_role_approver", "tenant-system", "root", "system-admin", false, true, "Enabled");
        EnsureUserTenantMembership("wf_dept_approver", "tenant-system", "finance", "finance-manager", false, true, "Enabled");
        EnsureUserTenantMembership("wf_delegate", "tenant-system", "audit", "audit-manager", false, true, "Enabled");
        EnsureUserTenantMembership("wf_no_permission", "tenant-system", "root", "system-admin", false, true, "Enabled");
    }

    private void SeedUserAppRoles()
    {
        EnsureUserAppRole("admin", "tenant-system", "SYSTEM", "admin", true);
        EnsureUserAppRole("admin", "tenant-a", "WMS", "wms_admin", false);
        EnsureUserAppRole("admin", "tenant-a", "MES", "mes_admin", false);
        EnsureUserAppRole("admin", "tenant-b", "WMS", "wms_auditor", false);
        EnsureUserAppRole("auditor", "tenant-b", "WMS", "wms_auditor", true);
        EnsureUserAppRole("wf_starter", "tenant-system", "SYSTEM", "wf_starter", true);
        EnsureUserAppRole("wf_user_approver", "tenant-system", "SYSTEM", "wf_user_approver", true);
        EnsureUserAppRole("wf_role_approver", "tenant-system", "SYSTEM", "wf_role_approver", true);
        EnsureUserAppRole("wf_dept_approver", "tenant-system", "SYSTEM", "wf_dept_approver", true);
        EnsureUserAppRole("wf_delegate", "tenant-system", "SYSTEM", "wf_delegate", true);
        EnsureUserAppRole("wf_no_permission", "tenant-system", "SYSTEM", "wf_no_permission", true);
    }

    private void SeedOrganizations()
    {
        UpsertDepartment("root", "ROOT", "集团总部", null, "总经理", "400-800-0000", 1, "Enabled");
        UpsertDepartment("finance", "FIN", "财务部", "root", "财务经理", "021-10000001", 10, "Enabled");
        UpsertDepartment("purchase", "PUR", "采购部", "root", "采购经理", "021-10000002", 20, "Enabled");
        UpsertDepartment("sales", "SAL", "销售一部", "root", "销售经理", "021-10000003", 30, "Enabled");
        UpsertDepartment("audit", "AUD", "审计部", "root", "审计经理", "021-10000004", 40, "Enabled");

        UpsertPosition("general-manager", "GM", "总经理", "root", "M5", 1, "Enabled");
        UpsertPosition("system-admin", "SYSADMIN", "系统管理员", "root", "M3", 2, "Enabled");
        UpsertPosition("hr-admin", "HRADMIN", "组织管理员", "root", "M3", 3, "Enabled");
        UpsertPosition("finance-manager", "FINMGR", "财务经理", "finance", "M4", 10, "Enabled");
        UpsertPosition("audit-manager", "AUDMGR", "审计主管", "audit", "M3", 20, "Enabled");
    }

    private void SeedRolePermissions()
    {
        var allPermissionCodes = db.Queryable<SystemPermissionCodeEntity>().Where(item => !item.IsDeleted).ToList().Select(item => item.PermissionCode).ToArray();

        UpsertRolePermissions("admin", allPermissionCodes);
        UpsertRolePermissions("admin", FlowiseAdminPermissionCodes());
        UpsertRolePermissions("menu_manager",
            "system:menu:query",
            "system:menu:add",
            "system:menu:edit",
            "system:menu:delete",
            "system:dict:query");
        UpsertRolePermissions("user_manager",
            "system:user:query",
            "system:user:add",
            "system:user:edit",
            "system:user:delete",
            "system:user:grant-role",
            "system:user:reset-password",
            "system:dept:query",
            "system:position:query",
            "system:query-view:query",
            "system:dict:query");
        UpsertRolePermissions("org_manager",
            "system:dept:query",
            "system:dept:add",
            "system:dept:edit",
            "system:dept:delete",
            "system:position:query",
            "system:position:add",
            "system:position:edit",
            "system:position:delete",
            "system:query-view:query");
        UpsertRolePermissions("finance",
            "system:dict:query",
            "system:user:query",
            "system:query-view:query");
        UpsertRolePermissions("auditor",
            "system:dict:query",
            "system:user:query",
            "system:query-view:query",
            "system:operation-log:query",
            "system:login-log:query",
            "system:online-user:query",
            "system:scheduled-job:query",
            "system:scheduled-job:log",
            "system:announcement:query");
        UpsertRolePermissions("wf_starter",
            "system:user:query",
            "system:role:query",
            "system:dept:query",
            "system:position:query",
            "system:query-view:query",
            "workflow:binding:query",
            "workflow:form:query",
            "workflow:draft:query",
            "workflow:draft:edit",
            "workflow:draft:delete",
            "workflow:draft:submit",
            "workflow:deployment:query",
            "workflow:instance:query",
            "workflow:instance:start",
            "workflow:history:query",
            "workflow:delegation:query",
            "workflow:delegation:edit",
            "workflow:delegation:delete",
            "workflow:notification:task:query",
            "workflow:notification:log:query");
        UpsertRolePermissions("wf_user_approver",
            "system:user:query",
            "system:query-view:query",
            "workflow:task:query",
            "workflow:task:claim",
            "workflow:task:approve",
            "workflow:task:transfer",
            "workflow:task:delegate",
            "workflow:task:attachment",
            "workflow:task:comment",
            "workflow:instance:query",
            "workflow:history:query",
            "workflow:participant:query",
            "workflow:delegation:query",
            "workflow:delegation:edit",
            "workflow:delegation:delete",
            "workflow:notification:task:query",
            "workflow:notification:log:query");
        UpsertRolePermissions("wf_role_approver",
            "system:role:query",
            "system:query-view:query",
            "workflow:task:query",
            "workflow:task:claim",
            "workflow:task:approve",
            "workflow:task:transfer",
            "workflow:task:delegate",
            "workflow:task:attachment",
            "workflow:task:comment",
            "workflow:instance:query",
            "workflow:history:query",
            "workflow:participant:query",
            "workflow:delegation:query",
            "workflow:delegation:edit",
            "workflow:delegation:delete",
            "workflow:notification:task:query",
            "workflow:notification:log:query");
        UpsertRolePermissions("wf_dept_approver",
            "system:dept:query",
            "system:query-view:query",
            "workflow:task:query",
            "workflow:task:claim",
            "workflow:task:approve",
            "workflow:task:transfer",
            "workflow:task:delegate",
            "workflow:task:attachment",
            "workflow:task:comment",
            "workflow:instance:query",
            "workflow:history:query",
            "workflow:participant:query",
            "workflow:delegation:query",
            "workflow:delegation:edit",
            "workflow:delegation:delete",
            "workflow:notification:task:query",
            "workflow:notification:log:query");
        UpsertRolePermissions("wf_delegate",
            "workflow:task:query",
            "workflow:task:approve",
            "workflow:task:transfer",
            "workflow:task:delegate",
            "workflow:task:attachment",
            "workflow:task:comment",
            "workflow:instance:query",
            "workflow:history:query",
            "workflow:participant:query",
            "workflow:delegation:query",
            "workflow:delegation:edit",
            "workflow:delegation:delete",
            "workflow:notification:task:query",
            "workflow:notification:log:query");
        UpsertRolePermissions("wf_no_permission",
            "system:user:query",
            "system:role:query",
            "system:query-view:query");
        UpsertRolePermissions("wms_admin",
            "system:user:query",
            "system:role:query",
            "system:dept:query",
            "system:position:query",
            "system:query-view:query",
            "system:dict:query",
            "system:parameter:query",
            "tenant:app:query",
            "tenant:app:install",
            "tenant:app:enable",
            "tenant:app:disable",
            "runtime:data:query",
            ApplicationRuntimeDataModelCatalog.RuntimeConfigurationPermission,
            "runtime:grid-view:save-user",
            "runtime:grid-view:save-tenant",
            "workflow:model:query",
            "workflow:model:add",
            "workflow:model:edit",
            "workflow:model:delete",
            "workflow:model:publish",
            "workflow:model:suspend",
            "workflow:deployment:query",
            "workflow:deployment:resource",
            "workflow:binding:query",
            "workflow:binding:edit",
            "workflow:binding:delete",
            "workflow:form:query",
            "workflow:draft:query",
            "workflow:draft:edit",
            "workflow:draft:delete",
            "workflow:draft:submit",
            "workflow:category:query",
            "workflow:category:edit",
            "workflow:category:delete",
            "workflow:instance:query",
            "workflow:instance:start",
            "workflow:instance:withdraw",
            "workflow:instance:terminate",
            "workflow:instance:variable",
            "workflow:task:query",
            "workflow:task:claim",
            "workflow:task:approve",
            "workflow:task:transfer",
            "workflow:task:delegate",
            "workflow:task:attachment",
            "workflow:task:comment",
            "workflow:history:query",
            "workflow:participant:query",
            "workflow:report:query",
            "workflow:delegation:query",
            "workflow:delegation:edit",
            "workflow:delegation:delete",
            "workflow:calendar:query",
            "workflow:calendar:edit",
            "workflow:calendar:delete",
            "workflow:notification:channel:query",
            "workflow:notification:channel:edit",
            "workflow:notification:channel:delete",
            "workflow:notification:template:query",
            "workflow:notification:template:edit",
            "workflow:notification:template:delete",
            "workflow:notification:rule:query",
            "workflow:notification:rule:edit",
            "workflow:notification:rule:delete",
            "workflow:notification:task:query",
            "workflow:notification:task:send",
            "workflow:notification:log:query");
        UpsertRolePermissions("mes_admin",
            "system:user:query",
            "system:role:query",
            "system:dept:query",
            "system:position:query",
            "system:query-view:query",
            "system:dict:query",
            "system:parameter:query",
            "tenant:app:query",
            "tenant:app:install",
            "tenant:app:enable",
            "tenant:app:disable",
            "runtime:data:query",
            ApplicationRuntimeDataModelCatalog.RuntimeConfigurationPermission,
            "runtime:grid-view:save-user",
            "runtime:grid-view:save-tenant",
            "workflow:model:query",
            "workflow:model:add",
            "workflow:model:edit",
            "workflow:model:delete",
            "workflow:model:publish",
            "workflow:model:suspend",
            "workflow:deployment:query",
            "workflow:deployment:resource",
            "workflow:binding:query",
            "workflow:binding:edit",
            "workflow:binding:delete",
            "workflow:form:query",
            "workflow:draft:query",
            "workflow:draft:edit",
            "workflow:draft:delete",
            "workflow:draft:submit",
            "workflow:category:query",
            "workflow:category:edit",
            "workflow:category:delete",
            "workflow:instance:query",
            "workflow:instance:start",
            "workflow:instance:withdraw",
            "workflow:instance:terminate",
            "workflow:instance:variable",
            "workflow:task:query",
            "workflow:task:claim",
            "workflow:task:approve",
            "workflow:task:transfer",
            "workflow:task:delegate",
            "workflow:task:attachment",
            "workflow:task:comment",
            "workflow:history:query",
            "workflow:participant:query",
            "workflow:report:query",
            "workflow:delegation:query",
            "workflow:delegation:edit",
            "workflow:delegation:delete",
            "workflow:calendar:query",
            "workflow:calendar:edit",
            "workflow:calendar:delete",
            "workflow:notification:channel:query",
            "workflow:notification:channel:edit",
            "workflow:notification:channel:delete",
            "workflow:notification:template:query",
            "workflow:notification:template:edit",
            "workflow:notification:template:delete",
            "workflow:notification:rule:query",
            "workflow:notification:rule:edit",
            "workflow:notification:rule:delete",
            "workflow:notification:task:query",
            "workflow:notification:task:send",
            "workflow:notification:log:query");
        UpsertRolePermissions("wms_admin", FlowiseAdminPermissionCodes());
        UpsertRolePermissions("mes_admin", FlowiseAdminPermissionCodes());
        UpsertRolePermissions("wms_admin", PermissionCodes.AppDataCenterPermissionCodes.ToArray());
        UpsertRolePermissions("mes_admin", PermissionCodes.AppDataCenterPermissionCodes.ToArray());
        UpsertRolePermissions("wms_admin", ImPermissionCodes());
        UpsertRolePermissions("mes_admin", ImPermissionCodes());
        UpsertRolePermissions("wms_auditor",
            "system:user:query",
            "system:role:query",
            "system:dept:query",
            "system:position:query",
            "system:query-view:query",
            "system:dict:query",
            "runtime:data:query",
            "runtime:grid-view:save-user",
            "workflow:model:query",
            "workflow:binding:query",
            "workflow:form:query",
            "workflow:draft:query",
            "workflow:draft:edit",
            "workflow:draft:delete",
            "workflow:draft:submit",
            "workflow:category:query",
            "workflow:instance:query",
            "workflow:instance:start",
            "workflow:task:query",
            "workflow:task:claim",
            "workflow:task:approve",
            "workflow:task:transfer",
            "workflow:task:delegate",
            "workflow:task:attachment",
            "workflow:task:comment",
            "workflow:history:query",
            "workflow:participant:query",
            "workflow:report:query",
            "workflow:delegation:query",
            "workflow:delegation:edit",
            "workflow:delegation:delete",
            "workflow:calendar:query",
            "workflow:notification:channel:query",
            "workflow:notification:template:query",
            "workflow:notification:rule:query",
            "workflow:notification:task:query",
            "workflow:notification:log:query");
    }

    private static string[] ImPermissionCodes() =>
    [
        PermissionCodes.ImConversationView,
        PermissionCodes.ImConversationCreate,
        PermissionCodes.ImMessageSend,
        PermissionCodes.ImMessageRead,
        PermissionCodes.ImUserSearch
    ];

    private void SeedMenus()
    {
        UpsertMenu("home", "首页", null, "/home", "DashboardPage", "Directory", 1, true, null, "LayoutDashboard");
        UpsertMenu("system", "系统设置", null, null, null, "Directory", 2, true, null, "Settings");
        UpsertMenu("platform", "平台管理", null, null, null, "Directory", 3, true, null, "Building2");
        UpsertMenu("platform:tenant", "租户管理", "platform", "/platform/tenants", "PlatformTenantsPage", "Menu", 1, true, "platform:tenant:query", "Building2");
        UpsertMenu("platform:application", "应用中心", "platform", "/platform/applications", "PlatformApplicationsPage", "Menu", 2, true, "platform:application:query", "PanelsTopLeft");
        UpsertMenu("platform:application:enter", "进入应用后台", "platform:application", null, null, "Button", 1, true, "platform:application:enter", null);
        UpsertMenu("platform:application:publish", "发布应用", "platform:application", null, null, "Button", 2, true, "platform:application:publish", null);
        UpsertMenu("platform:application:publish-task", "发布任务", "platform:application", null, null, "Button", 3, true, "platform:application:publish-task", null);
        UpsertMenu("platform:application:publish-log", "发布日志", "platform:application", null, null, "Button", 4, true, "platform:application:publish-log", null);
        UpsertMenu("platform:application:publish-artifact-download", "下载发布产物", "platform:application", null, null, "Button", 5, true, "platform:application:publish-artifact-download", null);
        UpsertMenu("platform:application:publish-artifact-delete", "删除发布产物", "platform:application", null, null, "Button", 6, true, "platform:application:publish-artifact-delete", null);
        UpsertMenu("platform:tenant-app", "租户应用", "platform", "/platform/tenant-apps", "PlatformTenantAppsPage", "Menu", 3, true, "platform:tenant-app:query", "Boxes");
        UpsertMenu("platform:user-tenant", "用户租户关系", "platform", "/platform/user-tenants", "PlatformUserTenantsPage", "Menu", 4, true, "platform:user-tenant:query", "Users");
        UpsertMenu("platform:user-app-role", "用户应用角色", "platform", "/platform/user-app-roles", "PlatformUserAppRolesPage", "Menu", 5, true, "platform:user-app-role:query", "ShieldCheck");
        UpsertMenu("project-management", "项目管理", "platform", "/platform/project-management", "ProjectManagementPage", "Menu", 6, true, PermissionCodes.ProjectManagementProjectView, "KanbanSquare");
        UpsertBpmMenuTree("tenant-system", "SYSTEM", 4);
        UpsertMenu("system:user", "用户管理", "system", "/system/users", "UsersPage", "Menu", 1, true, "system:user:query", "UserCog");
        UpsertMenu("system:user:add", "新增用户", "system:user", null, null, "Button", 1, true, "system:user:add", null);
        UpsertMenu("system:user:edit", "编辑用户", "system:user", null, null, "Button", 2, true, "system:user:edit", null);
        UpsertMenu("system:user:delete", "删除用户", "system:user", null, null, "Button", 3, true, "system:user:delete", null);
        UpsertMenu("system:user:grant-role", "分配角色", "system:user", null, null, "Button", 4, true, "system:user:grant-role", null);
        UpsertMenu("system:user:reset-password", "重置密码", "system:user", null, null, "Button", 5, true, "system:user:reset-password", null);
        UpsertMenu("system:user:workflow-start", "发起审批", "system:user", null, null, "Button", 6, true, "workflow:instance:start", null);
        UpsertMenu("system:user:workflow-history", "审批记录", "system:user", null, null, "Button", 7, true, "workflow:history:query", null);
        UpsertMenu("system:user:print-list", "打印列表", "system:user", null, null, "Button", 8, true, "system:print:use", null);
        UpsertMenu("system:user:print-detail", "打印详情", "system:user", null, null, "Button", 9, true, "system:print:use", null);
        UpsertMenu("system:user:print-template", "配置打印模板", "system:user", null, null, "Button", 10, true, "system:print:edit", null);
        UpsertMenu("system:dept", "部门管理", "system", "/system/departments", "DepartmentsPage", "Menu", 2, true, "system:dept:query", "Building2");
        UpsertMenu("system:dept:add", "新增部门", "system:dept", null, null, "Button", 1, true, "system:dept:add", null);
        UpsertMenu("system:dept:edit", "编辑部门", "system:dept", null, null, "Button", 2, true, "system:dept:edit", null);
        UpsertMenu("system:dept:delete", "删除部门", "system:dept", null, null, "Button", 3, true, "system:dept:delete", null);
        UpsertMenu("system:position", "岗位管理", "system", "/system/positions", "PositionsPage", "Menu", 3, true, "system:position:query", "BriefcaseBusiness");
        UpsertMenu("system:position:add", "新增岗位", "system:position", null, null, "Button", 1, true, "system:position:add", null);
        UpsertMenu("system:position:edit", "编辑岗位", "system:position", null, null, "Button", 2, true, "system:position:edit", null);
        UpsertMenu("system:position:delete", "删除岗位", "system:position", null, null, "Button", 3, true, "system:position:delete", null);
        UpsertMenu("system:menu", "菜单管理", "system", "/system/menus", "MenusPage", "Menu", 4, true, "system:menu:query", "FolderTree");
        UpsertMenu("system:menu:add", "新增菜单", "system:menu", null, null, "Button", 1, true, "system:menu:add", null);
        UpsertMenu("system:menu:edit", "编辑菜单", "system:menu", null, null, "Button", 2, true, "system:menu:edit", null);
        UpsertMenu("system:menu:delete", "删除菜单", "system:menu", null, null, "Button", 3, true, "system:menu:delete", null);
        UpsertMenu("system:role", "角色管理", "system", "/system/roles", "RolesPage", "Menu", 5, true, "system:role:query", "ShieldCheck");
        UpsertMenu("system:role:add", "新增角色", "system:role", null, null, "Button", 1, true, "system:role:add", null);
        UpsertMenu("system:role:edit", "编辑角色", "system:role", null, null, "Button", 2, true, "system:role:edit", null);
        UpsertMenu("system:role:delete", "删除角色", "system:role", null, null, "Button", 3, true, "system:role:delete", null);
        UpsertMenu("system:role:grant", "角色授权", "system:role", null, null, "Button", 4, true, "system:role:grant", null);
        UpsertMenu("system:role:workflow-start", "发起审批", "system:role", null, null, "Button", 5, true, "workflow:instance:start", null);
        UpsertMenu("system:role:workflow-history", "审批记录", "system:role", null, null, "Button", 6, true, "workflow:history:query", null);
        UpsertMenu("system:role:print-list", "打印列表", "system:role", null, null, "Button", 7, true, "system:print:use", null);
        UpsertMenu("system:role:print-detail", "打印详情", "system:role", null, null, "Button", 8, true, "system:print:use", null);
        UpsertMenu("system:role:print-template", "配置打印模板", "system:role", null, null, "Button", 9, true, "system:print:edit", null);
        UpsertMenu("system:dict", "字典管理", "system", "/system/dicts", "DictsPage", "Menu", 6, true, "system:dict:query", "ListTree");
        UpsertMenu("system:dict:add", "新增字典", "system:dict", null, null, "Button", 1, true, "system:dict:add", null);
        UpsertMenu("system:dict:edit", "编辑字典", "system:dict", null, null, "Button", 2, true, "system:dict:edit", null);
        UpsertMenu("system:dict:delete", "删除字典", "system:dict", null, null, "Button", 3, true, "system:dict:delete", null);
        UpsertMenu("system:parameter", "系统参数", "system", "/system/parameters", "ParametersPage", "Menu", 7, true, "system:parameter:query", "SlidersHorizontal");
        UpsertMenu("system:parameter:add", "新增参数", "system:parameter", null, null, "Button", 1, true, "system:parameter:add", null);
        UpsertMenu("system:parameter:edit", "编辑参数", "system:parameter", null, null, "Button", 2, true, "system:parameter:edit", null);
        UpsertMenu("system:parameter:delete", "删除参数", "system:parameter", null, null, "Button", 3, true, "system:parameter:delete", null);
        UpsertMenu("system:announcement", "通知公告", "system", "/system/announcements", "AnnouncementsPage", "Menu", 8, true, "system:announcement:query", "Megaphone");
        UpsertMenu("system:announcement:add", "新增公告", "system:announcement", null, null, "Button", 1, true, "system:announcement:add", null);
        UpsertMenu("system:announcement:edit", "编辑公告", "system:announcement", null, null, "Button", 2, true, "system:announcement:edit", null);
        UpsertMenu("system:announcement:delete", "删除公告", "system:announcement", null, null, "Button", 3, true, "system:announcement:delete", null);
        UpsertMenu("system:announcement:publish", "发布公告", "system:announcement", null, null, "Button", 4, true, "system:announcement:publish", null);
        UpsertMenu("system:announcement:withdraw", "撤回公告", "system:announcement", null, null, "Button", 5, true, "system:announcement:withdraw", null);
        UpsertMenu("system:announcement:top", "置顶公告", "system:announcement", null, null, "Button", 6, true, "system:announcement:top", null);
        UpsertMenu("system:operation-log", "操作日志", "system", "/system/operation-logs", "OperationLogsPage", "Menu", 9, true, "system:operation-log:query", "ScrollText");
        UpsertMenu("system:login-log", "登录日志", "system", "/system/login-logs", "LoginLogsPage", "Menu", 10, true, "system:login-log:query", "ClipboardList");
        UpsertMenu("system:online-user", "在线用户", "system", "/system/online-users", "OnlineUsersPage", "Menu", 11, true, "system:online-user:query", "UsersRound");
        UpsertMenu("system:online-user:kick", "强制下线", "system:online-user", null, null, "Button", 1, true, "system:online-user:kick", null);
        UpsertMenu("system:scheduled-job", "任务调度", "system", "/system/scheduled-jobs", "ScheduledJobsPage", "Menu", 12, true, "system:scheduled-job:query", "Activity");
        UpsertMenu("system:scheduled-job:add", "新增任务", "system:scheduled-job", null, null, "Button", 1, true, "system:scheduled-job:add", null);
        UpsertMenu("system:scheduled-job:edit", "编辑任务", "system:scheduled-job", null, null, "Button", 2, true, "system:scheduled-job:edit", null);
        UpsertMenu("system:scheduled-job:delete", "删除任务", "system:scheduled-job", null, null, "Button", 3, true, "system:scheduled-job:delete", null);
        UpsertMenu("system:scheduled-job:trigger", "手动执行", "system:scheduled-job", null, null, "Button", 4, true, "system:scheduled-job:trigger", null);
        UpsertMenu("system:scheduled-job:log", "执行日志", "system:scheduled-job", null, null, "Button", 5, true, "system:scheduled-job:log", null);
        UpsertMenu("system:abp-setting", "ABP 基础设施", "system", "/system/abp-infrastructure-settings", "AbpInfrastructureSettingsPage", "Menu", 13, true, "system:abp-setting:query", "Cable");
        UpsertMenu("system:abp-setting:edit", "保存基础设施设置", "system:abp-setting", null, null, "Button", 1, true, "system:abp-setting:edit", null);
        UpsertMenu("system:abp-setting:test", "测试基础设施设置", "system:abp-setting", null, null, "Button", 2, true, "system:abp-setting:test", null);
        UpsertMenu("system:file", "文件中心", "system", "/system/files", "FilesPage", "Menu", 14, true, "system:file:query", "FileSearch");
        UpsertMenu("system:file:upload", "上传文件", "system:file", null, null, "Button", 1, true, "system:file:upload", null);
        UpsertMenu("system:file:preview", "预览文件", "system:file", null, null, "Button", 2, true, "system:file:preview", null);
        UpsertMenu("system:file:download", "下载文件", "system:file", null, null, "Button", 3, true, "system:file:download", null);
        UpsertMenu("system:file:delete", "删除文件", "system:file", null, null, "Button", 4, true, "system:file:delete", null);
        UpsertMenu("system:file:print-list", "打印列表", "system:file", null, null, "Button", 5, true, "system:print:use", null);
        UpsertMenu("system:file:print-detail", "打印详情", "system:file", null, null, "Button", 6, true, "system:print:use", null);
        UpsertMenu("system:file:print-template", "配置打印模板", "system:file", null, null, "Button", 7, true, "system:print:edit", null);
        UpsertMenu("system:print", "打印中心", "system", "/system/print-center", "PrintCenterPage", "Menu", 15, true, "system:print:query", "Printer");
        UpsertMenu("system:print:add", "新增模板", "system:print", null, null, "Button", 1, true, "system:print:add", null);
        UpsertMenu("system:print:edit", "编辑模板", "system:print", null, null, "Button", 2, true, "system:print:edit", null);
        UpsertMenu("system:print:delete", "删除模板", "system:print", null, null, "Button", 3, true, "system:print:delete", null);
        UpsertMenu("system:print:publish", "发布模板", "system:print", null, null, "Button", 4, true, "system:print:publish", null);
        UpsertMenu("system:print:default", "设为默认模板", "system:print", null, null, "Button", 5, true, "system:print:default", null);
        UpsertMenu("system:print:use", "使用模板打印", "system:print", null, null, "Button", 6, true, "system:print:use", null);
        RetireWorkspaceMenu("tenant-a", "WMS", "system");
        RetireWorkspaceMenu("tenant-a", "WMS", "system:dict");
        RetireWorkspaceMenu("tenant-a", "WMS", "system:parameter");
        RetireWorkspaceMenu("tenant-a", "MES", "system");
        RetireWorkspaceMenu("tenant-a", "MES", "system:dict");
        RetireWorkspaceMenu("tenant-a", "MES", "system:parameter");
        RetireWorkspaceMenu("tenant-b", "WMS", "system");
        RetireWorkspaceMenu("tenant-b", "WMS", "system:dict");
        RetireWorkspaceMenu("tenant-b", "WMS", "system:parameter");
        RetireApplicationWorkspaceModuleMenus();
        HideMenu("system:settings");
        HideMenu("system:param");
        RetireLegacyProtectionMenus();
    }

    private void SeedDataModels()
    {
        SeedRuntimeDataModelsForWorkspace("tenant-system", "SYSTEM");
        SeedRuntimeDataModelsForWorkspace("tenant-a", "WMS");
        SeedRuntimeDataModelsForWorkspace("tenant-a", "MES");
        SeedRuntimeDataModelsForWorkspace("tenant-b", "WMS");
    }

    private void SeedRuntimeDataModelsForWorkspace(string tenantId, string appCode)
    {
        UpsertDataModel(
            tenantId,
            appCode,
            ApplicationRuntimeDataModelCatalog.RuntimeMenuModelCode,
            ApplicationRuntimeDataModelCatalog.RuntimeMenuModelName,
            ApplicationRuntimeDataModelCatalog.RuntimeMenuProviderKey,
            ApplicationRuntimeDataModelCatalog.RuntimeMenuKeyField,
            ApplicationRuntimeDataModelCatalog.RuntimeConfigurationPermission,
            ApplicationRuntimeDataModelCatalog.RuntimeMenuSchemaJson);
    }

    private void SeedDicts()
    {
        EnsureDictType(
            "sys_enabled_status",
            "启用状态",
            new[]
            {
                ("启用", "1"),
                ("禁用", "0")
            });

        EnsureDictType(
            "sys_yes_no",
            "是否",
            new[]
            {
                ("是", "Y"),
                ("否", "N")
            });

        EnsureDictType("pm_task_requirement_type", "任务分类", new[] { ("功能", "Feature"), ("非功能", "NonFunctional"), ("其他", "Other") });
        EnsureDictType("pm_task_requirement_source", "任务来源", new[] { ("产品规划", "ProductPlan"), ("客户反馈", "Customer"), ("内部提出", "Internal"), ("缺陷转任务", "BugConversion"), ("其他", "Other") });
        EnsureDictType("pm_task_work_item_type", "工作项类型", new[] { ("任务", "Requirement"), ("用户故事", "UserStory"), ("子任务", "Task"), ("缺陷", "Bug") });
    }

    private void SeedCodeRules()
    {
        EnsureCodeRule(
            "purchase-order",
            "采购订单",
            "CGDD",
            "Daily",
            new[]
            {
                ("Static", "CGDD", 0, 1),
                ("Date", "yyyyMMdd", 0, 2),
                ("Sequence", null, 4, 3)
            });

        EnsureCodeRule(
            "production-order",
            "生产工单",
            "SCRW",
            "Daily",
            new[]
            {
                ("Static", "SCRW", 0, 1),
                ("Date", "yyyyMMdd", 0, 2),
                ("Sequence", null, 4, 3)
            });

        EnsureCodeRule(
            "receipt-order",
            "入库单",
            "RKD",
            "Daily",
            new[]
            {
                ("Static", "RKD", 0, 1),
                ("Date", "yyyyMMdd", 0, 2),
                ("Sequence", null, 4, 3)
            });

        EnsureCodeRule(
            "inspection-report",
            "检验报告",
            "JYBG",
            "Daily",
            new[]
            {
                ("Static", "JYBG", 0, 1),
                ("Date", "yyyyMMdd", 0, 2),
                ("Sequence", null, 4, 3)
            });
    }

    private void EnsureDictType(string dictCode, string dictName, IReadOnlyCollection<(string Label, string Value)> items)
    {
        var dictType = db.Queryable<SystemDictTypeEntity>().ToList().FirstOrDefault(item => item.DictCode == dictCode);
        if (dictType is null)
        {
            dictType = new SystemDictTypeEntity { DictName = dictName, DictCode = dictCode, IsEnabled = true };
            db.Insertable(dictType).ExecuteCommand();
        }
        else if (dictCode == "pm_task_requirement_type" && dictType.DictName == "任务类型")
        {
            dictType.DictName = dictName;
            db.Updateable(dictType).ExecuteCommand();
        }

        var existingValues = db.Queryable<SystemDictItemEntity>()
            .Where(item => item.DictTypeId == dictType.Id)
            .Select(item => item.ItemValue)
            .ToList();

        var newItems = items
            .Where(item => !existingValues.Contains(item.Value))
            .Select((item, index) => new SystemDictItemEntity
            {
                DictTypeId = dictType.Id,
                ItemLabel = item.Label,
                ItemValue = item.Value,
                SortOrder = existingValues.Count + index + 1,
                IsEnabled = true
            })
            .ToArray();

        if (newItems.Length > 0)
        {
            db.Insertable(newItems).ExecuteCommand();
        }
    }

    private void EnsureCodeRule(
        string ruleCode,
        string ruleName,
        string prefix,
        string resetPolicy,
        IReadOnlyCollection<(string SegmentType, string? SegmentValue, int SegmentLength, int SortOrder)> segments)
    {
        var rule = db.Queryable<SystemCodeRuleEntity>().ToList().FirstOrDefault(item => item.RuleCode == ruleCode);
        if (rule is null)
        {
            rule = new SystemCodeRuleEntity
            {
                RuleCode = ruleCode,
                RuleName = ruleName,
                ResetPolicy = resetPolicy,
                CurrentSequence = 0,
                IsEnabled = true
            };

            db.Insertable(rule).ExecuteCommand();
        }

        var existingSegments = db.Queryable<SystemCodeRuleSegmentEntity>()
            .Where(item => item.CodeRuleId == rule.Id)
            .ToList()
            .Select(item => item.SegmentType + "|" + (item.SegmentValue ?? string.Empty) + "|" + item.SegmentLength + "|" + item.SortOrder)
            .ToList();

        var newSegments = segments
            .Select(segment => new
            {
                Key = segment.SegmentType + "|" + (segment.SegmentValue ?? string.Empty) + "|" + segment.SegmentLength + "|" + segment.SortOrder,
                Entity = new SystemCodeRuleSegmentEntity
                {
                    CodeRuleId = rule.Id,
                    SegmentType = segment.SegmentType,
                    SegmentValue = segment.SegmentValue,
                    SegmentLength = segment.SegmentLength,
                    SortOrder = segment.SortOrder,
                    IsEnabled = true
                }
            })
            .Where(item => !existingSegments.Contains(item.Key))
            .Select(item => item.Entity)
            .ToArray();

        if (newSegments.Length > 0)
        {
            db.Insertable(newSegments).ExecuteCommand();
        }
    }

    private void SeedParameters()
    {
        if (db.Queryable<SystemParameterEntity>().Any())
        {
            return;
        }

        var parameters = new[]
        {
            new SystemParameterEntity { ParamName = "Default Locale", ParamKey = "app.defaultLocale", ParamValue = "zh-CN", Category = "i18n", IsEnabled = true },
            new SystemParameterEntity { ParamName = "Default Theme", ParamKey = "app.defaultTheme", ParamValue = "brand", Category = "ui", IsEnabled = true },
            new SystemParameterEntity { ParamName = "Request Timeout", ParamKey = "app.requestTimeoutMs", ParamValue = "10000", Category = "http", IsEnabled = true }
        };

        db.Insertable(parameters).ExecuteCommand();
    }

    private void SeedScheduledJobs()
    {
        EnsureScheduledJob(
            "sample-health-check",
            "示例：系统健康检查",
            "Preset",
            "system.health-check",
            "Enabled",
            new { kind = "EveryMinutes", intervalValue = 30, timeOfDay = (string?)null, weekDays = (int[]?)null, monthDays = (int[]?)null, timeZone = "China Standard Time" },
            "*/30 * * * *",
            "每 30 分钟执行一次",
            null,
            "内置示例任务，可用于验证预置任务执行日志。");

        EnsureScheduledJob(
            "sample-parameter-inspection",
            "示例：系统参数巡检",
            "Preset",
            "system.parameter-inspection",
            "Paused",
            new { kind = "Daily", intervalValue = (int?)null, timeOfDay = "02:00", weekDays = (int[]?)null, monthDays = (int[]?)null, timeZone = "China Standard Time" },
            "0 2 * * *",
            "每天 02:00 执行",
            null,
            "默认暂停，恢复后会统计系统参数状态。");

        EnsureScheduledJob(
            "sample-http-callback",
            "示例：HTTP 回调",
            "HttpCallback",
            null,
            "Paused",
            new { kind = "Daily", intervalValue = (int?)null, timeOfDay = "09:00", weekDays = (int[]?)null, monthDays = (int[]?)null, timeZone = "China Standard Time" },
            "0 9 * * *",
            "每天 09:00 执行",
            new { url = "http://localhost:5000/api/health", method = "GET", bodyJson = (string?)null, headers = (Dictionary<string, string>?)null },
            "默认暂停的 HTTP 回调示例，只允许回调配置白名单域名。");
    }

    private void SeedWorkflowNotifications()
    {
        var enabledWorkspaces = db.Queryable<SystemTenantAppEntity>()
            .Where(item => !item.IsDeleted && item.Status == "Enabled")
            .ToList();

        foreach (var workspace in enabledWorkspaces)
        {
            UpsertWorkflowNotificationChannel(
                workspace.TenantId,
                workspace.AppCode,
                "in-app",
                "站内信",
                "in-app",
                new { delivery = "signalr", persistTask = true });

            UpsertWorkflowMessageTemplate(
                workspace.TenantId,
                workspace.AppCode,
                "workflow-process-start",
                "流程发起提醒",
                "in-app",
                "流程 {{processName}} 已发起",
                "业务 {{businessType}}/{{businessKey}} 的流程 {{processName}} 已由 {{starterUserName}} 发起。");

            UpsertWorkflowMessageTemplate(
                workspace.TenantId,
                workspace.AppCode,
                "workflow-node-enter",
                "审批待办提醒",
                "in-app",
                "待办 {{processName}} - {{nodeName}}",
                "你有一条 {{processName}} 的 {{nodeName}} 待办，业务单据 {{businessType}}/{{businessKey}}。");

            UpsertWorkflowMessageTemplate(
                workspace.TenantId,
                workspace.AppCode,
                "workflow-task-complete",
                "任务处理提醒",
                "in-app",
                "审批任务已处理",
                "{{nodeName}} 已完成 {{action}}，处理人 {{operatorUserName}}。");

            UpsertWorkflowMessageTemplate(
                workspace.TenantId,
                workspace.AppCode,
                "workflow-process-end",
                "流程结束提醒",
                "in-app",
                "流程 {{processName}} 已结束",
                "业务 {{businessType}}/{{businessKey}} 的流程 {{processName}} 已结束。");
        }
    }

    private void UpsertWorkflowNotificationChannel(
        string tenantId,
        string appCode,
        string channelCode,
        string channelName,
        string channelType,
        object config)
    {
        var normalizedAppCode = appCode.Trim().ToUpperInvariant();
        var existing = db.Queryable<WorkflowNotificationChannelEntity>()
            .First(item => item.TenantId == tenantId && item.AppCode == normalizedAppCode && item.ChannelCode == channelCode);
        var configJson = JsonSerializer.Serialize(config);

        if (existing is null)
        {
            db.Insertable(new WorkflowNotificationChannelEntity
            {
                TenantId = tenantId,
                AppCode = normalizedAppCode,
                ChannelCode = channelCode,
                ChannelName = channelName,
                ChannelType = channelType,
                IsEnabled = true,
                ConfigJson = configJson,
                FailurePolicy = "ignore"
            }).ExecuteCommand();
            return;
        }

        existing.ChannelName = channelName;
        existing.ChannelType = channelType;
        existing.IsEnabled = true;
        existing.ConfigJson = configJson;
        existing.FailurePolicy = "ignore";
        existing.IsDeleted = false;
        db.Updateable(existing).ExecuteCommand();
    }

    private void UpsertWorkflowMessageTemplate(
        string tenantId,
        string appCode,
        string templateCode,
        string templateName,
        string channelType,
        string subjectTemplate,
        string bodyTemplate)
    {
        var variablesJson = JsonSerializer.Serialize(new[]
        {
            "processName",
            "businessType",
            "businessKey",
            "starterUserName",
            "nodeName",
            "operatorUserName",
            "action"
        });
        var normalizedAppCode = appCode.Trim().ToUpperInvariant();

        var existing = db.Queryable<WorkflowMessageTemplateEntity>()
            .First(item => item.TenantId == tenantId && item.AppCode == normalizedAppCode && item.TemplateCode == templateCode);

        if (existing is null)
        {
            db.Insertable(new WorkflowMessageTemplateEntity
            {
                TenantId = tenantId,
                AppCode = normalizedAppCode,
                TemplateCode = templateCode,
                TemplateName = templateName,
                ChannelType = channelType,
                SubjectTemplate = subjectTemplate,
                BodyTemplate = bodyTemplate,
                VariablesJson = variablesJson,
                IsEnabled = true
            }).ExecuteCommand();
            return;
        }

        existing.TemplateName = templateName;
        existing.ChannelType = channelType;
        existing.SubjectTemplate = subjectTemplate;
        existing.BodyTemplate = bodyTemplate;
        existing.VariablesJson = variablesJson;
        existing.IsEnabled = true;
        existing.IsDeleted = false;
        db.Updateable(existing).ExecuteCommand();
    }

    private void UpsertPermissionCode(SystemPermissionCodeEntity entity)
    {
        var existing = db.Queryable<SystemPermissionCodeEntity>()
            .First(item => item.PermissionCode == entity.PermissionCode);

        if (existing is null)
        {
            db.Insertable(entity).ExecuteCommand();
            return;
        }

        existing.ModuleName = entity.ModuleName;
        existing.PermissionName = entity.PermissionName;
        existing.IsEnabled = entity.IsEnabled;
        existing.IsDeleted = false;
        db.Updateable(existing).ExecuteCommand();
    }

    private void UpsertRole(string roleCode, string roleName, string dataScope, bool isEnabled, string? tenantId, string? appCode)
    {
        var existing = db.Queryable<SystemRoleEntity>()
            .ToList()
            .FirstOrDefault(item =>
                item.RoleCode == roleCode &&
                string.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.AppCode, appCode, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            db.Insertable(new SystemRoleEntity
            {
                TenantId = tenantId,
                AppCode = appCode,
                RoleCode = roleCode,
                RoleName = roleName,
                DataScope = dataScope,
                IsEnabled = isEnabled
            }).ExecuteCommand();
            return;
        }

        existing.RoleName = roleName;
        existing.TenantId = tenantId;
        existing.AppCode = appCode;
        existing.DataScope = dataScope;
        existing.IsEnabled = isEnabled;
        existing.IsDeleted = false;
        db.Updateable(existing).ExecuteCommand();
    }

    private void UpsertDepartment(
        string id,
        string deptCode,
        string deptName,
        string? parentId,
        string? managerName,
        string? phoneNumber,
        int sortOrder,
        string status)
    {
        var existing = db.Queryable<SystemDepartmentEntity>()
            .ToList()
            .FirstOrDefault(item => item.Id == id || item.DeptCode == deptCode);

        if (existing is null)
        {
            db.Insertable(new SystemDepartmentEntity
            {
                Id = id,
                DeptCode = deptCode,
                DeptName = deptName,
                ParentId = parentId,
                ManagerName = managerName,
                PhoneNumber = phoneNumber,
                SortOrder = sortOrder,
                Status = status
            }).ExecuteCommand();
            return;
        }

        existing.DeptCode = deptCode;
        existing.DeptName = deptName;
        existing.ParentId = parentId;
        existing.ManagerName = managerName;
        existing.PhoneNumber = phoneNumber;
        existing.SortOrder = sortOrder;
        existing.Status = status;
        existing.IsDeleted = false;
        db.Updateable(existing).ExecuteCommand();
    }

    private void UpsertPosition(
        string id,
        string positionCode,
        string positionName,
        string deptId,
        string? positionLevel,
        int sortOrder,
        string status)
    {
        var existing = db.Queryable<SystemPositionEntity>()
            .ToList()
            .FirstOrDefault(item => item.Id == id || item.PositionCode == positionCode);

        if (existing is null)
        {
            db.Insertable(new SystemPositionEntity
            {
                Id = id,
                PositionCode = positionCode,
                PositionName = positionName,
                DeptId = deptId,
                PositionLevel = positionLevel,
                SortOrder = sortOrder,
                Status = status
            }).ExecuteCommand();
            return;
        }

        existing.PositionCode = positionCode;
        existing.PositionName = positionName;
        existing.DeptId = deptId;
        existing.PositionLevel = positionLevel;
        existing.SortOrder = sortOrder;
        existing.Status = status;
        existing.IsDeleted = false;
        db.Updateable(existing).ExecuteCommand();
    }

    private void UpsertUser(
        string userName,
        string displayName,
        string password,
        string? deptId,
        string? positionId,
        string? phoneNumber,
        string? email,
        bool isAdmin,
        string status)
    {
        var existing = db.Queryable<SystemUserEntity>()
            .ToList()
            .FirstOrDefault(item => item.UserName == userName);

        if (existing is null)
        {
            var entity = new SystemUserEntity
            {
                UserName = userName,
                DisplayName = displayName,
                PasswordHash = passwordHashService.HashPassword(password),
                DeptId = deptId,
                PositionId = positionId,
                PhoneNumber = phoneNumber,
                Email = email,
                IsAdmin = isAdmin,
                Status = status
            };
            db.Insertable(entity).ExecuteCommand();
            EnsureUserEmployment(entity, "tenant-system", "SYSTEM", deptId, positionId, true, status);
            return;
        }

        existing.DisplayName = displayName;
        if (!existing.PasswordHash.StartsWith("PBKDF2$v1$", StringComparison.Ordinal))
        {
            existing.PasswordHash = passwordHashService.HashPassword(password);
        }
        existing.PasswordResetRequired = false;
        existing.PasswordFormatVersion = "v1";
        existing.DeptId = deptId;
        existing.PositionId = positionId;
        existing.PhoneNumber = phoneNumber;
        existing.Email = email;
        existing.IsAdmin = isAdmin;
        existing.Status = status;
        existing.IsDeleted = false;
        db.Updateable(existing).ExecuteCommand();
        EnsureUserEmployment(existing, "tenant-system", "SYSTEM", deptId, positionId, true, status);
    }

    private void EnsureUserRole(string userName, string roleCode)
    {
        var user = db.Queryable<SystemUserEntity>()
            .ToList()
            .FirstOrDefault(item => item.UserName == userName && !item.IsDeleted);
        var role = db.Queryable<SystemRoleEntity>()
            .ToList()
            .FirstOrDefault(item => item.RoleCode == roleCode && !item.IsDeleted);

        if (user is null || role is null)
        {
            return;
        }

        var existing = db.Queryable<SystemUserRoleEntity>()
            .ToList()
            .FirstOrDefault(item => item.UserId == user.Id && item.RoleId == role.Id && !item.IsDeleted);

        if (existing is not null)
        {
            return;
        }

        db.Insertable(new SystemUserRoleEntity
        {
            UserId = user.Id,
            RoleId = role.Id
        }).ExecuteCommand();
    }

    private void EnsureUserTenantMembership(
        string userName,
        string tenantId,
        string? deptId,
        string? positionId,
        bool isTenantAdmin,
        bool isDefault,
        string status)
    {
        var user = db.Queryable<SystemUserEntity>()
            .ToList()
            .FirstOrDefault(item => item.UserName == userName && !item.IsDeleted);
        var tenant = db.Queryable<SystemTenantEntity>()
            .ToList()
            .FirstOrDefault(item => item.Id == tenantId && !item.IsDeleted);

        if (user is null || tenant is null)
        {
            return;
        }

        var existing = db.Queryable<SystemUserTenantMembershipEntity>()
            .ToList()
            .FirstOrDefault(item => item.UserId == user.Id && item.TenantId == tenantId);

        if (existing is null)
        {
            db.Insertable(new SystemUserTenantMembershipEntity
            {
                UserId = user.Id,
                TenantId = tenantId,
                DeptId = deptId,
                PositionId = positionId,
                IsTenantAdmin = isTenantAdmin,
                IsDefault = isDefault,
                Status = status
            }).ExecuteCommand();
            EnsureUserEmployment(user, tenantId, "SYSTEM", deptId, positionId, isDefault, status);
            return;
        }

        existing.DeptId = deptId;
        existing.PositionId = positionId;
        existing.IsTenantAdmin = isTenantAdmin;
        existing.IsDefault = isDefault;
        existing.Status = status;
        existing.IsDeleted = false;
        db.Updateable(existing).ExecuteCommand();
        EnsureUserEmployment(user, tenantId, "SYSTEM", deptId, positionId, isDefault, status);
    }

    private void EnsureUserAppRole(string userName, string tenantId, string appCode, string roleCode, bool isDefault)
    {
        var user = db.Queryable<SystemUserEntity>()
            .ToList()
            .FirstOrDefault(item => item.UserName == userName && !item.IsDeleted);
        var role = db.Queryable<SystemRoleEntity>()
            .ToList()
            .FirstOrDefault(item =>
                item.RoleCode == roleCode &&
                item.TenantId == tenantId &&
                item.AppCode == appCode &&
                !item.IsDeleted);

        if (user is null || role is null)
        {
            return;
        }

        var membership = db.Queryable<SystemUserTenantMembershipEntity>()
            .ToList()
            .FirstOrDefault(item => item.UserId == user.Id && item.TenantId == tenantId && !item.IsDeleted);
        EnsureUserEmployment(user, tenantId, appCode, membership?.DeptId ?? user.DeptId, membership?.PositionId ?? user.PositionId, isDefault, membership?.Status ?? user.Status);

        var existing = db.Queryable<SystemUserAppRoleEntity>()
            .ToList()
            .FirstOrDefault(item =>
                item.UserId == user.Id &&
                item.TenantId == tenantId &&
                item.AppCode == appCode &&
                item.RoleId == role.Id);

        if (existing is null)
        {
            db.Insertable(new SystemUserAppRoleEntity
            {
                UserId = user.Id,
                TenantId = tenantId,
                AppCode = appCode,
                RoleId = role.Id,
                IsDefault = isDefault
            }).ExecuteCommand();
            return;
        }

        existing.IsDefault = isDefault;
        existing.IsDeleted = false;
        db.Updateable(existing).ExecuteCommand();
    }

    private void EnsureUserEmployment(
        SystemUserEntity user,
        string tenantId,
        string appCode,
        string? deptId,
        string? positionId,
        bool isPrimary,
        string status)
    {
        if (string.IsNullOrWhiteSpace(deptId) || string.IsNullOrWhiteSpace(positionId))
        {
            return;
        }

        var normalizedAppCode = appCode.Trim().ToUpperInvariant();
        var employments = db.Queryable<SystemUserEmploymentEntity>()
            .ToList()
            .Where(item =>
                item.UserId == user.Id &&
                item.TenantId == tenantId &&
                string.Equals(item.AppCode, normalizedAppCode, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var existing = employments.FirstOrDefault(item =>
            item.DeptId == deptId &&
            item.PositionId == positionId);

        if (existing is null)
        {
            existing = new SystemUserEmploymentEntity
            {
                UserId = user.Id,
                TenantId = tenantId,
                AppCode = normalizedAppCode,
                DeptId = deptId,
                PositionId = positionId,
                EmploymentName = string.Empty,
                IsPrimary = isPrimary || employments.All(item => item.IsDeleted || item.Status != "Enabled"),
                Status = status,
                SortOrder = employments.Count + 1,
                IsDeleted = false
            };
            db.Insertable(existing).ExecuteCommand();
            employments.Add(existing);
        }
        else
        {
            existing.IsDeleted = false;
            existing.DeletedBy = null;
            existing.DeletedTime = null;
            existing.Status = status;
            existing.IsPrimary = isPrimary || employments.All(item => item.Id == existing.Id || item.IsDeleted || item.Status != "Enabled");
            db.Updateable(existing).ExecuteCommand();
        }

        NormalizePrimaryEmployment(user.Id, tenantId, normalizedAppCode, existing.Id);
    }

    private void NormalizePrimaryEmployment(string userId, string tenantId, string appCode, string preferredEmploymentId)
    {
        var employments = db.Queryable<SystemUserEmploymentEntity>()
            .ToList()
            .Where(item =>
                item.UserId == userId &&
                item.TenantId == tenantId &&
                string.Equals(item.AppCode, appCode, StringComparison.OrdinalIgnoreCase) &&
                !item.IsDeleted &&
                item.Status == "Enabled")
            .OrderByDescending(item => item.Id == preferredEmploymentId && item.IsPrimary)
            .ThenByDescending(item => item.IsPrimary)
            .ThenBy(item => item.SortOrder)
            .ToList();

        if (employments.Count == 0)
        {
            return;
        }

        var primaryId = employments[0].Id;
        foreach (var employment in employments)
        {
            var shouldBePrimary = employment.Id == primaryId;
            if (employment.IsPrimary == shouldBePrimary)
            {
                continue;
            }

            employment.IsPrimary = shouldBePrimary;
            db.Updateable(employment).ExecuteCommand();
        }
    }

    private void UpsertRolePermissions(string roleCode, params string[] permissionCodes)
    {
        var role = db.Queryable<SystemRoleEntity>()
            .ToList()
            .FirstOrDefault(item => item.RoleCode == roleCode && !item.IsDeleted);
        if (role is null)
        {
            return;
        }

        var codes = db.Queryable<SystemPermissionCodeEntity>()
            .Where(item => permissionCodes.Contains(item.PermissionCode) && !item.IsDeleted && item.IsEnabled)
            .ToList();

        var existingPermissionIds = db.Queryable<SystemRolePermissionEntity>()
            .Where(item => item.RoleId == role.Id && !item.IsDeleted)
            .Select(item => item.PermissionCodeId)
            .ToList();

        var records = codes
            .Where(code => !existingPermissionIds.Contains(code.Id))
            .Select(code => new SystemRolePermissionEntity
            {
                RoleId = role.Id,
                PermissionCodeId = code.Id
            })
            .ToArray();

        if (records.Length > 0)
        {
            db.Insertable(records).ExecuteCommand();
        }
    }

    private static string[] FlowiseAdminPermissionCodes() =>
    [
        "flowise:view",
        "flowise:manage",
        "flowise:edit",
        "flowise:run",
        "flowise:chatflows:view",
        "flowise:chatflows:edit",
        "flowise:chatflows:duplicate",
        "flowise:chatflows:export",
        "flowise:chatflows:config",
        "flowise:chatflows:domains",
        "flowise:chatflows:delete",
        "flowise:chatflows:run",
        "flowise:chatflows:share",
        "flowise:chatflows:test",
        "flowise:agentflows:view",
        "flowise:agentflows:edit",
        "flowise:agentflows:duplicate",
        "flowise:agentflows:export",
        "flowise:agentflows:config",
        "flowise:agentflows:domains",
        "flowise:agentflows:delete",
        "flowise:agentflows:run",
        "flowise:executions:view",
        "flowise:executions:manage",
        "flowise:assistants:view",
        "flowise:assistants:edit",
        "flowise:marketplaces:view",
        "flowise:marketplaces:edit",
        "flowise:tools:view",
        "flowise:tools:edit",
        "flowise:tools:create",
        "flowise:tools:update",
        "flowise:tools:delete",
        "flowise:credentials:view",
        "flowise:credentials:edit",
        "flowise:variables:view",
        "flowise:variables:edit",
        "flowise:api-keys:view",
        "flowise:api-keys:edit",
        "flowise:document-stores:view",
        "flowise:document-stores:edit",
        "flowise:document-stores:upsert",
        "flowise:datasets:view",
        "flowise:datasets:edit",
        "flowise:evaluators:view",
        "flowise:evaluators:edit",
        "flowise:evaluations:view",
        "flowise:evaluations:edit",
        "flowise:sso:manage",
        "flowise:roles:manage",
        "flowise:users:manage",
        "flowise:workspaces:view",
        "flowise:workspaces:manage",
        "flowise:login-activity:view",
        "flowise:login-activity:manage",
        "flowise:logs:view",
        "flowise:logs:manage",
        "flowise:account:view",
        "flowise:account:edit",
        "flowise:templates:view",
        "flowise:templates:edit",
        "flowise:templates:flow-export",
        "flowise:secret:reveal"
    ];

    private void UpsertMenu(
        string menuCode,
        string menuName,
        string? parentCode,
        string? routePath,
        string? componentName,
        string menuType,
        int sortOrder,
        bool visible,
        string? permissionCode,
        string? icon,
        string tenantId = "tenant-system",
        string appCode = "SYSTEM",
        string? pageCode = null,
        string? pageSchemaId = null,
        string? scopeType = null,
        string? configJson = null)
    {
        var normalizedAppCode = appCode.Trim().ToUpperInvariant();
        var cache = GetMenuCache();
        var cacheKey = BuildMenuCacheKey(tenantId, normalizedAppCode, menuCode);
        cache.TryGetValue(cacheKey, out var existing);

        if (existing is null)
        {
            existing = new SystemMenuEntity
            {
                TenantId = tenantId,
                AppCode = normalizedAppCode,
                MenuCode = menuCode,
                MenuName = menuName,
                ParentCode = parentCode,
                RoutePath = routePath,
                ComponentName = componentName,
                PageCode = pageCode,
                ArtifactId = pageSchemaId,
                ScopeType = scopeType,
                ConfigJson = configJson,
                MenuType = menuType,
                SortOrder = sortOrder,
                Visible = visible,
                PermissionCode = permissionCode,
                Icon = icon
            };
            db.Insertable(existing).ExecuteCommand();
            cache[cacheKey] = existing;
            return;
        }

        if (existing.MenuName == menuName &&
            existing.ParentCode == parentCode &&
            existing.RoutePath == routePath &&
            existing.ComponentName == componentName &&
            existing.PageCode == pageCode &&
            existing.ArtifactId == pageSchemaId &&
            existing.ScopeType == scopeType &&
            existing.ConfigJson == configJson &&
            existing.MenuType == menuType &&
            existing.SortOrder == sortOrder &&
            existing.Visible == visible &&
            existing.PermissionCode == permissionCode &&
            existing.Icon == icon &&
            !existing.IsDeleted)
        {
            return;
        }

        existing.MenuName = menuName;
        existing.ParentCode = parentCode;
        existing.RoutePath = routePath;
        existing.ComponentName = componentName;
        existing.PageCode = pageCode;
        existing.ArtifactId = pageSchemaId;
        existing.ScopeType = scopeType;
        existing.ConfigJson = configJson;
        existing.MenuType = menuType;
        existing.SortOrder = sortOrder;
        existing.Visible = visible;
        existing.PermissionCode = permissionCode;
        existing.Icon = icon;
        existing.IsDeleted = false;
        existing.UpdatedBy = "development-seed";
        existing.UpdatedTime = DateTime.UtcNow;
        existing.DeletedBy = null;
        existing.DeletedTime = null;
        db.Updateable(existing).ExecuteCommand();
    }

    private void HideMenu(string menuCode)
    {
        var cache = GetMenuCache();
        if (!cache.TryGetValue(BuildMenuCacheKey("tenant-system", "SYSTEM", menuCode), out var existing))
        {
            return;
        }

        if (!existing.Visible && existing.IsDeleted)
        {
            return;
        }

        existing.Visible = false;
        existing.IsDeleted = true;
        existing.UpdatedBy = "development-seed";
        existing.UpdatedTime = DateTime.UtcNow;
        db.Updateable(existing).ExecuteCommand();
    }

    private void RetireLegacyProtectionMenus()
    {
        var now = DateTime.UtcNow;
        var affected = db.Updateable<SystemMenuEntity>()
            .SetColumns(item => new SystemMenuEntity
            {
                Visible = false,
                IsDeleted = true,
                UpdatedBy = "development-seed",
                UpdatedTime = now,
                DeletedBy = "development-seed",
                DeletedTime = now
            })
            .Where(item =>
                !item.IsDeleted &&
                (item.MenuCode == "protection" ||
                 item.MenuCode.StartsWith("protection.") ||
                 item.MenuCode.StartsWith("protection:") ||
                 (item.RoutePath != null && item.RoutePath.StartsWith("/protection")) ||
                 (item.ComponentName != null && item.ComponentName.Contains("Protection")) ||
                 item.MenuName == "程序保护平台" ||
                 item.MenuName == "保护任务" ||
                 item.MenuName == "保护模板" ||
                 item.MenuName == "授权节点" ||
                 item.MenuName == "保护项目" ||
                 item.MenuName == "保护审计" ||
                 item.MenuName == "运维面板"))
            .ExecuteCommand();

        if (affected > 0)
        {
            logger.LogInformation("Development seed retired {Count} legacy protection menus", affected);
        }
    }

    private void RetireLegacyProtectionPermissionCodes()
    {
        var now = DateTime.UtcNow;
        var affected = db.Updateable<SystemPermissionCodeEntity>()
            .SetColumns(item => new SystemPermissionCodeEntity
            {
                IsEnabled = false,
                IsDeleted = true,
                UpdatedBy = "development-seed",
                UpdatedTime = now,
                DeletedBy = "development-seed",
                DeletedTime = now
            })
            .Where(item =>
                !item.IsDeleted &&
                (item.PermissionCode.StartsWith("protection:") ||
                 item.PermissionCode.StartsWith("platform:protection:") ||
                 item.ModuleName.Contains("Protection") ||
                 item.PermissionName.Contains("Protection") ||
                 item.PermissionName.Contains("保护")))
            .ExecuteCommand();

        if (affected > 0)
        {
            logger.LogInformation("Development seed retired {Count} legacy protection permission codes", affected);
        }
    }

    private void RetireWorkspaceMenu(string tenantId, string appCode, string menuCode)
    {
        var normalizedAppCode = appCode.Trim().ToUpperInvariant();
        var cache = GetMenuCache();
        if (!cache.TryGetValue(BuildMenuCacheKey(tenantId, normalizedAppCode, menuCode), out var existing))
        {
            return;
        }

        if (!existing.Visible && existing.IsDeleted)
        {
            return;
        }

        existing.Visible = false;
        existing.IsDeleted = true;
        existing.UpdatedTime = DateTime.UtcNow;
        db.Updateable(existing).ExecuteCommand();
    }

    private void RetireApplicationWorkspaceModuleMenus()
    {
        var now = DateTime.UtcNow;
        var affected = db.Updateable<SystemMenuEntity>()
            .SetColumns(item => new SystemMenuEntity
            {
                Visible = false,
                IsDeleted = true,
                UpdatedBy = "development-seed",
                UpdatedTime = now,
                DeletedBy = "development-seed",
                DeletedTime = now
            })
            .Where(item =>
                !item.IsDeleted &&
                item.AppCode != "SYSTEM" &&
                (item.MenuCode == "tenant" ||
                 item.MenuCode.StartsWith("tenant:") ||
                 item.MenuCode == "runtime" ||
                 item.MenuCode.StartsWith("runtime:") ||
                 item.MenuCode == "workflow" ||
                 item.MenuCode.StartsWith("workflow:") ||
                 item.MenuCode == "ai" ||
                 item.MenuCode.StartsWith("ai:") ||
                 item.MenuCode == "flowise" ||
                 item.MenuCode.StartsWith("flowise:") ||
                 item.MenuCode == "asterscene" ||
                 item.MenuCode.StartsWith("asterscene:")))
            .ExecuteCommand();

        if (affected > 0)
        {
            logger.LogInformation("Development seed retired {Count} imported module menus from application workspaces", affected);
        }
    }

    private Dictionary<string, SystemMenuEntity> GetMenuCache()
    {
        if (menuCache is not null)
        {
            return menuCache;
        }

        menuCache = db.Queryable<SystemMenuEntity>()
            .ToList()
            .GroupBy(item => BuildMenuCacheKey(item.TenantId, item.AppCode, item.MenuCode), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        return menuCache;
    }

    private static string BuildMenuCacheKey(string tenantId, string appCode, string menuCode)
        => $"{tenantId}::{appCode.Trim().ToUpperInvariant()}::{menuCode}";

    private void UpsertTenantAdminMenus(string tenantId, string appCode)
    {
        UpsertMenu("tenant", "租户管理", null, null, null, "Directory", 4, true, null, "Building", tenantId, appCode);
        UpsertMenu("tenant:apps", "应用安装", "tenant", "/tenant/apps", "TenantAppsPage", "Menu", 1, true, "tenant:app:query", "Boxes", tenantId, appCode);
    }

    private void UpsertBpmMenusForEnabledWorkspaces()
    {
        var enabledWorkspaces = db.Queryable<SystemTenantAppEntity>()
            .Where(item => !item.IsDeleted && item.Status == "Enabled")
            .ToList()
            .Where(item => !string.Equals(item.TenantId, "tenant-system", StringComparison.OrdinalIgnoreCase) ||
                           !string.Equals(item.AppCode, "SYSTEM", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var workspace in enabledWorkspaces)
        {
            UpsertBpmMenuTree(workspace.TenantId, workspace.AppCode, 3);
        }
    }

    private void UpsertBpmMenuTree(string tenantId, string appCode, int rootSort)
    {
        UpsertMenu("workflow", "审批流", null, null, null, "Directory", rootSort, true, null, "Workflow", tenantId, appCode);
        UpsertMenu("workflow:workspace", "个人工作台", "workflow", null, null, "Directory", 1, true, null, "UserCheck", tenantId, appCode);
        UpsertMenu("workflow:management", "流程管理", "workflow", null, null, "Directory", 2, true, null, "GitBranch", tenantId, appCode);
        UpsertMenu("workflow:analytics", "统计报表", "workflow", null, null, "Directory", 3, true, null, "ChartNoAxesCombined", tenantId, appCode);
        UpsertMenu("workflow:settings", "系统与基础设置", "workflow", null, null, "Directory", 4, true, null, "Settings2", tenantId, appCode);

        UpsertBpmWorkspaceMenus(tenantId, appCode);
        UpsertBpmManagementMenus(tenantId, appCode);
        UpsertBpmAnalyticsMenus(tenantId, appCode);
        UpsertBpmSettingsMenus(tenantId, appCode);
        UpsertDefaultWorkflowCategories(tenantId, appCode);
        RetireBpmLegacyMenus(tenantId, appCode);
    }

    private void UpsertBpmWorkspaceMenus(string tenantId, string appCode)
    {
        UpsertMenu("workflow:initiate", "发起申请", "workflow:workspace", "/workflows/initiate", "WorkflowInitiatePage", "Menu", 1, true, "workflow:instance:start", "Send", tenantId, appCode);
        UpsertMenu("workflow:todo", "待办审批", "workflow:workspace", "/workflows/tasks?tab=todo", "WorkflowTasksPage", "Menu", 2, true, "workflow:task:query", "ListChecks", tenantId, appCode);
        UpsertMenu("workflow:done", "已办审批", "workflow:workspace", "/workflows/tasks?tab=done", "WorkflowTasksPage", "Menu", 3, true, "workflow:task:query", "CheckCheck", tenantId, appCode);
        UpsertMenu("workflow:mine", "我发起的", "workflow:workspace", "/workflows/tasks?tab=mine", "WorkflowTasksPage", "Menu", 4, true, "workflow:instance:query", "FileClock", tenantId, appCode);
        UpsertMenu("workflow:cc", "抄送我的", "workflow:workspace", "/workflows/tasks?tab=cc", "WorkflowTasksPage", "Menu", 5, true, "workflow:task:query", "AtSign", tenantId, appCode);
        UpsertMenu("workflow:drafts", "草稿箱", "workflow:workspace", "/workflows/drafts", "WorkflowDraftsPage", "Menu", 6, true, "workflow:draft:query", "FilePenLine", tenantId, appCode);
        UpsertMenu("workflow:history", "审批记录", "workflow:workspace", "/workflows/history", "WorkflowHistoryPage", "Menu", 7, true, "workflow:history:query", "History", tenantId, appCode);

        UpsertMenu("workflow:task:claim", "认领任务", "workflow:todo", null, null, "Button", 1, true, "workflow:task:claim", null, tenantId, appCode);
        UpsertMenu("workflow:task:approve", "审批任务", "workflow:todo", null, null, "Button", 2, true, "workflow:task:approve", null, tenantId, appCode);
        UpsertMenu("workflow:task:transfer", "转办任务", "workflow:todo", null, null, "Button", 3, true, "workflow:task:transfer", null, tenantId, appCode);
        UpsertMenu("workflow:task:delegate", "委派任务", "workflow:todo", null, null, "Button", 4, true, "workflow:task:delegate", null, tenantId, appCode);
        UpsertMenu("workflow:task:attachment", "附件", "workflow:todo", null, null, "Button", 5, true, "workflow:task:attachment", null, tenantId, appCode);
        UpsertMenu("workflow:task:comment", "评论", "workflow:todo", null, null, "Button", 6, true, "workflow:task:comment", null, tenantId, appCode);
        UpsertMenu("workflow:draft:edit", "保存草稿", "workflow:drafts", null, null, "Button", 1, true, "workflow:draft:edit", null, tenantId, appCode);
        UpsertMenu("workflow:draft:submit", "提交草稿", "workflow:drafts", null, null, "Button", 2, true, "workflow:draft:submit", null, tenantId, appCode);
        UpsertMenu("workflow:draft:delete", "删除草稿", "workflow:drafts", null, null, "Button", 3, true, "workflow:draft:delete", null, tenantId, appCode);
    }

    private void UpsertBpmManagementMenus(string tenantId, string appCode)
    {
        UpsertMenu("workflow:forms", "表单管理", "workflow:management", "/workflows/forms", "WorkflowFormsPage", "Menu", 1, true, "workflow:form:query", "FormInput", tenantId, appCode);
        UpsertMenu("workflow:models", "流程设计", "workflow:management", "/workflows/models", "WorkflowModelsPage", "Menu", 2, true, "workflow:model:query", "Workflow", tenantId, appCode);
        UpsertMenu("workflow:bindings", "审批配置", "workflow:management", "/workflows/bindings", "WorkflowBindingsPage", "Menu", 3, true, "workflow:binding:query", "GitPullRequestArrow", tenantId, appCode);
        UpsertMenu("workflow:categories", "流程分类", "workflow:management", "/workflows/categories", "WorkflowCategoriesPage", "Menu", 4, true, "workflow:category:query", "FolderTree", tenantId, appCode);
        UpsertMenu("workflow:monitoring", "流程监控", "workflow:management", "/workflows/monitoring", "WorkflowMonitoringPage", "Menu", 5, true, "workflow:instance:query", "Radar", tenantId, appCode);

        UpsertMenu("workflow:model:add", "新增模型", "workflow:models", null, null, "Button", 1, true, "workflow:model:add", null, tenantId, appCode);
        UpsertMenu("workflow:model:edit", "编辑模型", "workflow:models", null, null, "Button", 2, true, "workflow:model:edit", null, tenantId, appCode);
        UpsertMenu("workflow:model:delete", "删除模型", "workflow:models", null, null, "Button", 3, true, "workflow:model:delete", null, tenantId, appCode);
        UpsertMenu("workflow:model:publish", "发布模型", "workflow:models", null, null, "Button", 4, true, "workflow:model:publish", null, tenantId, appCode);
        UpsertMenu("workflow:model:suspend", "停用模型", "workflow:models", null, null, "Button", 5, true, "workflow:model:suspend", null, tenantId, appCode);
        UpsertMenu("workflow:binding:edit", "保存审批配置", "workflow:bindings", null, null, "Button", 1, true, "workflow:binding:edit", null, tenantId, appCode);
        UpsertMenu("workflow:binding:delete", "删除审批配置", "workflow:bindings", null, null, "Button", 2, true, "workflow:binding:delete", null, tenantId, appCode);
        UpsertMenu("workflow:category:edit", "保存分类", "workflow:categories", null, null, "Button", 1, true, "workflow:category:edit", null, tenantId, appCode);
        UpsertMenu("workflow:category:delete", "删除分类", "workflow:categories", null, null, "Button", 2, true, "workflow:category:delete", null, tenantId, appCode);
    }

    private void UpsertBpmAnalyticsMenus(string tenantId, string appCode)
    {
        UpsertMenu("workflow:report:approval", "审批统计", "workflow:analytics", "/workflows/reports?tab=approval", "WorkflowReportsPage", "Menu", 1, true, "workflow:report:query", "ChartColumn", tenantId, appCode);
        UpsertMenu("workflow:report:efficiency", "效率分析", "workflow:analytics", "/workflows/reports?tab=efficiency", "WorkflowReportsPage", "Menu", 2, true, "workflow:report:query", "Timer", tenantId, appCode);
        UpsertMenu("workflow:report:business", "业务数据", "workflow:analytics", "/workflows/reports?tab=business", "WorkflowReportsPage", "Menu", 3, true, "workflow:report:query", "DatabaseZap", tenantId, appCode);
    }

    private void UpsertBpmSettingsMenus(string tenantId, string appCode)
    {
        UpsertMenu("workflow:settings:org", "组织架构与用户", "workflow:settings", "/system/users?from=workflow", "UsersPage", "Menu", 1, true, "system:user:query", "Users", tenantId, appCode);
        UpsertMenu("workflow:settings:roles", "角色与权限组", "workflow:settings", "/system/roles?from=workflow", "RolesPage", "Menu", 2, true, "system:role:query", "ShieldCheck", tenantId, appCode);
        UpsertMenu("workflow:delegations", "审批委托", "workflow:settings", "/workflows/delegations", "WorkflowDelegationsPage", "Menu", 3, true, "workflow:delegation:query", "UserRoundCog", tenantId, appCode);
        UpsertMenu("workflow:notifications", "消息通知设置", "workflow:settings", "/workflows/notifications", "WorkflowNotificationsPage", "Menu", 4, true, "workflow:notification:task:query", "BellRing", tenantId, appCode);
        UpsertMenu("workflow:calendars", "节假日/工作日历", "workflow:settings", "/workflows/calendars", "WorkflowCalendarsPage", "Menu", 5, true, "workflow:calendar:query", "CalendarDays", tenantId, appCode);

        UpsertMenu("workflow:delegation:edit", "保存委托", "workflow:delegations", null, null, "Button", 1, true, "workflow:delegation:edit", null, tenantId, appCode);
        UpsertMenu("workflow:delegation:delete", "删除委托", "workflow:delegations", null, null, "Button", 2, true, "workflow:delegation:delete", null, tenantId, appCode);
        UpsertMenu("workflow:notification:channel", "渠道配置", "workflow:notifications", null, null, "Button", 1, true, "workflow:notification:channel:query", null, tenantId, appCode);
        UpsertMenu("workflow:notification:template", "消息模板", "workflow:notifications", null, null, "Button", 2, true, "workflow:notification:template:query", null, tenantId, appCode);
        UpsertMenu("workflow:notification:rule", "通知规则", "workflow:notifications", null, null, "Button", 3, true, "workflow:notification:rule:query", null, tenantId, appCode);
        UpsertMenu("workflow:notification:task", "通知任务", "workflow:notifications", null, null, "Button", 4, true, "workflow:notification:task:query", null, tenantId, appCode);
        UpsertMenu("workflow:notification:log", "通知日志", "workflow:notifications", null, null, "Button", 5, true, "workflow:notification:log:query", null, tenantId, appCode);
        UpsertMenu("workflow:calendar:edit", "保存日历", "workflow:calendars", null, null, "Button", 1, true, "workflow:calendar:edit", null, tenantId, appCode);
        UpsertMenu("workflow:calendar:delete", "删除日历", "workflow:calendars", null, null, "Button", 2, true, "workflow:calendar:delete", null, tenantId, appCode);
    }

    private void UpsertDefaultWorkflowCategories(string tenantId, string appCode)
    {
        UpsertWorkflowCategory(tenantId, appCode, "HR", "人事类", 10);
        UpsertWorkflowCategory(tenantId, appCode, "FINANCE", "财务类", 20);
        UpsertWorkflowCategory(tenantId, appCode, "ADMIN", "行政类", 30);
        UpsertWorkflowCategory(tenantId, appCode, "BUSINESS", "业务类", 40);
    }

    private void UpsertWorkflowCategory(string tenantId, string appCode, string categoryCode, string categoryName, int sortOrder)
    {
        var normalizedAppCode = appCode.Trim().ToUpperInvariant();
        var existing = db.Queryable<WorkflowCategoryEntity>()
            .First(item =>
                item.TenantId == tenantId &&
                item.AppCode == normalizedAppCode &&
                item.CategoryCode == categoryCode);

        if (existing is null)
        {
            db.Insertable(new WorkflowCategoryEntity
            {
                TenantId = tenantId,
                AppCode = normalizedAppCode,
                CategoryCode = categoryCode,
                CategoryName = categoryName,
                SortOrder = sortOrder,
                IsEnabled = true,
                CreatedBy = "development-seed"
            }).ExecuteCommand();
            return;
        }

        existing.CategoryName = categoryName;
        existing.SortOrder = sortOrder;
        existing.IsEnabled = true;
        existing.IsDeleted = false;
        existing.UpdatedBy = "development-seed";
        existing.UpdatedTime = DateTime.UtcNow;
        existing.DeletedBy = null;
        existing.DeletedTime = null;
        db.Updateable(existing).ExecuteCommand();
    }

    private void RetireBpmLegacyMenus(string tenantId, string appCode)
    {
        RetireWorkspaceMenu(tenantId, appCode, "workflow:tasks");
    }

    private void UpsertDataModel(
        string tenantId,
        string appCode,
        string modelCode,
        string modelName,
        string providerKey,
        string keyField,
        string? permissionCode,
        string schemaJson)
    {
        var normalizedAppCode = appCode.Trim().ToUpperInvariant();
        var cache = GetDataModelCache();
        var cacheKey = BuildDataModelCacheKey(tenantId, normalizedAppCode, modelCode);
        cache.TryGetValue(cacheKey, out var existing);

        if (existing is null)
        {
            existing = new SystemDataModelEntity
            {
                Id = ResolveDataModelId(tenantId, normalizedAppCode, modelCode),
                TenantId = tenantId,
                AppCode = normalizedAppCode,
                ModelCode = modelCode,
                ModelName = modelName,
                ProviderKey = providerKey,
                KeyField = keyField,
                PermissionCode = permissionCode,
                VersionNo = 1,
                Status = "Published",
                SchemaJson = schemaJson,
                Remark = "P2 runtime data model seed"
            };
            db.Insertable(existing).ExecuteCommand();
            cache[cacheKey] = existing;
            return;
        }

        if (existing.ModelName == modelName &&
            existing.ProviderKey == providerKey &&
            existing.KeyField == keyField &&
            existing.PermissionCode == permissionCode &&
            existing.SchemaJson == schemaJson &&
            existing.Status == "Published" &&
            !existing.IsDeleted)
        {
            return;
        }

        existing.ModelName = modelName;
        existing.ProviderKey = providerKey;
        existing.KeyField = keyField;
        existing.PermissionCode = permissionCode;
        existing.SchemaJson = schemaJson;
        existing.Status = "Published";
        existing.IsDeleted = false;
        existing.UpdatedTime = DateTime.UtcNow;
        db.Updateable(existing).ExecuteCommand();
    }

    private Dictionary<string, SystemDataModelEntity> GetDataModelCache()
    {
        if (dataModelCache is not null)
        {
            return dataModelCache;
        }

        dataModelCache = db.Queryable<SystemDataModelEntity>()
            .Where(item => item.Status == "Published" && !item.IsDeleted)
            .ToList()
            .GroupBy(item => BuildDataModelCacheKey(item.TenantId, item.AppCode, item.ModelCode), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        return dataModelCache;
    }

    private static string BuildDataModelCacheKey(string tenantId, string appCode, string modelCode)
        => $"{tenantId}::{appCode.Trim().ToUpperInvariant()}::{modelCode}";

    private static string ResolveDataModelId(string tenantId, string appCode, string modelCode)
    {
        var key = $"{tenantId}-{appCode}-{modelCode}".ToLowerInvariant();
        var normalized = new string(key.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray());
        return $"data-model-{normalized}";
    }

    private void UpsertTenant(
        string id,
        string tenantCode,
        string tenantName,
        string? shortName,
        string status,
        DateTime? expiredAt,
        string? contactName,
        string? contactPhone,
        string? configJson)
    {
        var existing = db.Queryable<SystemTenantEntity>()
            .ToList()
            .FirstOrDefault(item => item.Id == id || item.TenantCode == tenantCode);

        if (existing is null)
        {
            db.Insertable(new SystemTenantEntity
            {
                Id = id,
                TenantCode = tenantCode,
                TenantName = tenantName,
                ShortName = shortName,
                Status = status,
                ExpiredAt = expiredAt,
                ContactName = contactName,
                ContactPhone = contactPhone,
                ConfigJson = configJson
            }).ExecuteCommand();
            return;
        }

        existing.TenantCode = tenantCode;
        existing.TenantName = tenantName;
        existing.ShortName = shortName;
        existing.Status = status;
        existing.ExpiredAt = expiredAt;
        existing.ContactName = contactName;
        existing.ContactPhone = contactPhone;
        existing.ConfigJson = configJson;
        existing.IsDeleted = false;
        db.Updateable(existing).ExecuteCommand();
    }

    private void UpsertApplication(
        string appCode,
        string appName,
        string appType,
        string? icon,
        string? defaultRoutePath,
        string? adminDefaultRoutePath,
        string? runtimeDefaultRoutePath,
        string status,
        string? version,
        string? remark)
    {
        var existing = db.Queryable<SystemApplicationEntity>()
            .ToList()
            .FirstOrDefault(item => item.AppCode == appCode);

        if (existing is null)
        {
            db.Insertable(new SystemApplicationEntity
            {
                AppCode = appCode,
                AppName = appName,
                AppType = appType,
                Icon = icon,
                DefaultRoutePath = defaultRoutePath,
                AdminDefaultRoutePath = adminDefaultRoutePath ?? defaultRoutePath,
                RuntimeDefaultRoutePath = runtimeDefaultRoutePath,
                Status = status,
                Version = version,
                Remark = remark
            }).ExecuteCommand();
            return;
        }

        existing.AppName = appName;
        existing.AppType = appType;
        existing.Icon = icon;
        existing.DefaultRoutePath = defaultRoutePath;
        existing.AdminDefaultRoutePath = adminDefaultRoutePath ?? defaultRoutePath;
        existing.RuntimeDefaultRoutePath = runtimeDefaultRoutePath;
        existing.Status = status;
        existing.Version = version;
        existing.Remark = remark;
        existing.IsDeleted = false;
        db.Updateable(existing).ExecuteCommand();
    }

    private void UpsertTenantApp(
        string tenantId,
        string appCode,
        string status,
        string? systemName,
        string? logoFileId,
        string? faviconFileId,
        string? primaryColor,
        DateTime? expiredAt,
        string? configJson)
    {
        var existing = db.Queryable<SystemTenantAppEntity>()
            .ToList()
            .FirstOrDefault(item => item.TenantId == tenantId && item.AppCode == appCode);

        if (existing is null)
        {
            db.Insertable(new SystemTenantAppEntity
            {
                TenantId = tenantId,
                AppCode = appCode,
                Status = status,
                SystemName = systemName,
                LogoFileId = logoFileId,
                FaviconFileId = faviconFileId,
                PrimaryColor = primaryColor,
                ExpiredAt = expiredAt,
                ConfigJson = configJson
            }).ExecuteCommand();
            return;
        }

        existing.Status = status;
        existing.SystemName = systemName;
        existing.LogoFileId = logoFileId;
        existing.FaviconFileId = faviconFileId;
        existing.PrimaryColor = primaryColor;
        existing.ExpiredAt = expiredAt;
        existing.ConfigJson = configJson ?? existing.ConfigJson;
        existing.IsDeleted = false;
        db.Updateable(existing).ExecuteCommand();
    }

    private string ResolveDevelopmentTenantAppConfig(
        string tenantId,
        string appCode,
        string displayName,
        string databaseName,
        params string[] shellCapabilities)
    {
        var existingConfigJson = db.Queryable<SystemTenantAppEntity>()
            .Where(item => item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted)
            .Select(item => item.ConfigJson)
            .First();
        var configJson = string.IsNullOrWhiteSpace(existingConfigJson)
            ? BuildDevelopmentApplicationDatabaseConfig(displayName, databaseName)
            : existingConfigJson;

        return shellCapabilities.Length == 0
            ? configJson
            : MergeShellCapabilities(configJson, shellCapabilities);
    }

    private static string MergeShellCapabilities(string configJson, IReadOnlyCollection<string> shellCapabilities)
    {
        var root = JsonNode.Parse(configJson)?.AsObject() ?? [];
        root["shellCapabilities"] = new JsonArray(shellCapabilities
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(item => JsonValue.Create(item.Trim()))
            .ToArray<JsonNode?>());
        return root.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static string BuildDevelopmentApplicationDatabaseConfig(string displayName, string databaseName)
    {
        var root = new JsonObject
        {
            ["applicationDatabase"] = new JsonObject
            {
                ["provider"] = "Sqlite",
                ["connectionStringCipherText"] = "development-managed-sqlite",
                ["displayName"] = displayName,
                ["databaseName"] = databaseName,
                ["updatedAt"] = DateTime.UnixEpoch.ToString("O"),
                ["updatedBy"] = "development.seed"
            }
        };

        return root.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private void EnsureScheduledJob(
        string jobCode,
        string jobName,
        string jobType,
        string? presetJobCode,
        string status,
        object scheduleConfig,
        string cronExpression,
        string friendlySchedule,
        object? httpCallback,
        string remark)
    {
        var existing = db.Queryable<SystemScheduledJobEntity>()
            .ToList()
            .FirstOrDefault(item => item.JobCode == jobCode);

        var scheduleJson = JsonSerializer.Serialize(scheduleConfig, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var callbackJson = httpCallback is null
            ? null
            : JsonSerializer.Serialize(httpCallback, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        if (existing is null)
        {
            db.Insertable(new SystemScheduledJobEntity
            {
                JobCode = jobCode,
                JobName = jobName,
                JobType = jobType,
                PresetJobCode = presetJobCode,
                Status = status,
                ScheduleKind = scheduleConfig.GetType().GetProperty("kind")?.GetValue(scheduleConfig)?.ToString() ?? "Daily",
                TimeZoneId = "China Standard Time",
                ScheduleConfigJson = scheduleJson,
                HttpCallbackJson = callbackJson,
                CronExpression = cronExpression,
                FriendlySchedule = friendlySchedule,
                ScheduleSyncStatus = "Pending",
                Remark = remark
            }).ExecuteCommand();
            return;
        }

        existing.JobName = jobName;
        existing.JobType = jobType;
        existing.PresetJobCode = presetJobCode;
        existing.ScheduleConfigJson = scheduleJson;
        existing.HttpCallbackJson = callbackJson;
        existing.CronExpression = cronExpression;
        existing.FriendlySchedule = friendlySchedule;
        existing.TimeZoneId = "China Standard Time";
        existing.Remark = remark;
        existing.IsDeleted = false;
        db.Updateable(existing).ExecuteCommand();
    }
}
