import type {
  MicroflowDefinition,
  MicroflowDomainField,
  MicroflowDomainObject,
  MicroflowNode,
  MicroflowSqlScript,
  MicroflowSqlScriptParameter,
  MicroflowVariable,
  MicroflowValueExpression
} from '../../../../api/application-data-center/applicationDataCenter.types';

import { getVariableRootCode } from './microflowDefinitionNormalizer';
import { syncGlobalVariableDefinitions } from './microflowGlobalVariableNode';
import {
  cloneMicroflowField,
  normalizeVariableFields,
  normalizeVariableValueType,
  type MicroflowVariableValueType
} from './microflowVariableSchema';

export type MicroflowContextVariableSource = 'input' | 'node' | 'output' | 'sqlResult' | 'variable';

export interface MicroflowContextVariable {
  expression: MicroflowValueExpression;
  fields: MicroflowDomainField[];
  id: string;
  sourceKind: MicroflowContextVariableSource;
  sourceLabel: string;
  sourceNodeId?: string;
  sourceNodeName?: string;
  valueType: MicroflowVariableValueType;
  variableCode: string;
  variableName: string;
}

export interface MicroflowReturnOutputSchema {
  arrayExpression?: MicroflowValueExpression | null;
  fields: MicroflowDomainField[];
  sourceMode?: 'fields' | 'sqlScript';
  sqlScript?: MicroflowSqlScript | null;
  valueType: MicroflowVariableValueType;
  variableCode: string;
  variableName: string;
}

export interface MicroflowReturnValidationIssue {
  message: string;
  path: string;
  severity: 'error' | 'warning';
}

export interface MicroflowNodeReferenceOption {
  description: string;
  expression: MicroflowValueExpression;
  field?: MicroflowDomainField;
  id: string;
  isField: boolean;
  label: string;
  sourceKind: MicroflowContextVariableSource;
  sourceNodeId?: string;
  sourceVariableCode: string;
  valueType: MicroflowVariableValueType;
}

export function listMicroflowContextVariables(
  definition: MicroflowDefinition,
  currentNodeId?: string | null
): MicroflowContextVariable[] {
  const scopedDefinition = syncGlobalVariableDefinitions(definition);
  const contexts: MicroflowContextVariable[] = [];
  const upstreamNodeIds = resolveUpstreamNodeIds(scopedDefinition, currentNodeId);
  const useUpstreamFilter = Boolean(currentNodeId);
  const currentNode = currentNodeId ? scopedDefinition.nodes.find((node) => node.id === currentNodeId) ?? null : null;
  const currentNodeOutputVariableCode = currentNode
    ? readNodeOutputVariableCode(currentNode, readConfiguredOutputSchema(currentNode.config.outputSchema))
    : '';

  for (const node of scopedDefinition.nodes) {
    if (node.id === currentNodeId) {
      continue;
    }

    if (useUpstreamFilter && !upstreamNodeIds.has(node.id)) {
      continue;
    }

    const nodeOutputSchema = readConfiguredOutputSchema(node.config.outputSchema);
    const targetVariable = readNodeOutputVariableCode(node, nodeOutputSchema);
    if (!targetVariable) {
      continue;
    }

    const schema = resolveVariableSchema(scopedDefinition, targetVariable);
    const nodeValueType = schema
      ? normalizeVariableValueType(schema.valueType)
      : normalizeVariableValueType(nodeOutputSchema?.valueType ?? inferNodeTargetValueType(node));
    const fields = resolveNodeContextFields(scopedDefinition, node, targetVariable, schema, nodeOutputSchema);
    contexts.push({
      expression: buildVariableExpression(targetVariable, nodeValueType),
      fields,
      id: `node:${node.id}:${targetVariable}`,
      sourceKind: 'node',
      sourceLabel: `${node.name}.${targetVariable}`,
      sourceNodeId: node.id,
      sourceNodeName: node.name,
      valueType: nodeValueType,
      variableCode: targetVariable,
      variableName: schema?.variableName || node.name || targetVariable
    });
  }

  appendVariableContexts(contexts, scopedDefinition.inputs, 'input');
  appendVariableContexts(
    contexts,
    scopedDefinition.variables,
    'variable',
    new Set(currentNodeOutputVariableCode ? [currentNodeOutputVariableCode.toLowerCase()] : [])
  );

  return dedupeContextVariables(contexts.filter((context) => context.variableCode.trim().length > 0));
}

