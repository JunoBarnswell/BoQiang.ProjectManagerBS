using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace AsterERP.Workflow.Core.Query;

public class QueryImplementation<T> : IQuery<T> where T : class
{
    private readonly IEnumerable<T> _source;
    protected IEnumerable<T> _query;

    public QueryImplementation(IEnumerable<T> source)
    {
        _source = source;
        _query = source;
    }

    public IQuery<T> Where(Expression<Func<T, bool>> predicate)
    {
        _query = _query.Where(predicate.Compile());
        return this;
    }

    public IQuery<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector, bool ascending = true)
    {
        var compiled = keySelector.Compile();
        _query = ascending
            ? _query.OrderBy(compiled)
            : _query.OrderByDescending(compiled);
        return this;
    }

    public IQuery<T> Skip(int count)
    {
        _query = _query.Skip(count);
        return this;
    }

    public IQuery<T> Take(int count)
    {
        _query = _query.Take(count);
        return this;
    }

    public global::System.Threading.Tasks.Task<List<T>> ToListAsync(CancellationToken cancellationToken = default)
    {
        return global::System.Threading.Tasks.Task.FromResult(_query.ToList());
    }

    public global::System.Threading.Tasks.Task<T?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        return global::System.Threading.Tasks.Task.FromResult(_query.FirstOrDefault());
    }

    public global::System.Threading.Tasks.Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return global::System.Threading.Tasks.Task.FromResult(_query.Count());
    }

    public global::System.Threading.Tasks.Task<PageResult<T>> ToPageAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var totalCount = _query.Count();
        var items = _query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

        var result = new PageResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        return global::System.Threading.Tasks.Task.FromResult(result);
    }
}
