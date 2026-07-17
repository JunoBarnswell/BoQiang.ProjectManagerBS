using System;
using System.Collections.Generic;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Service;

public class ProcessInstanceCreationOptions
{
    public ProcessDefinitionRecord? ProcessDefinition { get; private set; }
    public string? BusinessKey { get; private set; }
    public string? ProcessInstanceName { get; private set; }
    public Dictionary<string, object?> Variables { get; private set; } = new();
    public Dictionary<string, object?> TransientVariables { get; private set; } = new();
    public string? LinkedProcessInstanceId { get; private set; }
    public string? LinkedProcessInstanceType { get; private set; }
    public string? TenantId { get; private set; }

    private ProcessInstanceCreationOptions() { }

    public static Builder NewBuilder(ProcessDefinitionRecord processDefinition)
    {
        return new Builder(processDefinition);
    }

    public class Builder
    {
        private readonly ProcessDefinitionRecord _processDefinition;
        private string? _businessKey;
        private string? _processInstanceName;
        private Dictionary<string, object?> _variables = new();
        private Dictionary<string, object?> _transientVariables = new();
        private string? _linkedProcessInstanceId;
        private string? _linkedProcessInstanceType;
        private string? _tenantId;

        internal Builder(ProcessDefinitionRecord processDefinition)
        {
            _processDefinition = processDefinition;
        }

        public Builder BusinessKey(string businessKey)
        {
            _businessKey = businessKey;
            return this;
        }

        public Builder ProcessInstanceName(string processInstanceName)
        {
            _processInstanceName = processInstanceName;
            return this;
        }

        public Builder Variables(Dictionary<string, object?> variables)
        {
            _variables = variables;
            return this;
        }

        public Builder AddVariable(string name, object? value)
        {
            _variables[name] = value;
            return this;
        }

        public Builder TransientVariables(Dictionary<string, object?> transientVariables)
        {
            _transientVariables = transientVariables;
            return this;
        }

        public Builder LinkedProcessInstanceId(string linkedProcessInstanceId)
        {
            _linkedProcessInstanceId = linkedProcessInstanceId;
            return this;
        }

        public Builder LinkedProcessInstanceType(string linkedProcessInstanceType)
        {
            _linkedProcessInstanceType = linkedProcessInstanceType;
            return this;
        }

        public Builder TenantId(string tenantId)
        {
            _tenantId = tenantId;
            return this;
        }

        public ProcessInstanceCreationOptions Build()
        {
            return new ProcessInstanceCreationOptions
            {
                ProcessDefinition = _processDefinition,
                BusinessKey = _businessKey,
                ProcessInstanceName = _processInstanceName,
                Variables = _variables,
                TransientVariables = _transientVariables,
                LinkedProcessInstanceId = _linkedProcessInstanceId,
                LinkedProcessInstanceType = _linkedProcessInstanceType,
                TenantId = _tenantId
            };
        }
    }
}
