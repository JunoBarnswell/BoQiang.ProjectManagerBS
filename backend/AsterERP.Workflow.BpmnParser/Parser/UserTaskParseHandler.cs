using System.Xml;
using AsterERP.Workflow.BpmnModel;
using AsterERP.Workflow.BpmnParser.Converter;

namespace AsterERP.Workflow.BpmnParser.Parser;

public class UserTaskParseHandler : AbstractActivityBpmnParseHandler
{
    public override string[] HandledTypes => new[] { "userTask" };

    protected override FlowElement? ParseElement(XmlNode xmlNode, BpmnModel.BpmnModel model, Process activeProcess)
    {
        var userTask = new UserTask
        {
            Id = GetAttributeValue(xmlNode, "id"),
            Name = GetAttributeValue(xmlNode, "name"),
            Assignee = GetAttributeValue(xmlNode, "activiti", "assignee"),
            Owner = GetAttributeValue(xmlNode, "activiti", "owner"),
            CandidateUsers = ParseCommaSeparatedList(GetAttributeValue(xmlNode, "activiti", "candidateUsers")),
            CandidateGroups = ParseCommaSeparatedList(GetAttributeValue(xmlNode, "activiti", "candidateGroups")),
            FormKey = GetAttributeValue(xmlNode, "activiti", "formKey"),
            Priority = ParseInt(xmlNode, "activiti", "priority"),
            Category = GetAttributeValue(xmlNode, "activiti", "category"),
            SkipExpression = GetAttributeValue(xmlNode, "activiti", "skipExpression")
        };

        BpmnXMLUtil.ParseChildElements("userTask", userTask, xmlNode, model);

        return userTask;
    }
}
