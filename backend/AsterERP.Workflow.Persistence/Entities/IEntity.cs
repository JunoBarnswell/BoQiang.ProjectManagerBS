namespace AsterERP.Workflow.Persistence.Entities;

public interface IEntity
{
    string Id { get; set; }
    bool IsInserted { get; set; }
    bool IsUpdated { get; set; }
    bool IsDeleted { get; set; }
    object? GetPersistentState();
}
