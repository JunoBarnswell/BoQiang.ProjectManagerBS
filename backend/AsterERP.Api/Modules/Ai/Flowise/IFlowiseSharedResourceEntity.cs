using AsterERP.Api.Modules.Ai;

namespace AsterERP.Api.Modules.Ai.Flowise;

/// <summary>
/// Marks a Flowise resource whose visibility may be granted to another Flowise workspace.
/// The shared-workspace relation is resolved by the database-side permission filter.
/// </summary>
public interface IFlowiseSharedResourceEntity : IAiOwnedEntity
{
    string Id { get; set; }
}