export function listNodeInputReferenceOptions(
  definition: MicroflowDefinition,
  currentNodeId?: string | null
): MicroflowNodeReferenceOption[] {
  const scopedDefinition = syncGlobalVariableDefinitions(definition);
  const contexts = listMicroflowContextVariables(scopedDefinition, currentNodeId);
  const options: MicroflowNodeReferenceOption[] = [];

  for (const context of contexts) {
    options.push({
      description: `${context.variableName || context.variableCode} / ${context.valueType}`,
      expression: cloneExpression(context.expression),
      id: `${context.id}:self`,
      isField: false,
      label: context.sourceLabel,
      sourceKind: context.sourceKind,
      sourceNodeId: context.sourceNodeId,
      sourceVariableCode: context.variableCode,
      valueType: context.valueType
    });

    for (const field of context.fields) {
      const fieldCode = field.fieldCode.trim();
      if (!fieldCode) {
        continue;
      }

      const fieldType = normalizeVariableValueType(field.dataType);
      options.push({
        description: `${context.variableName || context.variableCode} · ${field.fieldName || fieldCode}`,
        expression: buildVariableFieldExpression(context.variableCode, [fieldCode], fieldType),
        field: cloneMicroflowField(field),
        id: `${context.id}:field:${fieldCode}`,
        isField: true,
        label: `${context.sourceLabel}.${fieldCode}`,
        sourceKind: context.sourceKind,
        sourceNodeId: context.sourceNodeId,
        sourceVariableCode: context.variableCode,
        valueType: fieldType
      });
    }
  }

  const localOptions = currentNodeId
    ? listLocalReferenceOptions(scopedDefinition, currentNodeId, contexts)
    : [];

  return dedupeReferenceOptions([...localOptions, ...options]);
}

export function listNodeOutputSchemaOptions(
  definition: MicroflowDefinition,
  node: MicroflowNode | null | undefined
): MicroflowContextVariable[] {
  if (!node) {
    return [];
  }

  return listMicroflowContextVariables(definition, node.id)
    .filter((context) => context.valueType === 'array' || context.valueType === 'object');
}

function listLocalReferenceOptions(
  definition: MicroflowDefinition,
  currentNodeId: string,
  contexts: MicroflowContextVariable[]
): MicroflowNodeReferenceOption[] {
  const options: MicroflowNodeReferenceOption[] = [];
  const currentNode = definition.nodes.find((node) => node.id === currentNodeId) ?? null;
  for (const loopNode of definition.nodes) {
    if (loopNode.type !== 'loop' || readConfigString(loopNode.config, 'bodyNodeId') !== currentNodeId) {
      continue;
    }

    const alias = readConfigString(loopNode.config, 'itemVariable') || 'item';
    appendLocalContextReferenceOptions(
      options,
      alias,
      `${loopNode.name || '循环'}.${alias}`,
      resolveLoopItemFields(definition, loopNode),
      'object'
    );
  }

  if (currentNode?.type === 'loop') {
    const alias = readConfigString(currentNode.config, 'itemVariable') || 'item';
    appendLocalContextReferenceOptions(
      options,
      alias,
      `${currentNode.name || '循环'}.${alias}`,
      resolveLoopItemFields(definition, currentNode),
      'object'
    );
  }

  const compositeRowFields = currentNode ? resolveCompositeRowFields(definition, currentNode, contexts) : [];
  if (compositeRowFields.length > 0) {
    appendLocalContextReferenceOptions(options, 'currentRow', '当前子行', compositeRowFields, 'object');
    appendLocalContextReferenceOptions(options, 'row', '当前子行', compositeRowFields, 'object');
    appendLocalContextReferenceOptions(options, 'item', '当前子行', compositeRowFields, 'object');
  }

  return options;
}

function appendLocalContextReferenceOptions(
  options: MicroflowNodeReferenceOption[],
  source: string,
  label: string,
  fields: MicroflowDomainField[],
  valueType: MicroflowVariableValueType
) {
  const normalizedFields = dedupeFields(fields);
  options.push({
    description: `${label} / ${valueType}`,
    expression: buildLocalExpression(source, '', valueType),
    id: `local:${source}:self`,
    isField: false,
    label,
    sourceKind: 'node',
    sourceVariableCode: source,
    valueType
  });

  for (const field of normalizedFields) {
    const fieldCode = field.fieldCode.trim();
    if (!fieldCode) {
      continue;
    }

    const fieldType = normalizeVariableValueType(field.dataType);
    options.push({
      description: `${label} · ${field.fieldName || fieldCode}`,
      expression: buildLocalExpression(source, fieldCode, fieldType),
      field: cloneMicroflowField(field),
      id: `local:${source}:field:${fieldCode}`,
      isField: true,
      label: `${label}.${fieldCode}`,
      sourceKind: 'node',
      sourceVariableCode: source,
      valueType: fieldType
    });
  }
}

