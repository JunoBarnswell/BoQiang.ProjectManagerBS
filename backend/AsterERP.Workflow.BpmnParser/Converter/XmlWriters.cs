using System.Text;
using System.Xml;

namespace AsterERP.Workflow.BpmnParser.Converter;

public class DelegatingXMLStreamWriter : XmlWriter
{
    private readonly XmlWriter _delegate;

    public DelegatingXMLStreamWriter(XmlWriter writer)
    {
        _delegate = writer;
    }

    public override WriteState WriteState => _delegate.WriteState;

    public override void Flush() => _delegate.Flush();
    public override string? LookupPrefix(string ns) => _delegate.LookupPrefix(ns);
    public override void WriteBase64(byte[] buffer, int index, int count) => _delegate.WriteBase64(buffer, index, count);
    public override void WriteCData(string? text) => _delegate.WriteCData(text);
    public override void WriteCharEntity(char ch) => _delegate.WriteCharEntity(ch);
    public override void WriteChars(char[] buffer, int index, int count) => _delegate.WriteChars(buffer, index, count);
    public override void WriteComment(string? text) => _delegate.WriteComment(text);
    public override void WriteDocType(string name, string? pubid, string? sysid, string? subset) => _delegate.WriteDocType(name, pubid, sysid, subset);
    public override void WriteEndAttribute() => _delegate.WriteEndAttribute();
    public override void WriteEndDocument() => _delegate.WriteEndDocument();
    public override void WriteEndElement() => _delegate.WriteEndElement();
    public override void WriteEntityRef(string name) => _delegate.WriteEntityRef(name);
    public override void WriteFullEndElement() => _delegate.WriteFullEndElement();
    public override void WriteProcessingInstruction(string name, string? text) => _delegate.WriteProcessingInstruction(name, text);
    public override void WriteRaw(string data) => _delegate.WriteRaw(data);
    public override void WriteRaw(char[] buffer, int index, int count) => _delegate.WriteRaw(buffer, index, count);
    public override void WriteStartAttribute(string? prefix, string localName, string? ns) => _delegate.WriteStartAttribute(prefix, localName, ns);
    public override void WriteStartDocument(bool standalone) => _delegate.WriteStartDocument(standalone);
    public override void WriteStartDocument() => _delegate.WriteStartDocument();
    public override void WriteStartElement(string? prefix, string localName, string? ns) => _delegate.WriteStartElement(prefix, localName, ns);
    public override void WriteString(string? text) => _delegate.WriteString(text);
    public override void WriteSurrogateCharEntity(char lowChar, char highChar) => _delegate.WriteSurrogateCharEntity(lowChar, highChar);
    public override void WriteWhitespace(string? ws) => _delegate.WriteWhitespace(ws);
}

public class IndentingXMLStreamWriter : DelegatingXMLStreamWriter
{
    private int _indentLevel;
    private bool _indentNext;
    private const string IndentChars = "  ";
    private const string NewLine = "\n";

    public IndentingXMLStreamWriter(XmlWriter writer) : base(writer)
    {
        _indentLevel = 0;
        _indentNext = false;
    }

    public override void WriteStartElement(string? prefix, string localName, string? ns)
    {
        WriteIndentIfNeeded();
        base.WriteStartElement(prefix, localName, ns);
        _indentLevel++;
        _indentNext = true;
    }

    public override void WriteEndElement()
    {
        _indentLevel--;
        if (_indentNext && _indentLevel > 0)
        {
            WriteIndentIfNeeded();
        }
        base.WriteEndElement();
        _indentNext = true;
    }

    public override void WriteFullEndElement()
    {
        _indentLevel--;
        if (_indentNext && _indentLevel > 0)
        {
            WriteIndentIfNeeded();
        }
        base.WriteFullEndElement();
        _indentNext = true;
    }

    public override void WriteStartDocument()
    {
        base.WriteStartDocument();
        _indentNext = true;
    }

    public override void WriteStartDocument(bool standalone)
    {
        base.WriteStartDocument(standalone);
        _indentNext = true;
    }

    public override void WriteCData(string? text)
    {
        _indentNext = false;
        base.WriteCData(text);
    }

    public override void WriteString(string? text)
    {
        _indentNext = false;
        base.WriteString(text);
    }

    public override void WriteComment(string? text)
    {
        WriteIndentIfNeeded();
        base.WriteComment(text);
        _indentNext = true;
    }

    public override void WriteProcessingInstruction(string name, string? text)
    {
        WriteIndentIfNeeded();
        base.WriteProcessingInstruction(name, text);
        _indentNext = true;
    }

    private void WriteIndentIfNeeded()
    {
        if (!_indentNext) return;
        _indentNext = false;
        base.WriteRaw(NewLine);
        for (int i = 0; i < _indentLevel; i++)
            base.WriteRaw(IndentChars);
    }
}
