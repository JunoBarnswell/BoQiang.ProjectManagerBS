using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

public abstract class AbstractEntity : IEntity, IHasRevision
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "ID_")]
    public string Id { get; set; } = null!;

    [SugarColumn(ColumnName = "REV_")]
    public int Revision { get; set; } = 1;

    [SugarColumn(IsIgnore = true)]
    public int RevisionNext => Revision + 1;

    [SugarColumn(IsIgnore = true)]
    public bool IsInserted { get; set; }

    [SugarColumn(IsIgnore = true)]
    public bool IsUpdated { get; set; }

    [SugarColumn(IsIgnore = true)]
    public bool IsDeleted { get; set; }

    public virtual object? GetPersistentState()
    {
        return null;
    }
}
