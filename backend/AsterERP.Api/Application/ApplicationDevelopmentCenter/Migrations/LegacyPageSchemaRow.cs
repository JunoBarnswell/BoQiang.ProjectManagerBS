namespace AsterERP.Api.Application.ApplicationDevelopmentCenter.Migrations;

/// <summary>
/// Raw projection used only while importing persisted historical page rows.
/// It deliberately has no ORM mapping so the imported table cannot become part of the runtime model.
/// </summary>
internal sealed record LegacyPageSchemaRow(
    string Id,
    string TenantId,
    string AppCode,
    string PageCode,
    string PageName,
    string PageType,
    string? ModelCode,
    string? PermissionCode,
    int VersionNo,
    string Status,
    string SchemaJson)
{
    public bool IsDeleted { get; init; }
}