function resolveCompositeRowFields(
  definition: MicroflowDefinition,
  node: MicroflowNode,
  contexts: MicroflowContextVariable[]
): MicroflowDomainField[] {
  const children = Array.isArray(node.config.children)
    ? node.config.children.filter(isRecord)
    : [];
  const fields: MicroflowDomainField[] = [];
  for (const child of children) {
    fields.push(...resolveExpressionFields(definition, child.rowsExpression, contexts));
  }

  return dedupeFields(fields);
}

function resolveExpressionFields(
  definition: MicroflowDefinition,
  expression: unknown,
  contexts: MicroflowContextVariable[]
): MicroflowDomainField[] {
  if (!isRecord(expression)) {
    return [];
  }

  if (expression.kind === 'literal' && Array.isArray(expression.value)) {
    return inferFieldsFromConstantArray(expression.value);
  }

  if (expression.kind !== 'ref' || !expression.ref) {
    return [];
  }

  const reference = expression.ref as { outputKey?: string | null; variableId?: string | null };
  const rootCode = reference.outputKey || reference.variableId || '';
  if (!rootCode) {
    return [];
  }

  const context = contexts.find((item) => item.variableCode.toLowerCase() === rootCode.toLowerCase());
  if (context?.fields.length) {
    return context.fields.map(cloneMicroflowField);
  }

  const schema = resolveVariableSchema(definition, rootCode);
  return schema ? normalizeVariableFields(schema) : [];
}

function inferFieldsFromConstantArray(value: unknown[]): MicroflowDomainField[] {
  const firstRecord = value.find(isRecord);
  if (!firstRecord) {
    return [];
  }

  return Object.entries(firstRecord).map(([key, fieldValue]) => ({
    dataType: inferValueType(fieldValue),
    fieldCode: key,
    fieldName: key,
    required: false,
    expression: null,
    visible: true,
    writable: true
  }));
}

function inferValueType(value: unknown): MicroflowVariableValueType {
  if (Array.isArray(value)) {
    return 'array';
  }

  if (value instanceof Date) {
    return 'datetime';
  }

  if (value !== null && typeof value === 'object') {
    return 'object';
  }

  if (typeof value === 'number') {
    return 'number';
  }

  if (typeof value === 'boolean') {
    return 'boolean';
  }

  return 'string';
}

export function getNodeConfigSummary(definition: MicroflowDefinition, node: MicroflowNode): string {
  if (node.type === 'return') {
    return getReturnOutputSummary(definition, node);
  }

  if (node.type === 'start') {
    return '开始';
  }

  if (node.type === 'end') {
    return '结束';
  }

  const references = listNodeInputReferenceOptions(definition, node.id);
  const fieldReferenceCount = references.filter((item) => item.isField).length;
  const outputSchema = readNodeOutputSchema(definition, node);
  const outputText = outputSchema
    ? `输出 ${outputSchema.variableCode}${outputSchema.fields.length > 0 ? `/${outputSchema.fields.length} 字段` : ''}`
    : '未配置输出';
  return `${fieldReferenceCount > 0 ? `可引用 ${fieldReferenceCount} 字段` : '无上游字段'} · ${outputText}`;
}

export function readNodeOutputSchema(
  definition: MicroflowDefinition,
  node: MicroflowNode
): MicroflowReturnOutputSchema | null {
  const configured = readConfiguredOutputSchema(node.config.outputSchema);
  const targetVariable = readNodeOutputVariableCode(node, configured);
  if (!targetVariable) {
    return null;
  }

  const variableSchema = resolveVariableSchema(definition, targetVariable);
  const valueType = normalizeVariableValueType(configured?.valueType ?? variableSchema?.valueType ?? inferNodeTargetValueType(node));
  return {
    arrayExpression: configured?.arrayExpression ? cloneExpression(configured.arrayExpression) : null,
    fields: resolveNodeContextFields(definition, node, targetVariable, variableSchema, configured),
    valueType,
    variableCode: targetVariable,
    variableName: configured?.variableName || variableSchema?.variableName || node.name || targetVariable
  };
}

export function readReturnOutputSchema(
  _definition: MicroflowDefinition,
  node: MicroflowNode
): MicroflowReturnOutputSchema | null {
  const configured = readConfiguredOutputSchema(node.config.outputSchema);
  return configured;
}

