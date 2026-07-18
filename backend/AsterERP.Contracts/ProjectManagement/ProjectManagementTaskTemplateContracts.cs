namespace AsterERP.Contracts.ProjectManagement;

/// <summary>
/// 模板的可见范围。项目模板只在所属项目可见；全局模板在当前租户、应用内可见。
/// </summary>
public static class ProjectManagementTaskTemplateScopes
{
    public const string Project = "Project";
    public const string Global = "Global";
}

/// <summary>
/// 旧版 JSON 定义写入协议。新客户端应优先使用“从任务创建模板”接口，
/// 由服务端从真实任务树生成不可变快照，避免客户端伪造任务关系。
/// </summary>
public sealed record ProjectManagementTaskTemplateUpsertRequest(
    string TemplateCode,
    string TemplateName,
    string DefinitionJson,
    string? RecurrenceExpression = null,
    long VersionNo = 0,
    bool IsGlobal = false);

public sealed record ProjectManagementTaskTemplateResponse(
    string Id,
    string? ProjectId,
    string Scope,
    string TemplateCode,
    string TemplateName,
    string DefinitionJson,
    string? RecurrenceExpression,
    long VersionNo);

/// <summary>
/// 从一个任务或主题父任务生成模板。若源任务有子任务，服务端固定捕获其完整子树。
/// </summary>
public sealed record ProjectManagementTaskTemplateCreateFromTaskRequest(
    string SourceTaskId,
    string TemplateCode,
    string TemplateName,
    bool IsGlobal = false,
    string? RecurrenceExpression = null);

/// <summary>
/// 模板节点中仅保留可复用的计划字段。实际日期、进度、实际工时、评论、附件和提醒从不进入模板。
/// </summary>
public sealed record ProjectManagementTaskTemplateNodeDefinition(
    string NodeKey,
    string TaskCode,
    string Title,
    string? Description,
    string Status,
    string Priority,
    string? DefaultRoleCode,
    IReadOnlyList<ProjectManagementTaskTemplateLabelDefinition> Labels,
    int? EstimateMinutes,
    int? DefaultDurationDays,
    string? MilestoneKey,
    string? ParentNodeKey,
    decimal Weight,
    int SortOrder);

/// <summary>
/// 标签按显示语义快照，而不是复制源项目标签 Id；实例化时由目标项目可见标签解析。
/// </summary>
public sealed record ProjectManagementTaskTemplateLabelDefinition(
    string LabelKey,
    string LabelName,
    string Color);

public sealed record ProjectManagementTaskTemplateMilestoneDefinition(
    string MilestoneKey,
    string MilestoneName);

public sealed record ProjectManagementTaskTemplateDependencyDefinition(
    string PredecessorNodeKey,
    string SuccessorNodeKey,
    string DependencyType,
    int LagMinutes);

/// <summary>
/// 版本化模板定义。Version 固定为 1；模板编辑只替换自身快照，已实例化的任务不保留回链也不会被回写。
/// </summary>
public sealed record ProjectManagementTaskTemplateDefinition(
    int SchemaVersion,
    IReadOnlyList<ProjectManagementTaskTemplateNodeDefinition> Nodes,
    IReadOnlyList<ProjectManagementTaskTemplateMilestoneDefinition> Milestones,
    IReadOnlyList<ProjectManagementTaskTemplateDependencyDefinition> Dependencies);

/// <summary>
/// 目标项目的里程碑与角色指派映射。角色映射必须由调用方显式选择成员；服务端不会从同角色成员中随机选择。
/// </summary>
public sealed record ProjectManagementTaskTemplateInstantiateRequest(
    string ProjectId,
    DateTime? StartDate = null,
    IReadOnlyDictionary<string, string>? MilestoneMappings = null,
    IReadOnlyDictionary<string, string>? RoleAssigneeMappings = null,
    string? ClientRequestId = null);

public sealed record ProjectManagementTaskTemplateInstantiationWarning(
    string Code,
    string NodeKey,
    string Message);

public sealed record ProjectManagementTaskTemplateInstantiationResponse(
    string TemplateId,
    long TemplateVersionNo,
    IReadOnlyList<ProjectManagementTaskResponse> Tasks,
    IReadOnlyList<ProjectManagementTaskTemplateInstantiationWarning> Warnings);

/// <summary>
/// 兼容既有重复任务调用；新功能使用 <see cref="ProjectManagementTaskTemplateInstantiateRequest"/>。
/// </summary>
public sealed record ProjectManagementTaskTemplateApplyRequest(string ProjectId, string OccurrenceKey, DateTime OccurrenceDate);
