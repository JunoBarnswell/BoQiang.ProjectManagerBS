using System.Collections.Generic;

namespace AsterERP.Workflow.BpmnModel.Image;

public class DiagramLayout
{
    private const double HorizontalSpacing = 120;
    private const double VerticalSpacing = 100;
    private const double TaskWidth = 100;
    private const double TaskHeight = 80;
    private const double EventDiameter = 36;
    private const double GatewaySize = 50;

    public static Dictionary<string, GraphicInfo> CalculateLayout(Process process)
    {
        var layout = new Dictionary<string, GraphicInfo>();
        var visited = new HashSet<string>();
        var elementMap = BuildElementMap(process);

        var startEvent = process.FlowElements.Find(fe => fe is StartEvent) as StartEvent;
        if (startEvent == null)
        {
            LayoutAllElementsLinear(process, layout);
            return layout;
        }

        var branchYOffsets = new Dictionary<string, double>();
        var maxDepth = new Dictionary<string, int>();

        CalculateDepths(startEvent.Id!, elementMap, maxDepth, new HashSet<string>());

        var nextYOffset = new Dictionary<int, double>();
        LayoutElement(startEvent.Id!, elementMap, layout, visited, 0, 0, nextYOffset, branchYOffsets);

        foreach (var element in process.FlowElements)
        {
            if (element.Id != null && !visited.Contains(element.Id))
            {
                var y = nextYOffset.GetValueOrDefault(0, 0) + VerticalSpacing;
                nextYOffset[0] = y;
                LayoutElement(element.Id, elementMap, layout, visited, 0, y, nextYOffset, branchYOffsets);
            }
        }

        return layout;
    }

    private static Dictionary<string, ElementInfo> BuildElementMap(Process process)
    {
        var map = new Dictionary<string, ElementInfo>();

        foreach (var element in process.FlowElements)
        {
            if (element is SequenceFlow)
                continue;

            if (element.Id == null)
                continue;

            var outgoing = new List<SequenceFlow>();
            var incoming = new List<SequenceFlow>();

            foreach (var flow in process.FlowElements)
            {
                if (flow is SequenceFlow sf)
                {
                    if (sf.SourceRef == element.Id)
                        outgoing.Add(sf);
                    if (sf.TargetRef == element.Id)
                        incoming.Add(sf);
                }
            }

            map[element.Id] = new ElementInfo
            {
                Element = element,
                OutgoingFlows = outgoing,
                IncomingFlows = incoming
            };
        }

        return map;
    }

    private static void CalculateDepths(string elementId, Dictionary<string, ElementInfo> elementMap,
        Dictionary<string, int> maxDepth, HashSet<string> visiting)
    {
        if (maxDepth.ContainsKey(elementId))
            return;

        if (visiting.Contains(elementId))
            return;

        visiting.Add(elementId);

        if (!elementMap.TryGetValue(elementId, out var info))
        {
            maxDepth[elementId] = 0;
            visiting.Remove(elementId);
            return;
        }

        var depth = 0;
        foreach (var flow in info.OutgoingFlows)
        {
            if (flow.TargetRef != null)
            {
                CalculateDepths(flow.TargetRef, elementMap, maxDepth, visiting);
                var targetDepth = maxDepth.GetValueOrDefault(flow.TargetRef, 0);
                depth = Math.Max(depth, targetDepth + 1);
            }
        }

        maxDepth[elementId] = depth;
        visiting.Remove(elementId);
    }

    private static void LayoutElement(string elementId, Dictionary<string, ElementInfo> elementMap,
        Dictionary<string, GraphicInfo> layout, HashSet<string> visited,
        int depth, double y, Dictionary<int, double> nextYOffset,
        Dictionary<string, double> branchYOffsets)
    {
        if (visited.Contains(elementId))
            return;

        if (!elementMap.TryGetValue(elementId, out var info))
            return;

        visited.Add(elementId);

        var x = depth * HorizontalSpacing;
        var (width, height) = GetElementSize(info.Element);

        if (info.Element is Gateway)
        {
            x -= (GatewaySize - TaskWidth) / 2.0;
        }
        else if (info.Element is Event)
        {
            x += (TaskWidth - EventDiameter) / 2.0;
        }

        layout[elementId] = new GraphicInfo
        {
            X = x,
            Y = y,
            Width = width,
            Height = height
        };

        if (info.OutgoingFlows.Count > 1)
        {
            var branchCount = info.OutgoingFlows.Count;
            var totalHeight = (branchCount - 1) * VerticalSpacing;
            var startY = y - totalHeight / 2.0;

            for (var i = 0; i < info.OutgoingFlows.Count; i++)
            {
                var flow = info.OutgoingFlows[i];
                if (flow.TargetRef != null)
                {
                    var branchY = startY + i * VerticalSpacing;
                    LayoutElement(flow.TargetRef, elementMap, layout, visited,
                        depth + 1, branchY, nextYOffset, branchYOffsets);
                }
            }
        }
        else if (info.OutgoingFlows.Count == 1)
        {
            var flow = info.OutgoingFlows[0];
            if (flow.TargetRef != null)
            {
                LayoutElement(flow.TargetRef, elementMap, layout, visited,
                    depth + 1, y, nextYOffset, branchYOffsets);
            }
        }
    }

    private static void LayoutAllElementsLinear(Process process, Dictionary<string, GraphicInfo> layout)
    {
        var x = 0.0;
        var y = 0.0;

        foreach (var element in process.FlowElements)
        {
            if (element is SequenceFlow)
                continue;

            var (width, height) = GetElementSize(element);
            if (element.Id != null)
            {
                layout[element.Id] = new GraphicInfo
                {
                    X = x,
                    Y = y,
                    Width = width,
                    Height = height
                };
            }
            x += HorizontalSpacing;
        }
    }

    internal static (double Width, double Height) GetElementSize(FlowElement element)
    {
        return element switch
        {
            StartEvent => (EventDiameter, EventDiameter),
            EndEvent => (EventDiameter, EventDiameter),
            IntermediateCatchEvent => (EventDiameter, EventDiameter),
            IntermediateThrowEvent => (EventDiameter, EventDiameter),
            Gateway => (GatewaySize, GatewaySize),
            SubProcess => (200, 120),
            _ => (TaskWidth, TaskHeight)
        };
    }

    private class ElementInfo
    {
        public FlowElement Element { get; set; } = null!;
        public List<SequenceFlow> OutgoingFlows { get; set; } = new();
        public List<SequenceFlow> IncomingFlows { get; set; } = new();
    }
}
