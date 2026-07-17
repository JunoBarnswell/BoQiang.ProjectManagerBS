using System.Text;

namespace AsterERP.Workflow.BpmnModel.Image;

public class SvgProcessDiagramGenerator : ISvgProcessDiagramGenerator
{
    private const double Padding = 20;
    private const double ArrowSize = 8;

    public string GenerateSvg(BpmnModel bpmnModel, string? processId = null)
    {
        var process = processId != null
            ? bpmnModel.Processes.Find(p => p.Id == processId)
            : bpmnModel.Processes.FirstOrDefault();

        if (process == null)
            return GenerateEmptySvg();

        var graphicInfo = DiagramLayout.CalculateLayout(process);
        return GenerateSvg(bpmnModel, graphicInfo);
    }

    public string GenerateSvg(BpmnModel bpmnModel, Dictionary<string, GraphicInfo> graphicInfo)
    {
        var process = bpmnModel.Processes.FirstOrDefault();
        if (process == null)
            return GenerateEmptySvg();

        var bounds = CalculateBounds(graphicInfo);
        var svg = new StringBuilder();
        var totalWidth = bounds.MaxX - bounds.MinX + 2 * Padding;
        var totalHeight = bounds.MaxY - bounds.MinY + 2 * Padding;

        svg.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{totalWidth}\" height=\"{totalHeight}\" viewBox=\"0 0 {totalWidth} {totalHeight}\">");

        svg.AppendLine("  <defs>");
        svg.AppendLine($"    <marker id=\"arrowhead\" markerWidth=\"{ArrowSize}\" markerHeight=\"{ArrowSize * 0.6}\" refX=\"{ArrowSize}\" refY=\"{ArrowSize * 0.3}\" orient=\"auto\">");
        svg.AppendLine($"      <polygon points=\"0 0, {ArrowSize} {ArrowSize * 0.3}, 0 {ArrowSize * 0.6}\" fill=\"{SvgStyle.SequenceFlowStroke}\"/>");
        svg.AppendLine("    </marker>");
        svg.AppendLine("  </defs>");

        svg.AppendLine($"  <rect width=\"100%\" height=\"100%\" fill=\"white\"/>");

        svg.AppendLine("  <g>");

        foreach (var flow in process.FlowElements.OfType<SequenceFlow>())
            DrawSequenceFlow(svg, flow, graphicInfo, bounds);

        foreach (var element in process.FlowElements)
        {
            if (element is SequenceFlow)
                continue;
            DrawFlowElement(svg, element, graphicInfo, bounds);
        }

