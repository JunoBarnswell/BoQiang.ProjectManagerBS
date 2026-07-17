import type {
  MicroflowDefinition,
  MicroflowDomainField,
  MicroflowNode,
  MicroflowVariable
} from '../../../../api/application-data-center/applicationDataCenter.types';

import { syncGlobalVariableDefinitions } from './microflowGlobalVariableNode';
import { cloneMicroflowField, normalizeVariableValueType } from './microflowVariableSchema';

export function normalizeMicroflowDefinitionForSave(definition: MicroflowDefinition): MicroflowDefinition {
  const withSyncedGlobals = syncGlobalVariableDefinitions(definition);
  const nodes = withSyncedGlobals.nodes.map(normalizeNodeBeforeSave);
  const outputs = collectReturnOutputs(nodes);
  return {
    ...withSyncedGlobals,
    nodes,
    outputs,
    variables: filterProcessVariables(
      normalizeProcessVariables(withSyncedGlobals.variables),
      withSyncedGlobals.inputs,
      outputs
    )
  };
}

export function getVariableRootCode(path: string | null | undefined): string {
  return String(path ?? '').trim().split('.')[0]?.trim() ?? '';
}

export function getVariableNestedPath(path: string | null | undefined): string {
  const parts = String(path ?? '')
    .trim()
    .split('.')
    .map((item) => item.trim())
    .filter(Boolean);
  return parts.slice(1).join('.');
}

export function normalizeSetVariableOutputFields(targetPath: string, fields: MicroflowDomainField[]): MicroflowDomainField[] {
  const nestedPath = getVariableNestedPath(targetPath);
  if (!nestedPath) {
    return fields.map(cloneMicroflowField);
  }

  const result: MicroflowDomainField[] = [createObjectField(nestedPath)];
  for (const field of fields) {
    const cloned = cloneMicroflowField(field);
    const fieldCode = cloned.fieldCode.trim();
    if (!fieldCode) {
      continue;
    }

    const normalizedCode = fieldCode === nestedPath || fieldCode.startsWith(`${nestedPath}.`)
      ? fieldCode
      : `${nestedPath}.${fieldCode}`;
    result.push({
      ...cloned,
      fieldCode: normalizedCode,
      fieldName: cloned.fieldName || normalizedCode
    });
  }

  return dedupeFields(result);
}

function normalizeNodeBeforeSave(node: MicroflowNode): MicroflowNode {
  if (node.type !== 'setVariable') {
    return node;
  }

  const targetPath = String(node.config.variableCode ?? '').trim();
  const rootCode = getVariableRootCode(targetPath);
  if (!rootCode) {
    return node;
  }

  const schema = isRecord(node.config.outputSchema)
    ? node.config.outputSchema
    : null;
  const fields = Array.isArray(schema?.fields)
    ? schema.fields.filter(isRecord).map((field) => cloneMicroflowField(field as unknown as MicroflowDomainField))
    : [];

  return {
    ...node,
    config: {
      ...node.config,
      outputSchema: {
        fields: normalizeSetVariableOutputFields(targetPath, fields),
        valueType: normalizeVariableValueType(String(schema?.valueType ?? 'object')),
        variableCode: rootCode,
        variableName: String(schema?.variableName ?? rootCode).trim() || rootCode
      }
    }
  };
}

function normalizeProcessVariables(variables: MicroflowVariable[]): MicroflowVariable[] {
  const byCode = new Map<string, MicroflowVariable>();
  for (const variable of variables) {
    const variableCode = variable.variableCode.trim();
    const rootCode = getVariableRootCode(variableCode);
    if (!rootCode) {
      continue;
    }

    if (rootCode === variableCode) {
      byCode.set(rootCode.toLowerCase(), {
        ...variable,
        fields: dedupeFields((variable.fields ?? []).map(cloneMicroflowField)),
        valueType: normalizeVariableValueType(variable.valueType),
        variableCode: rootCode
      });
      continue;
    }

    const existing = byCode.get(rootCode.toLowerCase()) ?? createObjectVariable(rootCode);
    byCode.set(rootCode.toLowerCase(), {
      ...existing,
      fields: dedupeFields([
        ...(existing.fields ?? []).map(cloneMicroflowField),
        ...normalizeSetVariableOutputFields(variableCode, variable.fields ?? [])
      ])
    });
  }

  return [...byCode.values()];
}

function filterProcessVariables(
  variables: MicroflowVariable[],
  inputs: MicroflowVariable[],
  outputs: MicroflowVariable[]
): MicroflowVariable[] {
  const reservedCodes = new Set(
    [...inputs, ...outputs]
      .map((variable) => variable.variableCode.trim().toLowerCase())
      .filter(Boolean)
  );
  return variables.filter((variable) => !reservedCodes.has(variable.variableCode.trim().toLowerCase()));
}

function collectReturnOutputs(nodes: MicroflowNode[]): MicroflowVariable[] {
  const outputs = new Map<string, MicroflowVariable>();
  for (const node of nodes) {
    if (node.type !== 'return' || !isRecord(node.config.outputSchema)) {
      continue;
    }

    const schema = node.config.outputSchema;
    const variableCode = String(schema.variableCode ?? '').trim();
    if (!variableCode) {
      continue;
    }

    const valueType = normalizeVariableValueType(String(schema.valueType ?? 'object'));
    outputs.set(variableCode.toLowerCase(), {
      defaultValue: valueType === 'array' ? [] : valueType === 'object' || valueType === 'json' ? {} : '',
      fields: Array.isArray(schema.fields)
        ? schema.fields.filter(isRecord).map((field) => cloneMicroflowField(field as unknown as MicroflowDomainField))
        : [],
      schemaObjectCode: null,
      valueType,
      variableCode,
      variableName: String(schema.variableName ?? variableCode).trim() || variableCode
    });
  }

  return [...outputs.values()];
}

function createObjectVariable(variableCode: string): MicroflowVariable {
  return {
    defaultValue: {},
    fields: [],
    schemaObjectCode: null,
    valueType: 'object',
    variableCode,
    variableName: variableCode
  };
}

function createObjectField(fieldCode: string): MicroflowDomainField {
  return {
    dataType: 'object',
    displayHelpers: [],
    fieldCode,
    fieldName: fieldCode,
    queryHelpers: [],
    readOnly: false,
    required: false,
    visible: true,
    writable: true,
    writeHelpers: []
  };
}

function dedupeFields(fields: MicroflowDomainField[]): MicroflowDomainField[] {
  const seen = new Set<string>();
  return fields.filter((field) => {
    const fieldCode = field.fieldCode.trim();
    if (!fieldCode) {
      return false;
    }

    const key = fieldCode.toLowerCase();
    if (seen.has(key)) {
      return false;
    }

    seen.add(key);
    return true;
  });
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value && typeof value === 'object' && !Array.isArray(value));
}
