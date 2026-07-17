using System.Xml;
using AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Parser;

public class BpmnParseHandlers
{
    private readonly Dictionary<string, IBpmnParseHandler> _handlersByType = new();

    public BpmnParseHandlers()
    {
        RegisterDefaultHandlers();
    }

    private void RegisterDefaultHandlers()
    {
        RegisterHandler(new StartEventParseHandler());
        RegisterHandler(new EndEventParseHandler());
        RegisterHandler(new UserTaskParseHandler());
        RegisterHandler(new ServiceTaskParseHandler());
        RegisterHandler(new ScriptTaskParseHandler());
        RegisterHandler(new ReceiveTaskParseHandler());
        RegisterHandler(new ManualTaskParseHandler());
        RegisterHandler(new SendTaskParseHandler());
        RegisterHandler(new BusinessRuleParseHandler());
        RegisterHandler(new AdhocSubProcessParseHandler());
        RegisterHandler(new ExclusiveGatewayParseHandler());
        RegisterHandler(new ParallelGatewayParseHandler());
        RegisterHandler(new InclusiveGatewayParseHandler());
        RegisterHandler(new BoundaryEventParseHandler());
        RegisterHandler(new SubProcessParseHandler());
        RegisterHandler(new CallActivityParseHandler());
        RegisterHandler(new SequenceFlowParseHandler());
        RegisterHandler(new IntermediateCatchEventParseHandler());
        RegisterHandler(new IntermediateThrowEventParseHandler());
        RegisterHandler(new EventBasedGatewayParseHandler());
        RegisterHandler(new EventSubProcessParseHandler());
        RegisterHandler(new TransactionParseHandler());
    }

    public void RegisterHandler(IBpmnParseHandler handler)
    {
        foreach (var type in handler.HandledTypes)
        {
            _handlersByType[type] = handler;
        }
    }

    public IBpmnParseHandler? GetHandler(string elementName)
    {
        return _handlersByType.GetValueOrDefault(elementName);
    }

    public IEnumerable<string> GetHandledTypes()
    {
        return _handlersByType.Keys;
    }
}
