using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace AsterERP.Workflow.Core.Query;

public interface IQuery<T> where T : class
{
    IQuery<T> Where(Expression<Func<T, bool>> predicate);
    IQuery<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector, bool ascending = true);
    IQuery<T> Skip(int count);
    IQuery<T> Take(int count);
    global::System.Threading.Tasks.Task<List<T>> ToListAsync(CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<T?> FirstOrDefaultAsync(CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<int> CountAsync(CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<PageResult<T>> ToPageAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}
