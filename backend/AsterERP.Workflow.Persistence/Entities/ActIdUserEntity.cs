using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_ID_USER")]
public class ActIdUserEntity
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "ID_")]
    public string Id { get; set; } = null!;

    [SugarColumn(ColumnName = "REV_")]
    public int Revision { get; set; }

    [SugarColumn(ColumnName = "FIRST_", IsNullable = true)]
    public string? FirstName { get; set; }

    [SugarColumn(ColumnName = "LAST_", IsNullable = true)]
    public string? LastName { get; set; }

    [SugarColumn(ColumnName = "DISPLAY_NAME_", IsNullable = true)]
    public string? DisplayName { get; set; }

    [SugarColumn(ColumnName = "EMAIL_", IsNullable = true)]
    public string? Email { get; set; }

    [SugarColumn(ColumnName = "PWD_", IsNullable = true)]
    public string? Password { get; set; }

    [SugarColumn(ColumnName = "PICTURE_ID_", IsNullable = true)]
    public string? PictureId { get; set; }

    [SugarColumn(ColumnName = "TENANT_ID_", IsNullable = true)]
    public string? TenantId { get; set; }
}
