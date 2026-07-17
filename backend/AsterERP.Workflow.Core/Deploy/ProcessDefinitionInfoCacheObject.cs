using System;
using System.Text.Json.Nodes;

namespace AsterERP.Workflow.Core.Deploy;

public class ProcessDefinitionInfoCacheObject
{
    public string? Id { get; set; }
    public int Revision { get; set; }
    public JsonObject? InfoNode { get; set; }
    public DateTime CreatedTime { get; set; } = AbpTimeIdProvider.UtcNow;
}

