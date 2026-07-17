namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataCenterTemplateResponse(
    string ModuleKey,
    string TemplateCode,
    string TemplateName,
    string ObjectType,
    string Description,
    string ConfigJson);
