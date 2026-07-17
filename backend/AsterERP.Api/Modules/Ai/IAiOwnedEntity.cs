namespace AsterERP.Api.Modules.Ai;

public interface IAiOwnedEntity : IAiWorkspaceScopedEntity
{
    string OwnerUserId { get; set; }
}
