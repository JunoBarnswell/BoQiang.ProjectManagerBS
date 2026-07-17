namespace AsterERP.Contracts.Ai.Flowise;

public sealed class FlowiseCustomMcpServerDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string ServerUrl { get; set; } = string.Empty;

    public string? IconSrc { get; set; }

    public string? Color { get; set; }

    public string AuthType { get; set; } = "none";

    public string AuthConfigJson { get; set; } = "{}";

    public string ToolsJson { get; set; } = "[]";

    public int ToolCount { get; set; }

    public string Status { get; set; } = "Enabled";

    public string? ErrorMessage { get; set; }

    public string? WorkspaceId { get; set; }

    public DateTime CreatedTime { get; set; }

    public DateTime? UpdatedTime { get; set; }
}

public sealed class FlowiseCustomMcpServerUpsertRequest
{
    public string Name { get; set; } = string.Empty;

    public string ServerUrl { get; set; } = string.Empty;

    public string? IconSrc { get; set; }

    public string? Color { get; set; }

    public string? AuthType { get; set; }

    public string? AuthConfigJson { get; set; }

    public string? Status { get; set; }

    public string? WorkspaceId { get; set; }
}

public sealed class FlowiseCustomMcpServerAuthorizeResultDto
{
    public string Id { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public int ToolCount { get; set; }

    public string ToolsJson { get; set; } = "[]";

    public string? ErrorMessage { get; set; }
}

public sealed class FlowiseCustomMcpServerToolDto
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string InputSchemaJson { get; set; } = "{}";

    public string AnnotationsJson { get; set; } = "{}";

    public string IconsJson { get; set; } = "[]";
}
