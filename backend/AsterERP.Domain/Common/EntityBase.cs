using SqlSugar;

namespace AsterERP.Domain.Common;

public abstract class EntityBase
{
    [SugarColumn(IsPrimaryKey = true)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [SugarColumn(IsNullable = true)]
    public string? CreatedBy { get; set; }

    public DateTime CreatedTime { get; set; } = DateTime.UtcNow;

    [SugarColumn(IsNullable = true)]
    public string? UpdatedBy { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? UpdatedTime { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? DeletedBy { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? DeletedTime { get; set; }

    public bool IsDeleted { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Remark { get; set; }
}
