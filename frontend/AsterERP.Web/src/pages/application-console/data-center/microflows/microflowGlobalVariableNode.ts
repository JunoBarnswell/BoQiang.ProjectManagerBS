import type {
  MicroflowDefinition,
  MicroflowDomainField,
  MicroflowNode,
  MicroflowVariable,
  MicroflowValueExpression
} from '../../../../api/application-data-center/applicationDataCenter.types';

import { cloneMicroflowField, normalizeVariableValueType } from './microflowVariableSchema';

export const globalVariablesNodeType = 'globalVariables';

export function isGlobalVariablesNode(node: MicroflowNode | null | undefined): boolean {
  return node?.type === globalVariablesNodeType;
}

export function createDefaultGlobalVariable(nodeId: string, index: number): MicroflowVariable {
  return {
    defaultValue: '',
    fields: [],
    schemaObjectCode: null,
    sourceNodeId: nodeId,
    valueType: 'string',
    variableCode: `global_${index + 1}`,
    variableName: `全局变量 ${index + 1}`
  };
}

export function readGlobalVariableNodeVariables(node: MicroflowNode): MicroflowVariable[] {
  const variables = Array.isArray(node.config.variables)
    ? node.config.variables.filter(isRecord)
    : [];

  return variables.map((variable, index) => normalizeGlobalVariable(variable, node.id, index));
}

export function writeGlobalVariableNodeVariables(
  definition: MicroflowDefinition,
  nodeId: string,
  variables: MicroflowVariable[]
): MicroflowDefinition {
  const normalizedVariables = variables.map((variable, index) => normalizeGlobalVariable(variable, nodeId, index));
  const nodes = definition.nodes.map((node) => node.id === nodeId
    ? {
        ...node,
        config: {
          ...node.config,
          variables: normalizedVariables
        }
      }
    : node);

  return syncGlobalVariableDefinitions({
    ...definition,
    nodes
  });
}

export function syncGlobalVariableDefinitions(definition: MicroflowDefinition): MicroflowDefinition {
  const globalNodeIds = new Set(
    definition.nodes
      .filter(isGlobalVariablesNode)
      .map((node) => node.id)
  );
  const globalVariables = definition.nodes
    .filter(isGlobalVariablesNode)
    .flatMap(readGlobalVariableNodeVariables);
  const retainedVariables = definition.variables.filter((variable) => {
    const sourceNodeId = String(variable.sourceNodeId ?? '').trim();
    return !sourceNodeId || !globalNodeIds.has(sourceNodeId);
  });

  return {
    ...definition,
    variables: [
      ...retainedVariables,
      ...globalVariables
    ]
  };
}

export function removeGlobalVariableNodeDefinitions(
  definition: MicroflowDefinition,
  nodeIds: string[]
): MicroflowDefinition {
  const removedNodeIds = new Set(nodeIds);
  return {
    ...definition,
    variables: definition.variables.filter((variable) => {
      const sourceNodeId = String(variable.sourceNodeId ?? '').trim();
      return !sourceNodeId || !removedNodeIds.has(sourceNodeId);
    })
  };
}

export function findGlobalVariableNodeDeleteBlockers(
  definition: MicroflowDefinition,
  nodeIds: string[]
): string[] {
  const removedNodeIds = new Set(nodeIds);
  const removedVariableCodes = new Set(
    definition.nodes
      .filter((node) => removedNodeIds.has(node.id) && isGlobalVariablesNode(node))
      .flatMap(readGlobalVariableNodeVariables)
      .map((variable) => variable.variableCode.trim().toLowerCase())
      .filter(Boolean)
  );
  if (removedVariableCodes.size === 0) {
    return [];
  }

  const blockers: string[] = [];
  for (const node of definition.nodes) {
    if (removedNodeIds.has(node.id)) {
      continue;
    }

    const references = collectVariableExpressionReferences(node.config);
    for (const reference of references) {
      if (removedVariableCodes.has(reference.variableCode.toLowerCase())) {
        blockers.push(`${node.name || node.id} 引用了全局变量 ${reference.variableCode}（${reference.path}）`);
      }
    }
  }

  return [...new Set(blockers)];
}

export function createGlobalVariableNodeSummary(node: MicroflowNode): string {
  const variables = readGlobalVariableNodeVariables(node);
  const errorCount = variables.filter((variable) => !variable.variableCode.trim()).length;
  const types = [...new Set(variables.map((variable) => normalizeVariableValueType(variable.valueType)))];
  const typeSummary = types.length > 0 ? ` · ${types.join('/')}` : '';
  return `${variables.length} 变量${typeSummary}${errorCount > 0 ? ` · ${errorCount} 错误` : ''}`;
}

function normalizeGlobalVariable(value: Record<string, unknown> | MicroflowVariable, nodeId: string, index: number): MicroflowVariable {
  const variableCode = String(value.variableCode ?? '').trim();
  const valueType = normalizeVariableValueType(String(value.valueType ?? 'string'));
  return {
    defaultValue: value.defaultValue,
    fields: Array.isArray(value.fields)
      ? value.fields.filter(isRecord).map((field) => cloneMicroflowField(field as unknown as MicroflowDomainField))
      : [],
    schemaObjectCode: typeof value.schemaObjectCode === 'string' ? value.schemaObjectCode : null,
    sourceNodeId: nodeId,
    valueType,
    variableCode,
    variableName: String(value.variableName ?? (variableCode || `全局变量 ${index + 1}`)).trim()
  };
}

function collectVariableExpressionReferences(value: unknown, references: Array<{ path: string; variableCode: string }> = []) {
  if (isVariableExpression(value)) {
    const reference = value.ref;
    if (value.kind === 'ref' && reference && ['global', 'nodeOutput', 'nodeInput', 'trigger'].includes(reference.sourceType)) {
      const root = reference.outputKey || reference.variableId;
      const path = [root, ...reference.fieldPath].filter(Boolean).join('.');
      references.push({
        path,
        variableCode: root
      });
    }
    return references;
  }

  if (Array.isArray(value)) {
    value.forEach((item) => collectVariableExpressionReferences(item, references));
    return references;
  }

  if (isRecord(value)) {
    Object.values(value).forEach((item) => collectVariableExpressionReferences(item, references));
  }

  return references;
}

function isVariableExpression(value: unknown): value is MicroflowValueExpression {
  return isRecord(value)
    && 'kind' in value
    && (
      'ref' in value ||
      'value' in value ||
      'args' in value ||
      'items' in value ||
      'properties' in value
    );
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value && typeof value === 'object' && !Array.isArray(value));
}