        svg.AppendLine("  </g>");
        svg.AppendLine("</svg>");
        return svg.ToString();
    }

    public byte[] GenerateDiagram(BpmnModel bpmnModel, string? processId = null)
    {
        return Encoding.UTF8.GetBytes(GenerateSvg(bpmnModel, processId));
    }

    private void DrawFlowElement(StringBuilder svg, FlowElement element,
        Dictionary<string, GraphicInfo> graphicInfo, BoundsInfo bounds)
    {
        if (!graphicInfo.TryGetValue(element.Id ?? "", out var gi))
            return;

        var x = gi.X - bounds.MinX + Padding;
        var y = gi.Y - bounds.MinY + Padding;

        switch (element)
        {
            case StartEvent startEvent:
                DrawStartEvent(svg, startEvent, x, y, gi);
                break;
            case EndEvent endEvent:
                DrawEndEvent(svg, endEvent, x, y, gi);
                break;
            case UserTask userTask:
                DrawUserTask(svg, userTask, x, y, gi);
                break;
            case ServiceTask serviceTask:
                DrawServiceTask(svg, serviceTask, x, y, gi);
                break;
            case ScriptTask scriptTask:
                DrawScriptTask(svg, scriptTask, x, y, gi);
                break;
            case ExclusiveGateway gateway:
                DrawExclusiveGateway(svg, gateway, x, y, gi);
                break;
            case ParallelGateway gateway:
                DrawParallelGateway(svg, gateway, x, y, gi);
                break;
            case InclusiveGateway gateway:
                DrawInclusiveGateway(svg, gateway, x, y, gi);
                break;
            case SubProcess subProcess:
                DrawSubProcess(svg, subProcess, x, y, gi);
                break;
            case IntermediateCatchEvent catchEvent:
                DrawIntermediateCatchEvent(svg, catchEvent, x, y, gi);
                break;
            case IntermediateThrowEvent throwEvent:
                DrawIntermediateThrowEvent(svg, throwEvent, x, y, gi);
                break;
            case BoundaryEvent boundaryEvent:
                DrawBoundaryEvent(svg, boundaryEvent, x, y, gi);
                break;
            case CallActivity callActivity:
                DrawCallActivity(svg, callActivity, x, y, gi);
                break;
            case ReceiveTask receiveTask:
                DrawReceiveTask(svg, receiveTask, x, y, gi);
                break;
            case SendTask sendTask:
                DrawSendTask(svg, sendTask, x, y, gi);
                break;
            case ManualTask manualTask:
                DrawManualTask(svg, manualTask, x, y, gi);
                break;
            case BusinessRuleTask businessRuleTask:
                DrawBusinessRuleTask(svg, businessRuleTask, x, y, gi);
                break;
        }
    }

    private void DrawStartEvent(StringBuilder svg, StartEvent startEvent, double x, double y, GraphicInfo gi)
    {
        var cx = x + gi.Width / 2;
        var cy = y + gi.Height / 2;
        var r = gi.Width / 2;

        svg.AppendLine($"  <circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r}\" fill=\"{SvgStyle.StartEventFill}\" stroke=\"{SvgStyle.StartEventStroke}\" stroke-width=\"{SvgStyle.StrokeWidth}\"/>");

        if (!string.IsNullOrEmpty(startEvent.Name))
            DrawLabel(svg, startEvent.Name, cx, y + gi.Height + 8);
    }

    private void DrawEndEvent(StringBuilder svg, EndEvent endEvent, double x, double y, GraphicInfo gi)
    {
        var cx = x + gi.Width / 2;
        var cy = y + gi.Height / 2;
        var r = gi.Width / 2;

        svg.AppendLine($"  <circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r}\" fill=\"{SvgStyle.EndEventFill}\" stroke=\"{SvgStyle.EndEventStroke}\" stroke-width=\"{SvgStyle.StrokeWidth * 1.5}\"/>");

        var innerR = r * 0.7;
        svg.AppendLine($"  <circle cx=\"{cx}\" cy=\"{cy}\" r=\"{innerR}\" fill=\"none\" stroke=\"{SvgStyle.EndEventStroke}\" stroke-width=\"{SvgStyle.StrokeWidth}\"/>");

        if (!string.IsNullOrEmpty(endEvent.Name))
            DrawLabel(svg, endEvent.Name, cx, y + gi.Height + 8);
    }

    private void DrawIntermediateCatchEvent(StringBuilder svg, IntermediateCatchEvent catchEvent, double x, double y, GraphicInfo gi)
    {
        var cx = x + gi.Width / 2;
        var cy = y + gi.Height / 2;
        var r = gi.Width / 2;

        svg.AppendLine($"  <circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r}\" fill=\"white\" stroke=\"#FFC107\" stroke-width=\"{SvgStyle.StrokeWidth}\"/>");
        svg.AppendLine($"  <circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r * 0.85}\" fill=\"none\" stroke=\"#FFC107\" stroke-width=\"{SvgStyle.StrokeWidth}\"/>");

        if (!string.IsNullOrEmpty(catchEvent.Name))
            DrawLabel(svg, catchEvent.Name, cx, y + gi.Height + 8);
    }

    private void DrawIntermediateThrowEvent(StringBuilder svg, IntermediateThrowEvent throwEvent, double x, double y, GraphicInfo gi)
    {
        var cx = x + gi.Width / 2;
        var cy = y + gi.Height / 2;
        var r = gi.Width / 2;

        svg.AppendLine($"  <circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r}\" fill=\"white\" stroke=\"#333333\" stroke-width=\"{SvgStyle.StrokeWidth}\"/>");
        svg.AppendLine($"  <circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r * 0.85}\" fill=\"none\" stroke=\"#333333\" stroke-width=\"{SvgStyle.StrokeWidth}\"/>");

        if (!string.IsNullOrEmpty(throwEvent.Name))
            DrawLabel(svg, throwEvent.Name, cx, y + gi.Height + 8);
    }

    private void DrawBoundaryEvent(StringBuilder svg, BoundaryEvent boundaryEvent, double x, double y, GraphicInfo gi)
    {
        var cx = x + gi.Width / 2;
        var cy = y + gi.Height / 2;
        var r = gi.Width / 2;

        svg.AppendLine($"  <circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r}\" fill=\"white\" stroke=\"#FFC107\" stroke-width=\"{SvgStyle.StrokeWidth}\"/>");
        svg.AppendLine($"  <circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r * 0.85}\" fill=\"none\" stroke=\"#FFC107\" stroke-width=\"{SvgStyle.StrokeWidth}\"/>");

        if (!string.IsNullOrEmpty(boundaryEvent.Name))
            DrawLabel(svg, boundaryEvent.Name, cx, y + gi.Height + 8);
    }

    private void DrawUserTask(StringBuilder svg, UserTask userTask, double x, double y, GraphicInfo gi)
    {
        DrawRoundedRect(svg, x, y, gi.Width, gi.Height, 6, SvgStyle.UserTaskFill, SvgStyle.UserTaskStroke);
        DrawUserIcon(svg, x + 8, y + 8);
        DrawTaskLabel(svg, userTask.Name, x, y, gi.Width, gi.Height);
    }

    private void DrawServiceTask(StringBuilder svg, ServiceTask serviceTask, double x, double y, GraphicInfo gi)
    {
        DrawRoundedRect(svg, x, y, gi.Width, gi.Height, 6, SvgStyle.ServiceTaskFill, SvgStyle.ServiceTaskStroke);
        DrawGearIcon(svg, x + 8, y + 8);
        DrawTaskLabel(svg, serviceTask.Name, x, y, gi.Width, gi.Height);
    }

    private void DrawScriptTask(StringBuilder svg, ScriptTask scriptTask, double x, double y, GraphicInfo gi)
    {
        DrawRoundedRect(svg, x, y, gi.Width, gi.Height, 6, SvgStyle.ScriptTaskFill, SvgStyle.ScriptTaskStroke);
        DrawScriptIcon(svg, x + 8, y + 8);
        DrawTaskLabel(svg, scriptTask.Name, x, y, gi.Width, gi.Height);
    }

    private void DrawReceiveTask(StringBuilder svg, ReceiveTask receiveTask, double x, double y, GraphicInfo gi)
    {
        DrawRoundedRect(svg, x, y, gi.Width, gi.Height, 6, "#E8F5E9", "#4CAF50");
        DrawTaskLabel(svg, receiveTask.Name, x, y, gi.Width, gi.Height);
    }

    private void DrawSendTask(StringBuilder svg, SendTask sendTask, double x, double y, GraphicInfo gi)
    {
        DrawRoundedRect(svg, x, y, gi.Width, gi.Height, 6, "#E3F2FD", "#1976D2");
        DrawTaskLabel(svg, sendTask.Name, x, y, gi.Width, gi.Height);
    }

    private void DrawManualTask(StringBuilder svg, ManualTask manualTask, double x, double y, GraphicInfo gi)
    {
        DrawRoundedRect(svg, x, y, gi.Width, gi.Height, 6, "#FFF8E1", "#F57F17");
        DrawTaskLabel(svg, manualTask.Name, x, y, gi.Width, gi.Height);
    }

    private void DrawBusinessRuleTask(StringBuilder svg, BusinessRuleTask businessRuleTask, double x, double y, GraphicInfo gi)
    {
        DrawRoundedRect(svg, x, y, gi.Width, gi.Height, 6, "#F3E5F5", "#7B1FA2");
        DrawTaskLabel(svg, businessRuleTask.Name, x, y, gi.Width, gi.Height);
    }

    private void DrawExclusiveGateway(StringBuilder svg, ExclusiveGateway gateway, double x, double y, GraphicInfo gi)
    {
        var cx = x + gi.Width / 2;
        var cy = y + gi.Height / 2;
        var half = gi.Width / 2;

        svg.AppendLine($"  <polygon points=\"{cx},{cy - half} {cx + half},{cy} {cx},{cy + half} {cx - half},{cy}\" fill=\"{SvgStyle.GatewayFill}\" stroke=\"{SvgStyle.GatewayStroke}\" stroke-width=\"{SvgStyle.StrokeWidth}\"/>");

        var crossSize = half * 0.4;
        svg.AppendLine($"  <line x1=\"{cx - crossSize}\" y1=\"{cy}\" x2=\"{cx + crossSize}\" y2=\"{cy}\" stroke=\"{SvgStyle.GatewayStroke}\" stroke-width=\"{SvgStyle.StrokeWidth}\"/>");
        svg.AppendLine($"  <line x1=\"{cx}\" y1=\"{cy - crossSize}\" x2=\"{cx}\" y2=\"{cy + crossSize}\" stroke=\"{SvgStyle.GatewayStroke}\" stroke-width=\"{SvgStyle.StrokeWidth}\"/>");

        if (!string.IsNullOrEmpty(gateway.Name))
            DrawLabel(svg, gateway.Name, cx, y + gi.Height + 8);
    }

    private void DrawParallelGateway(StringBuilder svg, ParallelGateway gateway, double x, double y, GraphicInfo gi)
    {
        var cx = x + gi.Width / 2;
        var cy = y + gi.Height / 2;
        var half = gi.Width / 2;

        svg.AppendLine($"  <polygon points=\"{cx},{cy - half} {cx + half},{cy} {cx},{cy + half} {cx - half},{cy}\" fill=\"{SvgStyle.GatewayFill}\" stroke=\"{SvgStyle.GatewayStroke}\" stroke-width=\"{SvgStyle.StrokeWidth}\"/>");

        var crossSize = half * 0.4;
        svg.AppendLine($"  <line x1=\"{cx - crossSize}\" y1=\"{cy}\" x2=\"{cx + crossSize}\" y2=\"{cy}\" stroke=\"{SvgStyle.GatewayStroke}\" stroke-width=\"{SvgStyle.StrokeWidth * 1.5}\"/>");
        svg.AppendLine($"  <line x1=\"{cx}\" y1=\"{cy - crossSize}\" x2=\"{cx}\" y2=\"{cy + crossSize}\" stroke=\"{SvgStyle.GatewayStroke}\" stroke-width=\"{SvgStyle.StrokeWidth * 1.5}\"/>");

        if (!string.IsNullOrEmpty(gateway.Name))
            DrawLabel(svg, gateway.Name, cx, y + gi.Height + 8);
    }

    private void DrawInclusiveGateway(StringBuilder svg, InclusiveGateway gateway, double x, double y, GraphicInfo gi)
    {
        var cx = x + gi.Width / 2;
        var cy = y + gi.Height / 2;
        var half = gi.Width / 2;

        svg.AppendLine($"  <polygon points=\"{cx},{cy - half} {cx + half},{cy} {cx},{cy + half} {cx - half},{cy}\" fill=\"{SvgStyle.GatewayFill}\" stroke=\"{SvgStyle.GatewayStroke}\" stroke-width=\"{SvgStyle.StrokeWidth}\"/>");

        var circleR = half * 0.35;
        svg.AppendLine($"  <circle cx=\"{cx}\" cy=\"{cy}\" r=\"{circleR}\" fill=\"none\" stroke=\"{SvgStyle.GatewayStroke}\" stroke-width=\"{SvgStyle.StrokeWidth}\"/>");

        if (!string.IsNullOrEmpty(gateway.Name))
            DrawLabel(svg, gateway.Name, cx, y + gi.Height + 8);
    }

    private void DrawSubProcess(StringBuilder svg, SubProcess subProcess, double x, double y, GraphicInfo gi)
    {
        DrawRoundedRect(svg, x, y, gi.Width, gi.Height, 10, SvgStyle.SubProcessFill, SvgStyle.SubProcessStroke);

        if (!string.IsNullOrEmpty(subProcess.Name))
        {
            svg.AppendLine($"  <text x=\"{x + 10}\" y=\"{y + 20}\" font-family=\"{SvgStyle.FontFamily}\" font-size=\"{SvgStyle.FontSize}\" fill=\"#333333\">{EscapeXml(subProcess.Name)}</text>");
        }

        var plusSize = 6;
        var plusCx = x + gi.Width / 2;
        var plusCy = y + gi.Height - 12;
        svg.AppendLine($"  <rect x=\"{plusCx - plusSize - 2}\" y=\"{plusCy - plusSize - 2}\" width=\"{plusSize * 2 + 4}\" height=\"{plusSize * 2 + 4}\" fill=\"{SvgStyle.SubProcessFill}\" stroke=\"{SvgStyle.SubProcessStroke}\" stroke-width=\"1\"/>");
        svg.AppendLine($"  <line x1=\"{plusCx}\" y1=\"{plusCy - plusSize}\" x2=\"{plusCx}\" y2=\"{plusCy + plusSize}\" stroke=\"{SvgStyle.SubProcessStroke}\" stroke-width=\"1.5\"/>");
        svg.AppendLine($"  <line x1=\"{plusCx - plusSize}\" y1=\"{plusCy}\" x2=\"{plusCx + plusSize}\" y2=\"{plusCy}\" stroke=\"{SvgStyle.SubProcessStroke}\" stroke-width=\"1.5\"/>");
    }

    private void DrawCallActivity(StringBuilder svg, CallActivity callActivity, double x, double y, GraphicInfo gi)
    {
        DrawRoundedRect(svg, x, y, gi.Width, gi.Height, 6, "#E8EAF6", "#283593");
        svg.AppendLine($"  <rect x=\"{x + 3}\" y=\"{y + 3}\" width=\"{gi.Width - 6}\" height=\"{gi.Height - 6}\" rx=\"4\" ry=\"4\" fill=\"none\" stroke=\"#283593\" stroke-width=\"1\"/>");
        DrawTaskLabel(svg, callActivity.Name, x, y, gi.Width, gi.Height);
    }

    private void DrawSequenceFlow(StringBuilder svg, SequenceFlow flow,
        Dictionary<string, GraphicInfo> graphicInfo, BoundsInfo bounds)
    {
        if (flow.SourceRef == null || flow.TargetRef == null)
            return;

        if (!graphicInfo.TryGetValue(flow.SourceRef, out var sourceGi))
            return;
        if (!graphicInfo.TryGetValue(flow.TargetRef, out var targetGi))
            return;

        var (sx, sy) = GetConnectionPoint(sourceGi, targetGi, bounds);
        var (tx, ty) = GetConnectionPoint(targetGi, sourceGi, bounds);

        svg.AppendLine($"  <line x1=\"{sx}\" y1=\"{sy}\" x2=\"{tx}\" y2=\"{ty}\" stroke=\"{SvgStyle.SequenceFlowStroke}\" stroke-width=\"1.5\" marker-end=\"url(#arrowhead)\"/>");

        if (!string.IsNullOrEmpty(flow.Name))
        {
            var midX = (sx + tx) / 2;
            var midY = (sy + ty) / 2;
            svg.AppendLine($"  <text x=\"{midX}\" y=\"{midY - 6}\" font-family=\"{SvgStyle.FontFamily}\" font-size=\"10\" fill=\"#666666\" text-anchor=\"middle\">{EscapeXml(flow.Name)}</text>");
        }
    }

    private (double X, double Y) GetConnectionPoint(GraphicInfo source, GraphicInfo target, BoundsInfo bounds)
    {
        var sx = source.X - bounds.MinX + Padding;
        var sy = source.Y - bounds.MinY + Padding;
        var tx = target.X - bounds.MinX + Padding;
        var ty = target.Y - bounds.MinY + Padding;

        var sourceCx = sx + source.Width / 2;
        var sourceCy = sy + source.Height / 2;
        var targetCx = tx + target.Width / 2;
        var targetCy = ty + target.Height / 2;

        var dx = targetCx - sourceCx;
        var dy = targetCy - sourceCy;

        if (Math.Abs(dx) < 0.01 && Math.Abs(dy) < 0.01)
            return (sourceCx, sourceCy);

        var isEvent = source.Width <= 36 && source.Height <= 36;
        var isGateway = Math.Abs(source.Width - source.Height) < 1 && source.Width < 60 && source.Height < 60;

        if (isEvent)
        {
            var r = source.Width / 2;
            var angle = Math.Atan2(dy, dx);
            return (sourceCx + r * Math.Cos(angle), sourceCy + r * Math.Sin(angle));
        }

        if (isGateway)
        {
            var half = source.Width / 2;
            if (Math.Abs(dx) > Math.Abs(dy))
            {
                var sign = dx > 0 ? 1 : -1;
                var intersectX = sign * half;
                var intersectY = intersectX * dy / dx;
                if (Math.Abs(intersectY) <= half)
                    return (sourceCx + intersectX, sourceCy + intersectY);
            }
            else
            {
                var sign = dy > 0 ? 1 : -1;
                var intersectY = sign * half;
                var intersectX = intersectY * dx / dy;
                if (Math.Abs(intersectX) <= half)
                    return (sourceCx + intersectX, sourceCy + intersectY);
            }
            return (sourceCx, sourceCy);
        }

        if (Math.Abs(dx) * source.Height > Math.Abs(dy) * source.Width)
        {
            var sign = dx > 0 ? 1 : -1;
            var ratio = (source.Width / 2) / Math.Abs(dx);
            return (sourceCx + sign * source.Width / 2, sourceCy + dy * ratio);
        }
        else
        {
            var sign = dy > 0 ? 1 : -1;
            var ratio = (source.Height / 2) / Math.Abs(dy);
            return (sourceCx + dx * ratio, sourceCy + sign * source.Height / 2);
        }
    }

    private void DrawRoundedRect(StringBuilder svg, double x, double y, double width, double height,
        double radius, string fill, string stroke)
    {
        svg.AppendLine($"  <rect x=\"{x}\" y=\"{y}\" width=\"{width}\" height=\"{height}\" rx=\"{radius}\" ry=\"{radius}\" fill=\"{fill}\" stroke=\"{stroke}\" stroke-width=\"{SvgStyle.StrokeWidth}\"/>");
    }

    private void DrawLabel(StringBuilder svg, string? text, double cx, double y)
    {
        if (string.IsNullOrEmpty(text))
            return;
        svg.AppendLine($"  <text x=\"{cx}\" y=\"{y}\" font-family=\"{SvgStyle.FontFamily}\" font-size=\"{SvgStyle.FontSize}\" fill=\"#333333\" text-anchor=\"middle\">{EscapeXml(text)}</text>");
    }

    private void DrawTaskLabel(StringBuilder svg, string? text, double x, double y, double width, double height)
    {
        if (string.IsNullOrEmpty(text))
            return;
        var cx = x + width / 2;
        var cy = y + height / 2 + SvgStyle.FontSize / 3;
        svg.AppendLine($"  <text x=\"{cx}\" y=\"{cy}\" font-family=\"{SvgStyle.FontFamily}\" font-size=\"{SvgStyle.FontSize}\" fill=\"#333333\" text-anchor=\"middle\">{EscapeXml(text)}</text>");
    }

    private void DrawUserIcon(StringBuilder svg, double x, double y)
    {
        svg.AppendLine($"  <circle cx=\"{x + 8}\" cy=\"{y + 4}\" r=\"4\" fill=\"none\" stroke=\"{SvgStyle.UserTaskStroke}\" stroke-width=\"1\"/>");
        svg.AppendLine($"  <path d=\"M{x},{y + 16} Q{x + 8},{y + 10} {x + 16},{y + 16}\" fill=\"none\" stroke=\"{SvgStyle.UserTaskStroke}\" stroke-width=\"1\"/>");
    }

    private void DrawGearIcon(StringBuilder svg, double x, double y)
    {
        var cx = x + 8;
        var cy = y + 8;
        svg.AppendLine($"  <circle cx=\"{cx}\" cy=\"{cy}\" r=\"5\" fill=\"none\" stroke=\"{SvgStyle.ServiceTaskStroke}\" stroke-width=\"1\"/>");
        svg.AppendLine($"  <circle cx=\"{cx}\" cy=\"{cy}\" r=\"2\" fill=\"{SvgStyle.ServiceTaskStroke}\"/>");
    }

    private void DrawScriptIcon(StringBuilder svg, double x, double y)
    {
        svg.AppendLine($"  <path d=\"M{x},{y + 2} L{x + 16},{y + 2} L{x + 14},{y + 8} L{x + 16},{y + 14} L{x},{y + 14} L{x + 2},{y + 8} Z\" fill=\"none\" stroke=\"{SvgStyle.ScriptTaskStroke}\" stroke-width=\"1\"/>");
    }

    private static BoundsInfo CalculateBounds(Dictionary<string, GraphicInfo> graphicInfo)
    {
        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;

        foreach (var gi in graphicInfo.Values)
        {
            if (gi.X < minX) minX = gi.X;
            if (gi.Y < minY) minY = gi.Y;
            if (gi.X + gi.Width > maxX) maxX = gi.X + gi.Width;
            if (gi.Y + gi.Height > maxY) maxY = gi.Y + gi.Height;
        }

        if (minX == double.MaxValue)
        {
            minX = 0;
            minY = 0;
            maxX = 100;
            maxY = 100;
        }

        return new BoundsInfo { MinX = minX, MinY = minY, MaxX = maxX, MaxY = maxY };
    }

    private static string EscapeXml(string? text)
    {
        if (text == null) return "";
        return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
    }

    private string GenerateEmptySvg()
    {
        var svg = new StringBuilder();
        svg.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"200\" height=\"100\" viewBox=\"0 0 200 100\">");
        svg.AppendLine("  <rect width=\"100%\" height=\"100%\" fill=\"white\"/>");
        svg.AppendLine("  <text x=\"100\" y=\"50\" font-family=\"Arial, sans-serif\" font-size=\"14\" fill=\"#999999\" text-anchor=\"middle\">No process to display</text>");
        svg.AppendLine("</svg>");
        return svg.ToString();
    }
}

internal class BoundsInfo
{
    public double MinX { get; set; }
    public double MinY { get; set; }
    public double MaxX { get; set; }
    public double MaxY { get; set; }
}
