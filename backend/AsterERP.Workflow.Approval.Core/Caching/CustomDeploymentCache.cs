namespace AsterERP.Workflow.Approval.Core.Caching;

public class CustomDeploymentCache<T> where T : class
{
    protected string? Id;
    protected T? Entry;

    public T? Get(string id)
    {
        if (id == this.Id)
        {
            return Entry;
        }
        return null;
    }

    public void Add(string id, T obj)
    {
        Id = id;
        Entry = obj;
    }

    public void Remove(string id)
    {
        if (id == Id)
        {
            Id = null;
            Entry = null;
        }
    }

    public void Clear()
    {
        Id = null;
        Entry = null;
    }

    public bool Contains(string id)
    {
        return id == Id;
    }

    public ICollection<T> GetAll()
    {
        if (Entry != null)
        {
            return new List<T> { Entry }.AsReadOnly();
        }
        return Array.Empty<T>();
    }

    public int Size()
    {
        return Entry != null ? 1 : 0;
    }
}
