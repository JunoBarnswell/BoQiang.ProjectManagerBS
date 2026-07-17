using System;
using System.Linq;
using AsterERP.Workflow.Common;

namespace AsterERP.Workflow.Core.Helper;

public static class ClassDelegateUtil
{
    public static Type ResolveType(string className)
    {
        if (string.IsNullOrWhiteSpace(className))
            throw new WorkflowEngineArgumentException("className is null or empty");

        var type = Type.GetType(className, throwOnError: false);
        if (type != null)
            return type;

        type = AppDomain.CurrentDomain
            .GetAssemblies()
            .Select(assembly => assembly.GetType(className, throwOnError: false))
            .FirstOrDefault(candidate => candidate != null);

        return type ?? throw new WorkflowEngineObjectNotFoundException($"Class '{className}' was not found");
    }

    public static object Instantiate(string className, IServiceProvider? serviceProvider = null)
    {
        var type = ResolveType(className);
        var service = serviceProvider?.GetService(type);
        if (service != null)
            return service;

        return Activator.CreateInstance(type)
            ?? throw new WorkflowEngineException($"Class '{className}' could not be instantiated");
    }

    public static T Instantiate<T>(string className, IServiceProvider? serviceProvider = null) where T : class
    {
        var instance = Instantiate(className, serviceProvider);
        if (instance is T typedInstance)
            return typedInstance;

        throw new WorkflowEngineArgumentException(
            $"Class '{className}' does not implement {typeof(T).FullName}");
    }
}