export function applyReturnOutputSchema(
  definition: MicroflowDefinition,
  nodeId: string,
  schema: MicroflowReturnOutputSchema
): MicroflowDefinition {
  const normalizedSchema = normalizeReturnOutputSchema(schema);
  const nextNodes = definition.nodes.map((node) => node.id === nodeId
    ? {
        ...node,
        config: {
          ...node.config,
          outputSchema: normalizedSchema
        }
      }
    : node);

  const existingIndex = definition.outputs.findIndex((output) =>
    output.variableCode.trim().toLowerCase() === normalizedSchema.variableCode.toLowerCase()
  );
  const nextOutput: MicroflowVariable = {
    defaultValue: createOutputDefaultValue(normalizedSchema.valueType),
    fields: normalizedSchema.fields.map(cloneMicroflowField),
    schemaObjectCode: null,
    valueType: normalizedSchema.valueType,
    variableCode: normalizedSchema.variableCode,
    variableName: normalizedSchema.variableName
  };
  const nextOutputs = existingIndex >= 0
    ? definition.outputs.map((output, index) => index === existingIndex
      ? {
          ...output,
          fields: normalizedSchema.fields.map(cloneMicroflowField),
          valueType: normalizedSchema.valueType,
          variableCode: normalizedSchema.variableCode,
          variableName: normalizedSchema.variableName || output.variableName || normalizedSchema.variableCode
        }
      : output)
    : [...definition.outputs, nextOutput];

  return {
    ...definition,
    nodes: nextNodes,
    outputs: nextOutputs
  };
}

export function getReturnOutputSummary(definition: MicroflowDefinition, node: MicroflowNode): string {
  const schema = readReturnOutputSchema(definition, node);
  if (!schema) {
    return '返回配置错误 · 未配置结构';
  }

  if (schema.fields.length === 0) {
    return `返回 ${schema.variableCode} · 配置错误: 未配置字段`;
  }

  return `返回 ${schema.variableCode} · ${schema.fields.length} 字段`;
}

export function validateReturnOutputSchema(definition: MicroflowDefinition, node: MicroflowNode): MicroflowReturnValidationIssue[] {
  if (node.type !== 'return') {
    return [];
  }

  const schema = readReturnOutputSchema(definition, node);
  if (!schema) {
    return [{ message: 'Return 节点未配置返回结构', path: 'outputSchema', severity: 'error' }];
  }

  const issues: MicroflowReturnValidationIssue[] = [];
  if (!schema.variableCode.trim()) {
    issues.push({ message: '返回变量编码不能为空', path: 'outputSchema.variableCode', severity: 'error' });
  }

  const sourceMode = schema.sourceMode ?? (schema.sqlScript ? 'sqlScript' : 'fields');
  if (schema.fields.length === 0) {
    issues.push({ message: '至少配置一个返回字段', path: 'outputSchema.fields', severity: 'error' });
  }

  if (sourceMode === 'sqlScript') {
    if (!schema.sqlScript) {
      issues.push({ message: 'SQL 脚本模式缺少脚本配置', path: 'outputSchema.sqlScript', severity: 'error' });
    } else {
      validateSqlScriptSchema(schema.sqlScript, issues);
    }
  }

  const fieldCodes = new Set<string>();
  schema.fields.forEach((field, index) => {
    const fieldCode = field.fieldCode.trim();
    if (!fieldCode) {
      issues.push({ message: `第 ${index + 1} 个返回字段编码不能为空`, path: `outputSchema.fields[${index}].fieldCode`, severity: 'error' });
      return;
    }

    const normalizedCode = fieldCode.toLowerCase();
    if (fieldCodes.has(normalizedCode)) {
      issues.push({ message: `返回字段编码重复: ${fieldCode}`, path: `outputSchema.fields[${index}].fieldCode`, severity: 'error' });
    }
    fieldCodes.add(normalizedCode);

    if (!field.fieldName.trim()) {
      issues.push({ message: `字段 ${fieldCode} 显示名称不能为空`, path: `outputSchema.fields[${index}].fieldName`, severity: 'error' });
    }

    if (!field.dataType.trim()) {
      issues.push({ message: `字段 ${fieldCode} 数据类型不能为空`, path: `outputSchema.fields[${index}].dataType`, severity: 'error' });
    }

    if (!hasExpression(field.expression)) {
      issues.push({ message: `字段 ${fieldCode} 缺少来源表达式`, path: `outputSchema.fields[${index}].expression`, severity: 'error' });
      return;
    }

    if (
      sourceMode === 'sqlScript' &&
      (field.expression?.kind !== 'ref' || field.expression.ref?.sourceType !== 'sqlResult')
    ) {
      issues.push({
        message: `SQL 脚本模式字段 ${fieldCode} 只能绑定 SQL 结果字段`,
        path: `outputSchema.fields[${index}].expression.ref.sourceType`,
        severity: 'error'
      });
    }

    const dataType = normalizeVariableValueType(field.dataType);
    const expressionType = normalizeVariableValueType(field.expression?.dataType);
    if (dataType !== expressionType && !['string', 'object'].includes(expressionType)) {
      issues.push({
        message: `字段 ${fieldCode} 类型为 ${dataType}，来源表达式类型为 ${expressionType}`,
        path: `outputSchema.fields[${index}].expression.dataType`,
        severity: 'warning'
      });
    }
  });

  return issues;
}

