using System.Text;
using System.Xml;
using AsterERP.Workflow.BpmnParser;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Service;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class BpmnXmlSecurityTests
{
    private const string ValidBpmn = """
        <?xml version="1.0" encoding="UTF-8"?>
        <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL" targetNamespace="urn:test">
          <process id="process_1" isExecutable="true" />
        </definitions>
        """;

    private const string BpmnWithDtd = """
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE definitions [<!ENTITY internal "expanded">]>
        <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL" targetNamespace="urn:test">
          <process id="&internal;" isExecutable="true" />
        </definitions>
        """;

    [Fact]
    public void Security_policy_accepts_a_small_DTD_free_BPMN_document()
    {
        BpmnXmlSecurity.Validate(ValidBpmn);
        BpmnXmlSecurity.Validate(Encoding.UTF8.GetBytes(ValidBpmn));
    }

    [Fact]
    public void Security_policy_rejects_DTD_and_entity_declarations()
    {
        Assert.Throws<XmlException>(() => BpmnXmlSecurity.Validate(BpmnWithDtd));
        Assert.Throws<XmlException>(() => BpmnXmlSecurity.Validate(Encoding.UTF8.GetBytes(BpmnWithDtd)));
    }

    [Fact]
    public void Security_policy_rejects_oversized_documents_before_parsing()
    {
        var bytes = new byte[BpmnXmlSecurity.MaxDocumentBytes + 1];

        var exception = Assert.Throws<XmlException>(() => BpmnXmlSecurity.Validate(bytes));

        Assert.Contains("byte limit", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Repository_validation_rejects_unsafe_XML_before_command_execution()
    {
        var executor = new RecordingCommandExecutor();
        var repository = new RepositoryServiceImplementation(executor);

        var errors = await repository.ValidateProcessAsync(Encoding.UTF8.GetBytes(BpmnWithDtd));

        var error = Assert.Single(errors);
        Assert.Equal("BPMN_SECURITY_VALIDATION", error.Type);
        Assert.False(error.IsWarning);
        Assert.False(executor.WasInvoked);
    }

    [Fact]
    public async Task Repository_deployment_rejects_unsafe_BPMN_before_command_execution()
    {
        var executor = new RecordingCommandExecutor();
        var repository = new RepositoryServiceImplementation(executor);
        var resources = new Dictionary<string, byte[]>
        {
            ["unsafe.bpmn"] = Encoding.UTF8.GetBytes(BpmnWithDtd)
        };

        await Assert.ThrowsAsync<XmlException>(() =>
            repository.DeployAsync("unsafe", null, null, resources));

        Assert.False(executor.WasInvoked);
    }

    private sealed class RecordingCommandExecutor : ICommandExecutor
    {
        public bool WasInvoked { get; private set; }

        public ICommandInterceptor First => throw new NotSupportedException();

        public T Execute<T>(ICommand<T> command)
        {
            WasInvoked = true;
            throw new InvalidOperationException("Unsafe BPMN reached the command executor.");
        }

        public Task<T> ExecuteAsync<T>(ICommand<T> command, CancellationToken cancellationToken = default)
        {
            WasInvoked = true;
            throw new InvalidOperationException("Unsafe BPMN reached the command executor.");
        }
    }
}
