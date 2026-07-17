namespace AsterERP.Contracts.Ai.Flowise;

public sealed class FlowiseDatasetDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Status { get; set; } = "Enabled";

    public int RowCount { get; set; }

    public DateTime CreatedTime { get; set; }
}

public sealed class FlowiseDatasetRowDto
{
    public string Id { get; set; } = string.Empty;

    public string DatasetId { get; set; } = string.Empty;

    public string Input { get; set; } = string.Empty;

    public string? ExpectedOutput { get; set; }

    public string? ActualOutput { get; set; }

    public string MetadataJson { get; set; } = "{}";
}

public sealed class FlowiseEvaluatorDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;

    public string PromptTemplate { get; set; } = string.Empty;

    public string Status { get; set; } = "Enabled";

    public DateTime CreatedTime { get; set; }
}

public sealed class FlowiseEvaluationDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string DatasetId { get; set; } = string.Empty;

    public string EvaluatorId { get; set; } = string.Empty;

    public string TargetFlowId { get; set; } = string.Empty;

    public string Status { get; set; } = "Draft";

    public int VersionNo { get; set; }

    public DateTime CreatedTime { get; set; }
}

public sealed class FlowiseEvaluationResultDto
{
    public string Id { get; set; } = string.Empty;

    public int VersionNo { get; set; }

    public string Status { get; set; } = "Pending";

    public decimal PassRate { get; set; }

    public int AverageLatencyMs { get; set; }

    public int TotalTokens { get; set; }

    public string MetricsJson { get; set; } = "{}";

    public string ResultRowsJson { get; set; } = "[]";
}