function validateSqlScriptSchema(
  sqlScript: MicroflowSqlScript,
  issues: MicroflowReturnValidationIssue[]
) {
  if (!sqlScript.script.trim()) {
    issues.push({ message: 'SQL 脚本不能为空', path: 'outputSchema.sqlScript.script', severity: 'error' });
  } else {
    if (!/\breturn\s+(select|@|json\b)/i.test(sqlScript.script)) {
      issues.push({ message: 'SQL 脚本必须包含 RETURN SELECT、RETURN @变量 或 RETURN JSON', path: 'outputSchema.sqlScript.script', severity: 'error' });
    }

    const stripped = stripSqlStringLiterals(sqlScript.script);
    if (/\b(alter|attach|call|detach|exec|execute|grant|merge|pragma|replace|revoke|vacuum)\b/i.test(stripped)) {
      issues.push({ message: 'SQL 脚本包含危险语句', path: 'outputSchema.sqlScript.script', severity: 'error' });
    }

    for (const statement of stripped.split(';')) {
      const trimmed = statement.trim();
      const isCreateOrDrop = /^\s*(create|drop)\b/i.test(trimmed);
      const allowedTempDdl = /^\s*create\s+(temp|temporary)\s+table\b/i.test(trimmed) ||
        /^\s*drop\s+(temp|temporary)\s+table\b/i.test(trimmed) ||
        /^\s*drop\s+table\s+if\s+exists\s+temp\.[A-Za-z_][A-Za-z0-9_]*\s*$/i.test(trimmed);
      if (isCreateOrDrop && !allowedTempDdl) {
        issues.push({ message: 'SQL 脚本只允许临时表 DDL，不能操作永久数据库对象', path: 'outputSchema.sqlScript.script', severity: 'error' });
      }
    }
  }

  const parameterNames = new Set<string>();
  sqlScript.parameters.forEach((parameter, index) => {
      const name = parameter.name.trim().replace(/^@+/, '');
      if (!name) {
      issues.push({ message: `第 ${index + 1} 个 SQL 参数名不能为空`, path: `outputSchema.sqlScript.parameters[${index}].name`, severity: 'error' });
      } else if (parameterNames.has(name.toLowerCase())) {
      issues.push({ message: `SQL 参数名重复: ${name}`, path: `outputSchema.sqlScript.parameters[${index}].name`, severity: 'error' });
      }
      parameterNames.add(name.toLowerCase());

      if (!hasExpression(parameter.expression)) {
      issues.push({ message: `SQL 参数 ${name || index + 1} 缺少变量来源`, path: `outputSchema.sqlScript.parameters[${index}].expression`, severity: 'error' });
      }
    });

  const localNames = new Set<string>();
  sqlScript.localVariables.forEach((variable, index) => {
    const name = variable.name.trim().replace(/^@+/, '');
    if (!name) {
      issues.push({ message: `第 ${index + 1} 个局部变量名不能为空`, path: `outputSchema.sqlScript.localVariables[${index}].name`, severity: 'error' });
    } else if (localNames.has(name.toLowerCase()) || parameterNames.has(name.toLowerCase())) {
      issues.push({ message: `SQL 脚本变量名重复: ${name}`, path: `outputSchema.sqlScript.localVariables[${index}].name`, severity: 'error' });
    }
    localNames.add(name.toLowerCase());
  });
}

function stripSqlStringLiterals(value: string): string {
  return value.replace(/'(?:''|[^'])*'/g, "''");
}

export function buildReturnResultPathOptions(definition: MicroflowDefinition | null): string[] {
  if (!definition) {
    return [];
  }

  const paths: string[] = [];
  for (const node of definition.nodes) {
    if (node.type !== 'return') {
      continue;
    }

    const schema = readReturnOutputSchema(definition, node);
    if (schema?.variableCode) {
      paths.push(`variables.${schema.variableCode}`);
    }
  }

  for (const output of definition.outputs) {
    const variableCode = output.variableCode.trim();
    if (variableCode) {
      paths.push(`variables.${variableCode}`);
    }
  }

  return Array.from(new Set(paths));
}

function appendVariableContexts(
  contexts: MicroflowContextVariable[],
  variables: MicroflowVariable[],
  sourceKind: Exclude<MicroflowContextVariableSource, 'node'>,
  excludedVariableCodes = new Set<string>()
) {
  for (const variable of variables) {
    const variableCode = variable.variableCode.trim();
    if (!variableCode) {
      continue;
    }

    if (excludedVariableCodes.has(variableCode.toLowerCase())) {
      continue;
    }

    const valueType = normalizeVariableValueType(variable.valueType);
    contexts.push({
      expression: buildVariableExpression(variableCode, valueType),
      fields: normalizeVariableFields(variable),
      id: `${sourceKind}:${variableCode}`,
      sourceKind,
      sourceLabel: `${sourceKind === 'input' ? 'inputs' : 'variables'}.${variableCode}`,
      valueType,
      variableCode,
      variableName: variable.variableName || variableCode
    });
  }
}

