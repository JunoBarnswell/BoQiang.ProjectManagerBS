using System.Collections.Generic;

namespace AsterERP.Workflow.Core.Deploy;

public interface IDeploymentCache<T>
{
    T? Get(string id);
    bool Contains(string id);
    void Add(string id, T obj);
    void Remove(string id);
    void Clear();
    IEnumerable<T> GetAll();
}
