namespace AsterERP.Api.Modules.Ai;

public interface IAiWorkspaceScopedEntity
{
    string TenantId { get; set; }

    string AppCode { get; set; }
}
