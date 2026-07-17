namespace AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Model;

public class HighLightedNodeVo
{
    public List<string> HighLightedFlows { get; set; }
    public List<string> ActiveActivityIds { get; set; }
    public List<string> HisActiveActivityIds { get; set; }
    public List<string> NullActiveActivityIds { get; set; }
    public string ModelXml { get; set; }
    public string ModelName { get; set; }

    public HighLightedNodeVo() { }

    public HighLightedNodeVo(List<string> highLightedFlows, List<string> activeActivityIds)
    {
        HighLightedFlows = highLightedFlows;
        ActiveActivityIds = activeActivityIds;
    }

    public HighLightedNodeVo(List<string> highLightedFlows, List<string> activeActivityIds, string modelXml, string modelName)
    {
        HighLightedFlows = highLightedFlows;
        ActiveActivityIds = activeActivityIds;
        ModelXml = modelXml;
        ModelName = modelName;
    }
}
