using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AsterERP.Workflow.Core.Delegate;

public class FieldDeclaration
{
    public string Name { get; }
    public string Type { get; }
    public object? Value { get; }

    public FieldDeclaration(string name, string type, object? value)
    {
        Name = name;
        Type = type;
        Value = value;
    }
}

