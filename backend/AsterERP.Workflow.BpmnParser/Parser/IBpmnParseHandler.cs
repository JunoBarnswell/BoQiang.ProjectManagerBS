using System.Xml;
using AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Parser;

public interface IBpmnParseHandler
{
    string[] HandledTypes { get; }
    void Parse(XmlNode xmlNode, BpmnModel.BpmnModel model, Process activeProcess);
}
