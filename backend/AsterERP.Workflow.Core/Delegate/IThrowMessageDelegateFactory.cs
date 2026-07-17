using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AsterERP.Workflow.Core.Delegate;

public interface IThrowMessageDelegateFactory
{
    IThrowMessageDelegate Create(string implementation);
}

