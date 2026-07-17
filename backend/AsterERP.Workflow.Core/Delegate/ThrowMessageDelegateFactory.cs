using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AsterERP.Workflow.Core.Delegate;

public class ThrowMessageDelegateFactory : IThrowMessageDelegateFactory
{
    public IThrowMessageDelegate Create(string implementation)
    {
        if (string.IsNullOrWhiteSpace(implementation))
        {
            return new DefaultThrowMessageJavaDelegate();
        }

        var implementationType = Type.GetType(implementation);
        if (implementationType == null)
        {
            throw new ArgumentException($"Unable to resolve throw message delegate type '{implementation}'.", nameof(implementation));
        }

        if (!typeof(IThrowMessageDelegate).IsAssignableFrom(implementationType))
        {
            throw new ArgumentException($"Type '{implementation}' does not implement {nameof(IThrowMessageDelegate)}.", nameof(implementation));
        }

        if (implementationType.IsInterface || implementationType.IsAbstract)
        {
            throw new ArgumentException($"Type '{implementation}' must be a concrete delegate implementation.", nameof(implementation));
        }

        return new ThrowMessageJavaDelegate(implementationType);
    }
}

