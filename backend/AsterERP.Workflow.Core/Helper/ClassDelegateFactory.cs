using System.Collections.Generic;
using AsterERP.Workflow.Core.Delegate;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Helper;

public interface IClassDelegateFactory
{
    ClassDelegate Create(string className, List<FieldDeclaration>? fieldDeclarations = null);

    ClassDelegate Create(
        string id,
        string className,
        List<FieldDeclaration>? fieldDeclarations,
        string? skipExpression,
        List<BpmnModelNs.MapExceptionEntry>? mapExceptions);
}
