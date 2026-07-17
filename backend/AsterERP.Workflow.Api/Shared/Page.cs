namespace AsterERP.Workflow.Api.Shared;

public class Page<T>
{
    public IReadOnlyList<T> Content { get; set; } = Array.Empty<T>();
    public int TotalElements { get; set; }
    public int TotalPages { get; set; }
    public int Number { get; set; }
    public int Size { get; set; }
    public bool IsFirst { get; set; }
    public bool IsLast { get; set; }
    public bool HasContent { get; set; }
    public bool HasNext { get; set; }
    public bool HasPrevious { get; set; }
}
