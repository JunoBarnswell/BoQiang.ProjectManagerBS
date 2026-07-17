using AsterERP.Workflow.Approval.Core.Repositories.Workflow;
using AsterERP.Workflow.Approval.Core.Repositories.Base;
using AsterERP.Workflow.Approval.Core.Repositories.Model;
using AsterERP.Workflow.Approval.Core.Repositories.Hr;
using AsterERP.Workflow.Approval.Core.Repositories.Org;
using AsterERP.Workflow.Approval.Core.Repositories.Privilege;
using AsterERP.Workflow.DependencyInjection;
using AsterERP.Workflow.Persistence.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SqlSugar;

namespace AsterERP.Workflow.Approval.Core.Configuration;

public static class OrmConfig
{
    public static IServiceCollection AddAsterERPWorkflowApprovalOrm(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("Default")
            ?? configuration.GetConnectionString("FlowMaster")
            ?? "DataSource=flowmaster.db";
        var dbTypeText = configuration["Database:DbType"];
        var dbType = TryParseDbType(dbTypeText, out var parsedDbType) ? parsedDbType : DbType.Sqlite;

        services.TryAddScoped<ISqlSugarClient>(_ =>
        {
            var config = new ConnectionConfig
            {
                ConnectionString = connectionString,
                DbType = dbType,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            };
            return new SqlSugarScope(config);
        });
        services.TryAddScoped<SqliteSchemaValidator>();
        services.TryAddScoped<DatabaseInitializer>();
        services.AddAsterERPWorkflow(connectionString, dbType);

        services.AddScoped<IAppRepository, AppRepository>();
        services.AddScoped<IAreaRepository, AreaRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IDicItemRepository, DicItemRepository>();
        services.AddScoped<IDicTypeRepository, DicTypeRepository>();
        services.AddScoped<IDictionaryRepository, DictionaryRepository>();
        services.AddScoped<ISystemConfigRepository, SystemConfigRepository>();

        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IJobGradeRepository, JobGradeRepository>();
        services.AddScoped<IJobGradeTypeRepository, JobGradeTypeRepository>();
        services.AddScoped<IPersonalRepository, PersonalRepository>();
        services.AddScoped<IPersonalRoleRepository, PersonalRoleRepository>();
        services.AddScoped<IPositionInfoRepository, PositionInfoRepository>();
        services.AddScoped<IPositionSeqRepository, PositionSeqRepository>();
        services.AddScoped<IRolePositionPersonalRepository, RolePositionPersonalRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();

        services.AddScoped<IAclRepository, AclRepository>();
        services.AddScoped<IAppPrivilegeValueRepository, AppPrivilegeValueRepository>();
        services.AddScoped<IGroupRepository, GroupRepository>();
        services.AddScoped<ILoginLogRepository, LoginLogRepository>();
        services.AddScoped<IModuleRepository, ModuleRepository>();
        services.AddScoped<IShiroSessionRepository, ShiroSessionRepository>();
        services.AddScoped<IUserGroupRepository, UserGroupRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        services.AddScoped<ICommentInfoRepository, CommentInfoRepository>();
        services.AddScoped<IExtendHisprocinstRepository, ExtendHisprocinstRepository>();
        services.AddScoped<IExtendProcinstRepository, ExtendProcinstRepository>();
        services.AddScoped<IFlowListenerParamRepository, FlowListenerParamRepository>();
        services.AddScoped<IFlowListenerRepository, FlowListenerRepository>();
        services.AddScoped<IWorkflowHistoricActivityRepository, WorkflowHistoricActivityRepository>();
        services.AddScoped<IWorkflowRuntimeActivityRepository, WorkflowRuntimeActivityRepository>();
        services.AddScoped<IWorkflowProcessDefinitionRepository, WorkflowProcessDefinitionRepository>();
        services.AddScoped<IModelInfoRepository, ModelInfoRepository>();
        services.AddScoped<IWorkflowProcessInstanceRepository, WorkflowProcessInstanceRepository>();
        services.AddScoped<IWorkflowTaskRepository, WorkflowTaskRepository>();
        services.AddScoped<WorkflowModelRepository>();

        services.AddScoped<ILeaveRepository, LeaveRepository>();

        return services;
    }

    private static bool TryParseDbType(string? value, out DbType dbType)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && Enum.TryParse<DbType>(value, ignoreCase: true, out dbType))
        {
            return true;
        }

        dbType = default;
        return false;
    }
}
