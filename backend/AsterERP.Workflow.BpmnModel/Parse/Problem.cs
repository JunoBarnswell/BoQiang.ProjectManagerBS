namespace AsterERP.Workflow.BpmnModel.Parse;

public class Problem
{
    public string ErrorMessage { get; }
    public string? Resource { get; }
    public int Line { get; }
    public int Column { get; }

    public Problem(string errorMessage, string? localName, int lineNumber, int columnNumber)
    {
        ErrorMessage = errorMessage;
        Resource = localName;
        Line = lineNumber;
        Column = columnNumber;
    }

    public Problem(string errorMessage, BaseElement element)
    {
        ErrorMessage = errorMessage;
        Resource = element.Id;
        Line = element.XmlRowNumber;
        Column = element.XmlColumnNumber;
    }

    public Problem(string errorMessage, GraphicInfo graphicInfo)
    {
        ErrorMessage = errorMessage;
        Resource = graphicInfo.Element;
        Line = 0;
        Column = 0;
    }

    public override string ToString()
    {
        return ErrorMessage + (Resource != null ? " | " + Resource : "") + " | line " + Line + " | column " + Column;
    }
}

public class Warning
{
    public string WarningMessage { get; }
    public string? Resource { get; }
    public int Line { get; }
    public int Column { get; }

    public Warning(string warningMessage, string? localName, int lineNumber, int columnNumber)
    {
        WarningMessage = warningMessage;
        Resource = localName;
        Line = lineNumber;
        Column = columnNumber;
    }

    public Warning(string warningMessage, BaseElement element)
    {
        WarningMessage = warningMessage;
        Resource = element.Id;
        Line = element.XmlRowNumber;
        Column = element.XmlColumnNumber;
    }

    public override string ToString()
    {
        return WarningMessage + (Resource != null ? " | " + Resource : "") + " | line " + Line + " | column " + Column;
    }
}
