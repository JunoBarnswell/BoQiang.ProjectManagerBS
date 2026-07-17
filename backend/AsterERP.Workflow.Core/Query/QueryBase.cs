using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Query;

public enum Direction
{
    Ascending,
    Descending
}

public enum QueryOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Like,
    LikeIgnoreCase,
    EqualsIgnoreCase,
    NotEqualsIgnoreCase
}

public class QueryVariableValue
{
    public string? Name { get; }
    public object? Value { get; }
    public QueryOperator Operator { get; }
    public bool LocalScope { get; }

    public QueryVariableValue(string? name, object? value, QueryOperator op, bool localScope)
    {
        Name = name;
        Value = value;
        Operator = op;
        LocalScope = localScope;
    }
}

public class Page
{
    public int FirstResult { get; }
    public int MaxResults { get; }

    public Page(int firstResult, int maxResults)
    {
        FirstResult = firstResult;
        MaxResults = maxResults;
    }
}

public abstract class AbstractQuery<T, TFilter> : IQuery<TFilter> where T : AbstractQuery<T, TFilter> where TFilter : class
{
    protected ICommandExecutor? CommandExecutor { get; set; }
    protected Direction? SortDirection { get; set; }
    protected string? OrderProperty { get; set; }
    protected int? FirstResultValue { get; set; }
    protected int? MaxResultsValue { get; set; }
    private readonly List<Func<TFilter, bool>> _predicates = new();
    private Func<IEnumerable<TFilter>, IEnumerable<TFilter>>? _orderByFunc;

    protected AbstractQuery() { }

    protected AbstractQuery(ICommandExecutor commandExecutor)
    {
        CommandExecutor = commandExecutor;
    }

    public IQuery<TFilter> Where(Expression<Func<TFilter, bool>> predicate)
    {
        _predicates.Add(predicate.Compile());
        return this;
    }

    public IQuery<TFilter> OrderBy<TKey>(Expression<Func<TFilter, TKey>> keySelector, bool ascending = true)
    {
        var compiled = keySelector.Compile();
        _orderByFunc = ascending
            ? q => q.OrderBy(compiled)
            : q => q.OrderByDescending(compiled);
        SortDirection = ascending ? Direction.Ascending : Direction.Descending;
        return this;
    }

    protected IEnumerable<TFilter> ApplyIQueryFilters(IEnumerable<TFilter> source)
    {
        var result = source;
        foreach (var predicate in _predicates)
            result = result.Where(predicate);
        if (_orderByFunc != null)
            result = _orderByFunc(result);
        return result;
    }

    public IQuery<TFilter> Skip(int count)
    {
        FirstResultValue = count;
        return this;
    }

    public IQuery<TFilter> Take(int count)
    {
        MaxResultsValue = count;
        return this;
    }

    public abstract Task<List<TFilter>> ToListAsync(CancellationToken cancellationToken = default);
    public abstract Task<TFilter?> FirstOrDefaultAsync(CancellationToken cancellationToken = default);
    public abstract Task<int> CountAsync(CancellationToken cancellationToken = default);

    public virtual async Task<PageResult<TFilter>> ToPageAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var totalCount = await CountAsync(cancellationToken);
        var items = await ToListAsync(cancellationToken);
        var pagedItems = items.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

        return new PageResult<TFilter>
        {
            Items = pagedItems,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    protected T OrderByProperty(string property)
    {
        OrderProperty = property;
        return (T)this;
    }

    protected T Ascending()
    {
        SortDirection = Direction.Ascending;
        return (T)this;
    }

    protected T Descending()
    {
        SortDirection = Direction.Descending;
        return (T)this;
    }

    public Task<List<TFilter>> ListAsync(CancellationToken cancellationToken = default)
    {
        return ToListAsync(cancellationToken);
    }

    public Task<PageResult<TFilter>> ListPageAsync(int firstResult, int maxResults, CancellationToken cancellationToken = default)
    {
        FirstResultValue = firstResult;
        MaxResultsValue = maxResults;
        var pageNumber = (firstResult / maxResults) + 1;
        return ToPageAsync(pageNumber, maxResults, cancellationToken);
    }

    public Task<TFilter?> SingleResultAsync(CancellationToken cancellationToken = default)
    {
        return FirstOrDefaultAsync(cancellationToken);
    }
}

public abstract class AbstractVariableQuery<T, TFilter> : AbstractQuery<T, TFilter> where T : AbstractVariableQuery<T, TFilter> where TFilter : class
{
    protected List<QueryVariableValue> QueryVariableValues { get; } = new();

