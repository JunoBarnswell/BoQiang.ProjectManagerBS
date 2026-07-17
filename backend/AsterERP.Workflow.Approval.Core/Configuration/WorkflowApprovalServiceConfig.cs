using AsterERP.Workflow.Approval.Core.Services.Base;
using AsterERP.Workflow.Approval.Core.Services.Workflow;
using AsterERP.Workflow.Approval.Core.Services.Hr;
using AsterERP.Workflow.Approval.Core.Services.Org;
using AsterERP.Workflow.Approval.Core.Services.Privilege;
using Microsoft.Extensions.DependencyInjection;

namespace AsterERP.Workflow.Approval.Core.Configuration;

public static class WorkflowApprovalServiceConfig
{
    public static IServiceCollection AddAsterERPWorkflowApprovalServices(this IServiceCollection services)
    {
        services.AddAsterERPWorkflowGlobalListeners();

        services.AddScoped<IAppService, AppService>();
        services.AddScoped<IAreaService, AreaService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IDicItemService, DicItemService>();
        services.AddScoped<IDicTypeService, DicTypeService>();
        services.AddScoped<IDictionaryService, DictionaryService>();
        services.AddScoped<ISystemConfigService, SystemConfigService>();

        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IJobGradeService, JobGradeService>();
        services.AddScoped<IJobGradeTypeService, JobGradeTypeService>();
        services.AddScoped<IPersonalService, PersonalService>();
        services.AddScoped<IPersonalRoleService, PersonalRoleService>();
        services.AddScoped<IPositionInfoService, PositionInfoService>();
        services.AddScoped<IPositionSeqService, PositionSeqService>();
        services.AddScoped<IRolePositionPersonalService, RolePositionPersonalService>();
        services.AddScoped<IRoleService, RoleService>();

        services.AddScoped<IAclService, AclService>();
        services.AddScoped<IAppPrivilegeValueService, AppPrivilegeValueService>();
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<ILoginLogService, LoginLogService>();
        services.AddScoped<IModuleService, ModuleService>();
        services.AddScoped<SystemInitializer>();
        services.AddScoped<IShiroSessionService, ShiroSessionService>();
        services.AddScoped<IUserGroupService, UserGroupService>();
        services.AddScoped<IUserService, UserService>();

        services.AddScoped<ICommentInfoService, CommentInfoService>();
        services.AddScoped<IExtendHisprocinstService, ExtendHisprocinstService>();
        services.AddScoped<IExtendProcinstService, ExtendProcinstService>();
        services.AddScoped<IFlowListenerParamService, FlowListenerParamService>();
        services.AddScoped<IFlowListenerService, FlowListenerService>();
        services.AddScoped<IWorkflowActivityInstanceService, WorkflowActivityInstanceService>();
        services.AddScoped<IBpmnModelService, BpmnModelService>();
        services.AddScoped<IWorkflowBpmnDefinitionService, WorkflowBpmnDefinitionService>();
        services.AddScoped<IWorkflowModelRuntimeService, WorkflowModelRuntimeService>();
        services.AddScoped<IWorkflowProcessInstanceRuntimeService, WorkflowProcessInstanceRuntimeService>();
        services.AddScoped<IWorkflowTaskRuntimeService, WorkflowTaskRuntimeService>();
        services.AddScoped<IFlowProcessDiagramService, FlowProcessDiagramService>();
        services.AddScoped<IModelInfoService, ModelInfoService>();
        services.AddScoped<ILeaveService, LeaveService>();

        return services;
    }
}
