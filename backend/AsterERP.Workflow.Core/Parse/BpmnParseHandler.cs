using System.Collections.Generic;

namespace AsterERP.Workflow.Core.Parse;

public interface IBpmnParseHandler
{
    ICollection<string> GetHandledTypes();
    void Parse(object bpmnParse, object baseElement);
}
