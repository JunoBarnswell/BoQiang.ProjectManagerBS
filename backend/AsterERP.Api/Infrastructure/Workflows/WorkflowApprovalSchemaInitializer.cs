using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.Workflows;
using AsterERP.Workflow.Approval.Api.Models.Base;
using AsterERP.Workflow.Approval.Api.Models.Workflow;
using AsterERP.Workflow.Approval.Api.Models.Hr;
using AsterERP.Workflow.Approval.Api.Models.Org;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Forms.Api.Models.Form;
using SqlSugar;
using FlowDictionary = AsterERP.Workflow.Approval.Api.Models.Base.Dictionary;
using FlowModule = AsterERP.Workflow.Approval.Api.Models.Privilege.Module;

namespace AsterERP.Api.Infrastructure.Workflows;

public sealed class WorkflowApprovalSchemaInitializer(ISqlSugarClient db)
{
    public void Initialize() => Initialize(db);

    public void Initialize(ISqlSugarClient targetDb)
    {
        targetDb.CodeFirst.InitTables(
            typeof(App),
            typeof(Area),
            typeof(Category),
            typeof(DicItem),
            typeof(DicType),
            typeof(FlowDictionary),
            typeof(SystemConfig),
            typeof(Company),
            typeof(Department),
            typeof(JobGrade),
            typeof(JobGradeType),
            typeof(Personal),
            typeof(PersonalRole),
            typeof(PositionInfo),
            typeof(PositionSeq),
            typeof(Role),
            typeof(RolePositionPersonal),
            typeof(Acl),
            typeof(AppPrivilegeValue),
            typeof(Group),
            typeof(LoginLog),
            typeof(FlowModule),
            typeof(ShiroSession),
            typeof(User),
            typeof(UserGroup),
            typeof(Leave),
            typeof(WorkflowBindingEntity),
            typeof(WorkflowBusinessInstanceEntity),
            typeof(WorkflowCallbackLogEntity),
            typeof(WorkflowModelExtensionEntity),
            typeof(WorkflowNotificationChannelEntity),
            typeof(WorkflowMessageTemplateEntity),
            typeof(WorkflowNodeNotificationRuleEntity),
            typeof(WorkflowNotificationTaskEntity),
            typeof(WorkflowNotificationLogEntity),
            typeof(WorkflowCategoryEntity),
            typeof(WorkflowRequestDraftEntity),
            typeof(WorkflowDelegationRuleEntity),
            typeof(WorkflowWorkCalendarEntity),
            typeof(ModelInfo),
            typeof(ExtendProcinst),
            typeof(ExtendHisprocinst),
            typeof(CommentInfo),
            typeof(FlowListener),
            typeof(FlowListenerParam),
            typeof(WorkflowRuntimeActivityRecord),
            typeof(FormInfo),
            typeof(FormDataInfo));

        var schema = new SqliteSchemaExecutor(targetDb);
        schema.CreateIndexIfColumnsExist("workflow_bindings", "idx_workflow_bindings_unique", "TenantId", "AppCode", "MenuCode", "BusinessType");
        schema.CreateIndexIfColumnsExist("workflow_bindings", "idx_workflow_bindings_model", "ModelId");
        schema.EnsureColumn("workflow_bindings", "FormResourceCode", "TEXT NULL");
        schema.EnsureColumn("workflow_bindings", "PageCode", "TEXT NULL");
        schema.EnsureColumn("workflow_bindings", "ModelCode", "TEXT NULL");
        schema.EnsureColumn("workflow_bindings", "KeyField", "TEXT NULL");
        schema.EnsureColumn("workflow_bindings", "DetailRoute", "TEXT NULL");
        schema.EnsureColumn("workflow_bindings", "TitleTemplate", "TEXT NULL");
        schema.EnsureColumn("workflow_bindings", "StatusField", "TEXT NULL");
        schema.CreateIndexIfColumnsExist("workflow_bindings", "idx_workflow_bindings_form_resource", "TenantId", "AppCode", "FormResourceCode");
        schema.CreateIndexIfColumnsExist("workflow_bindings", "idx_workflow_bindings_page_model", "TenantId", "AppCode", "PageCode", "ModelCode");
        schema.CreateIndexIfColumnsExist("workflow_business_instances", "idx_workflow_business_instances_business", "TenantId", "AppCode", "MenuCode", "BusinessType", "BusinessKey");
        schema.CreateIndexIfColumnsExist("workflow_business_instances", "idx_workflow_business_instances_procinst", "ProcessInstanceId");
        schema.CreateIndexIfColumnsExist("workflow_callback_logs", "idx_workflow_callback_logs_instance", "ProcessInstanceId", "Trigger", "CreatedTime");
        schema.CreateIndexIfColumnsExist("workflow_callback_logs", "idx_workflow_callback_logs_target", "TenantId", "AppCode", "TargetModelCode", "TargetKey");
        schema.CreateIndexIfColumnsExist("workflow_model_extensions", "idx_workflow_model_extensions_model", "ModelId");
        schema.CreateIndexIfColumnsExist("wf_notification_channel", "idx_wf_notification_channel_scope", "TenantId", "AppCode", "ChannelCode", "IsDeleted");
        schema.CreateIndexIfColumnsExist("wf_message_template", "idx_wf_message_template_scope", "TenantId", "AppCode", "TemplateCode", "IsDeleted");
        schema.CreateIndexIfColumnsExist("wf_node_notification_rule", "idx_wf_node_notification_rule_lookup", "TenantId", "AppCode", "ProcessDefinitionKey", "NodeId", "Trigger", "IsEnabled");
        schema.CreateIndexIfColumnsExist("wf_notification_task", "idx_wf_notification_task_due", "Status", "DueAt", "RetryCount");
        schema.CreateIndexIfColumnsExist("wf_notification_task", "idx_wf_notification_task_instance", "ProcessInstanceId", "WorkflowTaskId", "CreatedTime");
        schema.CreateIndexIfColumnsExist("wf_notification_log", "idx_wf_notification_log_instance", "ProcessInstanceId", "WorkflowTaskId", "CreatedTime");
        schema.EnsureColumn("wf_notification_log", "Provider", "TEXT NULL");
        schema.EnsureColumn("wf_notification_log", "TraceId", "TEXT NULL");
        schema.CreateIndexIfColumnsExist("workflow_categories", "idx_workflow_categories_scope", "TenantId", "AppCode", "CategoryCode", "IsDeleted");
        schema.CreateIndexIfColumnsExist("workflow_request_drafts", "idx_workflow_request_drafts_owner", "TenantId", "AppCode", "OwnerUserId", "Status", "LastSavedAt");
        schema.CreateIndexIfColumnsExist("workflow_request_drafts", "idx_workflow_request_drafts_form", "TenantId", "AppCode", "FormResourceCode");
        schema.CreateIndexIfColumnsExist("workflow_delegation_rules", "idx_workflow_delegation_rules_owner", "TenantId", "AppCode", "OwnerUserId", "StartAt", "EndAt", "IsEnabled");
        schema.CreateIndexIfColumnsExist("workflow_work_calendars", "idx_workflow_work_calendars_date", "TenantId", "AppCode", "CalendarDate", "IsDeleted");
        schema.CreateIndexIfColumnsExist("tbl_privilege_user", "idx_tbl_privilege_user_admin_lookup", "Username", "UserNo", "DelFlag");
        schema.CreateIndexIfColumnsExist("tbl_privilege_pvalue", "idx_tbl_privilege_pvalue_position", "Position");
        schema.CreateIndexIfColumnsExist("tbl_base_app", "idx_tbl_base_app_sn", "Sn", "DelFlag");
        schema.CreateIndexIfColumnsExist("tbl_base_category", "idx_tbl_base_category_code", "Code", "DelFlag");
        schema.CreateIndexIfColumnsExist("tbl_org_personal", "idx_tbl_org_personal_code", "Code", "DelFlag");
        schema.CreateIndexIfColumnsExist("tbl_org_role", "idx_tbl_org_role_code", "Code", "DelFlag");
        schema.CreateIndexIfColumnsExist("tbl_flow_model_info", "idx_tbl_flow_model_info_key_app", "ModelKey", "AppSn", "DelFlag");
        schema.CreateIndexIfColumnsExist("tbl_flow_extend_procinst", "idx_tbl_flow_extend_procinst_proc", "ProcessInstanceId");
        schema.CreateIndexIfColumnsExist("tbl_flow_extend_hisprocinst", "idx_tbl_flow_extend_hisprocinst_proc", "ProcessInstanceId");
        schema.CreateIndexIfColumnsExist("act_ru_actinst", "idx_flow_act_ru_proc_task", "PROC_INST_ID_", "TASK_ID_");
        schema.CreateIndexIfColumnsExist("act_hi_actinst", "idx_flow_act_hi_proc_task", "PROC_INST_ID_", "TASK_ID_");
    }
}
