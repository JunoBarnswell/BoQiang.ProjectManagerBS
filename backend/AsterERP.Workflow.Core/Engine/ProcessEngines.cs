using System;
using System.Collections.Generic;
using AsterERP.Workflow.Common;

namespace AsterERP.Workflow.Core.Engine;

public static class ProcessEngines
{
    private static readonly Dictionary<string, IProcessEngine> _engines = new();
    private static readonly object _lock = new();

    public static IReadOnlyDictionary<string, IProcessEngine> ProcessEnginesByName
    {
        get
        {
            lock (_lock)
            {
                return new Dictionary<string, IProcessEngine>(_engines);
            }
        }
    }

    public static IProcessEngine GetDefaultProcessEngine()
    {
        lock (_lock)
        {
            if (_engines.Count == 0)
                throw new WorkflowEngineException("No process engines registered");

            using var enumerator = _engines.Values.GetEnumerator();
            enumerator.MoveNext();
            return enumerator.Current;
        }
    }

    public static IProcessEngine GetProcessEngine(string name)
    {
        lock (_lock)
        {
            if (_engines.TryGetValue(name, out var engine))
                return engine;

            throw new WorkflowEngineObjectNotFoundException($"Process engine with name '{name}' not found", typeof(IProcessEngine));
        }
    }

    public static void RegisterProcessEngine(IProcessEngine processEngine)
    {
        ArgumentNullException.ThrowIfNull(processEngine);

        lock (_lock)
        {
            _engines[processEngine.Name] = processEngine;
        }
    }

    public static void UnregisterProcessEngine(IProcessEngine processEngine)
    {
        ArgumentNullException.ThrowIfNull(processEngine);

        lock (_lock)
        {
            _engines.Remove(processEngine.Name);
        }
    }

    public static void Destroy()
    {
        lock (_lock)
        {
            foreach (var engine in _engines.Values)
            {
                try
                {
                    engine.Dispose();
                }
                catch
                {
                }
            }
            _engines.Clear();
        }
    }
}
