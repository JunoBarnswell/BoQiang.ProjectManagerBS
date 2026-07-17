using System;
using System.Collections.Generic;
using System.Linq;

namespace AsterERP.Workflow.Core.Deploy;

public class DeploymentEntity
{
    public string Id { get; set; } = null!;
    public string? Name { get; set; }
    public string? Category { get; set; }
    public string? Key { get; set; }
    public string? TenantId { get; set; }
    public DateTime DeployTime { get; set; } = AbpTimeIdProvider.UtcNow;
    public bool IsNew { get; set; } = true;
    public Dictionary<string, object> DeploymentSettings { get; set; } = new();

    private readonly Dictionary<Type, List<object>> _deployedArtifacts = new();

    public void AddDeployedArtifact<T>(T artifact) where T : class
    {
        var type = typeof(T);
        if (!_deployedArtifacts.ContainsKey(type))
        {
            _deployedArtifacts[type] = new List<object>();
        }

        _deployedArtifacts[type].Add(artifact);
    }

    public List<T>? GetDeployedArtifacts<T>() where T : class
    {
        var type = typeof(T);
        if (_deployedArtifacts.TryGetValue(type, out var artifacts))
        {
            return artifacts.Cast<T>().ToList();
        }

        return null;
    }
}

