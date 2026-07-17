using System.Collections.Generic;
using AsterERP.Workflow.Core.Delegate;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Helper;

public class DefaultClassDelegateFactory : IClassDelegateFactory
{
    private readonly IServiceProvider? _serviceProvider;

    public DefaultClassDelegateFactory(IServiceProvider? serviceProvider = null)
    {
        _serviceProvider = serviceProvider;
    }

    public ClassDelegate Create(string className, List<FieldDeclaration>? fieldDeclarations = null)
    {
        return new ClassDelegate(className, fieldDeclarations, _serviceProvider);
    }

    public ClassDelegate Create(
        string id,
        string className,
        List<FieldDeclaration>? fieldDeclarations,
        string? skipExpression,
        List<BpmnModelNs.MapExceptionEntry>? mapExceptions)
    {
        return new ClassDelegate(id, className, fieldDeclarations, skipExpression, mapExceptions, _serviceProvider);
    }
}