function dedupeContextVariables(contexts: MicroflowContextVariable[]): MicroflowContextVariable[] {
  const seen = new Set<string>();
  return contexts.filter((context) => {
    const key = context.variableCode.trim().toLowerCase();
    if (!key || seen.has(key)) {
      return false;
    }

    seen.add(key);
    return true;
  });
}

function resolveVariableSchema(definition: MicroflowDefinition, variableCode: string): MicroflowVariable | null {
  const normalizedCode = variableCode.trim().toLowerCase();
  if (!normalizedCode) {
    return null;
  }

  const candidates = [...definition.outputs, ...definition.variables, ...definition.inputs];
  return candidates.find((variable) => variable.variableCode.trim().toLowerCase() === normalizedCode) ?? null;
}

function resolveNodeContextFields(
  definition: MicroflowDefinition,
  node: MicroflowNode,
  variableCode: string,
  schema: MicroflowVariable | null,
  configuredOutputSchema?: MicroflowReturnOutputSchema | null
): MicroflowDomainField[] {
  const nodeOutputSchema = configuredOutputSchema ?? readConfiguredOutputSchema(node.config.outputSchema);
  const nodeOutputVariableCode = readNodeOutputVariableCode(node, null);
  if (
    nodeOutputSchema
    && (
      nodeOutputSchema.variableCode.toLowerCase() === variableCode.toLowerCase()
      || nodeOutputVariableCode.toLowerCase() === variableCode.toLowerCase()
    )
  ) {
    return nodeOutputSchema.fields.map(cloneMicroflowField);
  }

  const schemaFields = schema ? normalizeVariableFields(schema) : [];
  if (schemaFields.length > 0) {
    return schemaFields;
  }

  if (node.type === 'loop') {
    return resolveLoopItemFields(definition, node);
  }

  const domainObject = findNodeDomainObject(definition.domainObjects, node);
  return domainObject?.fields.map(cloneMicroflowField) ?? [];
}

function readNodeOutputVariableCode(node: MicroflowNode, configuredOutputSchema?: MicroflowReturnOutputSchema | null): string {
  if (node.type === 'return' || node.type === 'end' || node.type === 'decision' || node.type === 'globalVariables') {
    return '';
  }

  if (node.type === 'loop') {
    return readConfigString(node.config, 'itemVariable') || (configuredOutputSchema?.variableCode ?? '');
  }

  if (node.type === 'setVariable') {
    return getVariableRootCode(readConfigString(node.config, 'variableCode') || (configuredOutputSchema?.variableCode ?? ''));
  }

  return readConfigString(node.config, 'targetVariable')
    || readConfigString(node.config, 'variableCode')
    || configuredOutputSchema?.variableCode
    || '';
}

function resolveLoopItemFields(definition: MicroflowDefinition, node: MicroflowNode): MicroflowDomainField[] {
  const expressionVariableCode = readExpressionVariableCode(node.config.collectionExpression);
  if (!expressionVariableCode) {
    return [];
  }

  const upstreamContext = listMicroflowContextVariables(definition, node.id)
    .find((context) => context.variableCode.toLowerCase() === expressionVariableCode.toLowerCase());
  if (upstreamContext?.fields.length) {
    return upstreamContext.fields.map(cloneMicroflowField);
  }

  const variableSchema = resolveVariableSchema(definition, expressionVariableCode);
  return variableSchema ? normalizeVariableFields(variableSchema) : [];
}

function findNodeDomainObject(domainObjects: MicroflowDomainObject[], node: MicroflowNode): MicroflowDomainObject | null {
  const modelCode = readConfigString(node.config, 'modelCode') || readConfigString(node.config, 'rootModelCode');
  if (!modelCode) {
    return null;
  }

  return domainObjects.find((domainObject) =>
    [domainObject.objectCode, domainObject.modelCode].some((code) => code && code.toLowerCase() === modelCode.toLowerCase())
  ) ?? null;
}

function resolveUpstreamNodeIds(definition: MicroflowDefinition, currentNodeId?: string | null): Set<string> {
  const result = new Set<string>();
  if (!currentNodeId) {
    return result;
  }

  const pending = [currentNodeId];
  while (pending.length > 0) {
    const targetNodeId = pending.shift();
    if (!targetNodeId) {
      continue;
    }

    for (const edge of definition.edges) {
      if (edge.targetNodeId !== targetNodeId || result.has(edge.sourceNodeId)) {
        continue;
      }

      result.add(edge.sourceNodeId);
      pending.push(edge.sourceNodeId);
    }
  }

  return result;
}