    protected AbstractVariableQuery() { }

    protected AbstractVariableQuery(ICommandExecutor commandExecutor) : base(commandExecutor) { }

    public T VariableValueEquals(string name, object? value, bool localScope = true)
    {
        AddVariable(name, value, QueryOperator.Equals, localScope);
        return (T)this;
    }

    public T VariableValueEquals(object? value, bool localScope = true)
    {
        QueryVariableValues.Add(new QueryVariableValue(null, value, QueryOperator.Equals, localScope));
        return (T)this;
    }

    public T VariableValueNotEquals(string name, object? value, bool localScope = true)
    {
        AddVariable(name, value, QueryOperator.NotEquals, localScope);
        return (T)this;
    }

    public T VariableValueEqualsIgnoreCase(string name, string value, bool localScope = true)
    {
        AddVariable(name, value?.ToLowerInvariant(), QueryOperator.EqualsIgnoreCase, localScope);
        return (T)this;
    }

    public T VariableValueNotEqualsIgnoreCase(string name, string value, bool localScope = true)
    {
        AddVariable(name, value?.ToLowerInvariant(), QueryOperator.NotEqualsIgnoreCase, localScope);
        return (T)this;
    }

    public T VariableValueGreaterThan(string name, object? value, bool localScope = true)
    {
        AddVariable(name, value, QueryOperator.GreaterThan, localScope);
        return (T)this;
    }

    public T VariableValueGreaterThanOrEqual(string name, object? value, bool localScope = true)
    {
        AddVariable(name, value, QueryOperator.GreaterThanOrEqual, localScope);
        return (T)this;
    }

    public T VariableValueLessThan(string name, object? value, bool localScope = true)
    {
        AddVariable(name, value, QueryOperator.LessThan, localScope);
        return (T)this;
    }

    public T VariableValueLessThanOrEqual(string name, object? value, bool localScope = true)
    {
        AddVariable(name, value, QueryOperator.LessThanOrEqual, localScope);
        return (T)this;
    }

    public T VariableValueLike(string name, string? value, bool localScope = true)
    {
        AddVariable(name, value, QueryOperator.Like, localScope);
        return (T)this;
    }

    public T VariableValueLikeIgnoreCase(string name, string? value, bool localScope = true)
    {
        AddVariable(name, value?.ToLowerInvariant(), QueryOperator.LikeIgnoreCase, localScope);
        return (T)this;
    }

    protected void AddVariable(string? name, object? value, QueryOperator op, bool localScope)
    {
        QueryVariableValues.Add(new QueryVariableValue(name, value, op, localScope));
    }

    public bool HasLocalQueryVariableValue()
    {
        return QueryVariableValues.Any(v => v.LocalScope);
    }

    public bool HasNonLocalQueryVariableValue()
    {
        return QueryVariableValues.Any(v => !v.LocalScope);
    }
}

public abstract class AbstractNativeQuery<T, TFilter> where T : AbstractNativeQuery<T, TFilter> where TFilter : class
{
    protected ICommandExecutor? CommandExecutor { get; set; }
    protected string? SqlStatement { get; set; }
    protected Dictionary<string, object> Parameters { get; } = new();
    protected int? FirstResultValue { get; set; }
    protected int? MaxResultsValue { get; set; }

    protected AbstractNativeQuery() { }

    protected AbstractNativeQuery(ICommandExecutor commandExecutor)
    {
        CommandExecutor = commandExecutor;
    }

    public T Sql(string sql)
    {
        SqlStatement = sql;
        return (T)this;
    }

    public T Parameter(string name, object value)
    {
        Parameters[name] = value;
        return (T)this;
    }

    public abstract Task<List<TFilter>> ToListAsync(CancellationToken cancellationToken = default);
    public abstract Task<long> CountAsync(CancellationToken cancellationToken = default);
    public abstract Task<TFilter?> SingleResultAsync(CancellationToken cancellationToken = default);

    public Task<PageResult<TFilter>> ListPageAsync(int firstResult, int maxResults, CancellationToken cancellationToken = default)
    {
        FirstResultValue = firstResult;
        MaxResultsValue = maxResults;
        return ToPageAsync((firstResult / maxResults) + 1, maxResults, cancellationToken);
    }

    public async Task<PageResult<TFilter>> ToPageAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var totalCount = (int)await CountAsync(cancellationToken);
        var items = await ToListAsync(cancellationToken);
        var pagedItems = items.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

        return new PageResult<TFilter>
        {
            Items = pagedItems,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
