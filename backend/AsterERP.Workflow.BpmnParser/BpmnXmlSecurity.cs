using System.Xml;

namespace AsterERP.Workflow.BpmnParser;

/// <summary>
/// Defines the mandatory trust boundary for BPMN XML before it reaches any legacy or current parser.
/// </summary>
public static class BpmnXmlSecurity
{
    public const int MaxDocumentBytes = 2_000_000;
    public const long MaxDocumentCharacters = 2_000_000;

    public static void Validate(byte[]? xmlBytes)
    {
        if (xmlBytes is null || xmlBytes.Length == 0)
        {
            throw new XmlException("BPMN XML is empty.");
        }

        if (xmlBytes.Length > MaxDocumentBytes)
        {
            throw new XmlException($"BPMN XML exceeds the {MaxDocumentBytes} byte limit.");
        }

        using var stream = new MemoryStream(xmlBytes, writable: false);
        using var reader = XmlReader.Create(stream, CreateReaderSettings());
        while (reader.Read())
        {
            // Reading the complete document enforces DTD, entity and size restrictions.
        }
    }

    public static void Validate(string? xmlContent)
    {
        if (string.IsNullOrWhiteSpace(xmlContent))
        {
            throw new XmlException("BPMN XML is empty.");
        }

        if (xmlContent.Length > MaxDocumentCharacters)
        {
            throw new XmlException($"BPMN XML exceeds the {MaxDocumentCharacters} character limit.");
        }

        using var textReader = new StringReader(xmlContent);
        using var reader = XmlReader.Create(textReader, CreateReaderSettings());
        while (reader.Read())
        {
            // Reading the complete document enforces DTD, entity and size restrictions.
        }
    }

    private static XmlReaderSettings CreateReaderSettings() => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        MaxCharactersInDocument = MaxDocumentCharacters,
        MaxCharactersFromEntities = 1_024,
        ConformanceLevel = ConformanceLevel.Document,
        CloseInput = false
    };
}
