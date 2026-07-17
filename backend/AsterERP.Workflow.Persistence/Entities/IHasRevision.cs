namespace AsterERP.Workflow.Persistence.Entities;

public interface IHasRevision
{
    int Revision { get; set; }
    int RevisionNext { get; }
}
