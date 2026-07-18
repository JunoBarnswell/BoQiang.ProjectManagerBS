namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementTaskTemplateUpsertRequest(string TemplateCode, string TemplateName, string DefinitionJson, string? RecurrenceExpression = null, long VersionNo = 0);
public sealed record ProjectManagementTaskTemplateResponse(string Id, string? ProjectId, string TemplateCode, string TemplateName, string DefinitionJson, string? RecurrenceExpression, long VersionNo);
public sealed record ProjectManagementTaskTemplateApplyRequest(string ProjectId, string OccurrenceKey, DateTime OccurrenceDate);
