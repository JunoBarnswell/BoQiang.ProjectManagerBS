using System.Text.Json.Serialization;

namespace AsterERP.Api.Infrastructure.Publishing;

public sealed class ApplicationPublishModuleFileMap
{
    public string SchemaVersion { get; set; } = "1.0";

    public List<string> Skeleton { get; set; } = [];

    public List<ApplicationPublishModuleFileMapEntry> Modules { get; set; } = [];
}

public sealed class ApplicationPublishModuleFileMapEntry
{
    public string ModuleKey { get; set; } = string.Empty;

    public List<string> PermissionPrefixes { get; set; } = [];

    public List<string> ProviderKeys { get; set; } = [];

    public List<string> DependsOn { get; set; } = [];

    public List<string> FileGlobs { get; set; } = [];

    [JsonIgnore]
    public bool IsEmpty => string.IsNullOrWhiteSpace(ModuleKey);
}