function inferNodeTargetValueType(node: MicroflowNode): MicroflowVariableValueType {
  if (node.type === 'detail' || node.type === 'compositeDetail' || node.type === 'create' || node.type === 'change' || node.type === 'setVariable') {
    return 'object';
  }

  if (node.type === 'delete' || node.type === 'compositeDelete') {
    return 'object';
  }

  return 'array';
}

function normalizeReturnOutputSchema(schema: MicroflowReturnOutputSchema): MicroflowReturnOutputSchema {
  const variableCode = schema.variableCode.trim();
  const sourceMode = schema.sourceMode ?? (schema.sqlScript ? 'sqlScript' : 'fields');
  return {
    arrayExpression: sourceMode === 'fields' && schema.arrayExpression ? cloneExpression(schema.arrayExpression) : null,
    fields: schema.fields
      .map(cloneMicroflowField),
    sourceMode,
    sqlScript: sourceMode === 'sqlScript' ? normalizeSqlScript(schema.sqlScript) : null,
    valueType: normalizeVariableValueType(schema.valueType),
    variableCode,
    variableName: schema.variableName.trim() || variableCode
  };
}

function readConfiguredOutputSchema(value: unknown): MicroflowReturnOutputSchema | null {
  if (!isRecord(value)) {
    return null;
  }

  const variableCode = String(value.variableCode ?? '').trim();
  if (!variableCode) {
    return null;
  }

  const fields = Array.isArray(value.fields)
    ? value.fields.filter(isRecord).map((field) => cloneMicroflowField(field as unknown as MicroflowDomainField))
    : [];
  return {
    arrayExpression: isRecord(value.arrayExpression)
      ? cloneExpression(value.arrayExpression as unknown as MicroflowValueExpression)
      : null,
    fields,
    sourceMode: readSourceMode(value.sourceMode),
    sqlScript: readSqlScript(value.sqlScript),
    valueType: normalizeVariableValueType(String(value.valueType ?? 'array')),
    variableCode,
    variableName: String(value.variableName ?? variableCode).trim() || variableCode
  };
}

function readSourceMode(value: unknown): MicroflowReturnOutputSchema['sourceMode'] {
  return value === 'fields' || value === 'sqlScript'
    ? value
    : undefined;
}

function readSqlScript(value: unknown): MicroflowSqlScript | null {
  if (!isRecord(value)) {
    return null;
  }

  const parameters: MicroflowSqlScriptParameter[] = Array.isArray(value.parameters)
    ? value.parameters.filter(isRecord).map((parameter) => ({
        dataType: normalizeVariableValueType(String(parameter.dataType ?? 'string')),
        expression: isRecord(parameter.expression)
          ? cloneExpression(parameter.expression as unknown as MicroflowValueExpression)
          : null,
        name: String(parameter.name ?? '').trim()
      }))
    : [];
  const localVariables = Array.isArray(value.localVariables)
    ? value.localVariables.filter(isRecord).map((variable) => ({
        dataType: normalizeVariableValueType(String(variable.dataType ?? 'string')),
        initializer: isRecord(variable.initializer)
          ? cloneExpression(variable.initializer as unknown as MicroflowValueExpression)
          : null,
        name: String(variable.name ?? '').trim()
      }))
    : [];
  const resultShape = isRecord(value.resultShape)
    ? {
        fields: Array.isArray(value.resultShape.fields)
          ? value.resultShape.fields.filter(isRecord).map((field) => cloneMicroflowField(field as unknown as MicroflowDomainField))
          : [],
        valueType: normalizeVariableValueType(String(value.resultShape.valueType ?? value.valueType ?? 'array'))
      }
    : { fields: [], valueType: 'array' as const };
  return {
    dataSourceId: String(value.dataSourceId ?? '').trim(),
    localVariables,
    maxRows: Number.isFinite(Number(value.maxRows)) ? Number(value.maxRows) : 50,
    parameters,
    resultShape,
    script: String(value.script ?? '')
  };
}

