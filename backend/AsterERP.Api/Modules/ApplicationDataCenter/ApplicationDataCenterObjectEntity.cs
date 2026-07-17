using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDataCenter;

public abstract class ApplicationDataCenterObjectEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string ModuleKey { get; set; } = string.Empty;

    public string ObjectCode { get; set; } = string.Empty;

    public string ObjectName { get; set; } = string.Empty;

    public string ObjectType { get; set; } = string.Empty;

    public string Status { get; set; } = "Draft";

    public int VersionNo { get; set; } = 1;

    [SugarColumn(IsNullable = true)]
    public string? OwnerUserId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? OwnerName { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Environment { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Endpoint { get; set; }

    [SugarColumn(Length = 262144)]
    public string ConfigJson { get; set; } = "{}";

    [SugarColumn(Length = 262144, IsNullable = true)]
    public string? SecretConfigCipherText { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? SecretRef { get; set; }

    [SugarColumn(Length = 262144, IsNullable = true)]
    public string? PublicConfigJson { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? LastValidationStatus { get; set; }

    [SugarColumn(Length = 2000, IsNullable = true)]
    public string? LastValidationMessage { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? LastValidatedAt { get; set; }

    public int ReferenceCount { get; set; }
}
