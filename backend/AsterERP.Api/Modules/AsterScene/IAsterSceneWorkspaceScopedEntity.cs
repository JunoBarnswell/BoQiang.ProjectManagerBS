namespace AsterERP.Api.Modules.AsterScene;

public interface IAsterSceneWorkspaceScopedEntity
{
    string TenantId { get; set; }

    string AppCode { get; set; }
}