function normalizeSqlScript(sqlScript: MicroflowSqlScript | null | undefined): MicroflowSqlScript | null {
  if (!sqlScript) {
    return null;
  }

  const hasValue = sqlScript.script.trim() ||
    sqlScript.parameters.length > 0 ||
    sqlScript.localVariables.length > 0 ||
    sqlScript.resultShape.fields.length > 0;
  if (!hasValue) {
    return null;
  }

  return {
    dataSourceId: '',
    localVariables: sqlScript.localVariables.map((variable) => ({
      dataType: normalizeVariableValueType(variable.dataType),
      initializer: variable.initializer ? cloneExpression(variable.initializer) : null,
      name: variable.name.trim().replace(/^@+/, '')
    })),
    maxRows: Math.max(1, Math.min(200, Number(sqlScript.maxRows ?? 50) || 50)),
    parameters: sqlScript.parameters.map((parameter) => ({
      dataType: normalizeVariableValueType(parameter.dataType),
      expression: parameter.expression ? cloneExpression(parameter.expression) : null,
      name: parameter.name.trim().replace(/^@+/, '')
    })),
    resultShape: {
      fields: sqlScript.resultShape.fields.map(cloneMicroflowField),
      valueType: normalizeVariableValueType(sqlScript.resultShape.valueType)
    },
    script: sqlScript.script.trim()
  };
}

function readExpressionVariableCode(value: unknown): string {
  if (!isRecord(value)) {
    return '';
  }

  if (value.kind !== 'ref' || !isRecord(value.ref)) {
    return '';
  }

  const reference = value.ref as { outputKey?: unknown; variableId?: unknown };
  return String(reference.outputKey ?? reference.variableId ?? '').trim();
}

function buildVariableExpression(variableCode: string, valueType: MicroflowVariableValueType): MicroflowValueExpression {
  return {
    dataType: valueType,
    kind: 'ref',
    ref: {
      dataType: valueType,
      fieldPath: [],
      label: variableCode,
      outputKey: variableCode,
      sourceType: 'global',
      variableId: variableCode
    }
  };
}

function buildVariableFieldExpression(
  variableCode: string,
  fieldPath: string[],
  valueType: MicroflowVariableValueType
): MicroflowValueExpression {
  return {
    dataType: valueType,
    kind: 'ref',
    ref: {
      dataType: valueType,
      fieldPath: fieldPath.filter(Boolean),
      label: [variableCode, ...fieldPath.filter(Boolean)].join('.'),
      outputKey: variableCode,
      sourceType: 'global',
      variableId: variableCode
    }
  };
}

function buildLocalExpression(source: string, path: string, valueType: MicroflowVariableValueType): MicroflowValueExpression {
  return {
    dataType: valueType,
    kind: 'ref',
    ref: {
      dataType: valueType,
      fieldPath: path ? path.split('.').filter(Boolean) : [],
      label: path ? `${source}.${path}` : source,
      outputKey: source,
      sourceType: 'loopItem',
      variableId: source
    }
  };
}

function cloneExpression(expression: MicroflowValueExpression): MicroflowValueExpression {
  return JSON.parse(JSON.stringify(expression)) as MicroflowValueExpression;
}

function hasExpression(expression: MicroflowValueExpression | null | undefined): boolean {
  if (!expression) {
    return false;
  }

  const kind = String(expression.kind ?? '').trim();
  if (!kind) {
    return false;
  }

  if (kind === 'literal') {
    return expression.value !== undefined;
  }

  if (kind === 'ref') {
    return Boolean(expression.ref);
  }

  if (kind === 'function') {
    return Boolean(expression.functionId);
  }

  if (kind === 'object') {
    return Boolean(expression.properties && Object.keys(expression.properties).length > 0);
  }

  if (kind === 'array' || kind === 'template') {
    return Boolean(expression.items?.length);
  }

  return false;
}

function dedupeFields(fields: MicroflowDomainField[]): MicroflowDomainField[] {
  const seen = new Set<string>();
  const result: MicroflowDomainField[] = [];
  for (const field of fields) {
    const fieldCode = field.fieldCode.trim();
    if (!fieldCode || seen.has(fieldCode.toLowerCase())) {
      continue;
    }

    seen.add(fieldCode.toLowerCase());
    result.push(cloneMicroflowField(field));
  }

  return result;
}

function dedupeReferenceOptions(options: MicroflowNodeReferenceOption[]): MicroflowNodeReferenceOption[] {
  const seen = new Set<string>();
  return options.filter((option) => {
    const ref = option.expression.ref;
    const key = `${ref?.sourceType ?? option.expression.kind}:${ref?.variableId ?? ''}:${ref?.outputKey ?? ''}:${ref?.fieldPath?.join('.') ?? ''}:${option.isField ? 'field' : 'self'}`;
    if (seen.has(key)) {
      return false;
    }

    seen.add(key);
    return true;
  });
}

function createOutputDefaultValue(valueType: MicroflowVariableValueType): unknown {
  if (valueType === 'array') {
    return [];
  }

  if (valueType === 'object' || valueType === 'json') {
    return {};
  }

  return '';
}

function readConfigString(config: Record<string, unknown>, key: string): string {
  return String(config[key] ?? '').trim();
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value && typeof value === 'object' && !Array.isArray(value));
}
