import type {
  MicroflowDefinition,
  MicroflowDomainField,
  MicroflowDomainObject,
  MicroflowVariable,
  MicroflowValueExpression
} from '../../../../api/application-data-center/applicationDataCenter.types';
import { translateCurrentLiteral } from '../../../../core/i18n/I18nProvider';

export type MicroflowVariableValueType = 'array' | 'boolean' | 'date' | 'datetime' | 'json' | 'number' | 'object' | 'string';

export type MicroflowFieldInputValue = boolean | string;

export type MicroflowObjectInputValue = Record<string, MicroflowFieldInputValue>;

export type MicroflowVariableInputValue =
  | boolean
  | string
  | MicroflowObjectInputValue
  | MicroflowObjectInputValue[];

export interface MicroflowVariableValidationResult {
  errors: Record<string, string>;
  valid: boolean;
}

export const microflowVariableValueTypeOptions: Array<{ label: string; value: string }> = [
  { label: translateCurrentLiteral("文本"), value: 'string' },
  { label: translateCurrentLiteral("数字"), value: 'number' },
  { label: translateCurrentLiteral("布尔"), value: 'boolean' },
  { label: translateCurrentLiteral("日期"), value: 'date' },
  { label: translateCurrentLiteral("时间"), value: 'datetime' },
  { label: translateCurrentLiteral("对象"), value: 'object' },
  { label: translateCurrentLiteral("数组"), value: 'array' },
  { label: 'JSON', value: 'json' }
];

export const microflowFieldDataTypeOptions: Array<{ label: string; value: string }> = [
  { label: translateCurrentLiteral("文本"), value: 'string' },
  { label: translateCurrentLiteral("数字"), value: 'number' },
  { label: translateCurrentLiteral("布尔"), value: 'boolean' },
  { label: translateCurrentLiteral("日期"), value: 'date' },
  { label: translateCurrentLiteral("时间"), value: 'datetime' },
  { label: translateCurrentLiteral("对象"), value: 'object' },
  { label: translateCurrentLiteral("数组"), value: 'array' },
  { label: 'JSON', value: 'json' }
];

export function listPreviewInputVariables(definition: MicroflowDefinition | null): MicroflowVariable[] {
  if (!definition) {
    return [];
  }

  const outputSchemas = new Map<string, MicroflowVariable>();
  for (const output of definition.outputs) {
    const key = output.variableCode?.trim().toLowerCase();
    if (key) {
      outputSchemas.set(key, output);
    }
  }

  const result: MicroflowVariable[] = [];
  const indexes = new Map<string, number>();
  for (const variable of definition.inputs) {
    const key = variable.variableCode?.trim();
    if (!key) {
      continue;
    }

    const normalizedKey = key.toLowerCase();
    const mergedVariable = mergePreviewVariableSchema(variable, outputSchemas.get(normalizedKey));
    const existingIndex = indexes.get(normalizedKey);
    if (existingIndex === undefined) {
      indexes.set(normalizedKey, result.length);
      result.push(mergedVariable);
      continue;
    }

    result[existingIndex] = mergePreviewVariableSchema(result[existingIndex], mergedVariable);
  }

  return result;
}

export function normalizeVariableValueType(valueType: string | null | undefined): MicroflowVariableValueType {
  const normalized = String(valueType ?? '').trim().toLowerCase();
  if (normalized.includes('array') || normalized.includes('list') || normalized.includes('collection')) {
    return 'array';
  }

  if (normalized.includes('json')) {
    return 'json';
  }

  if (normalized.includes('object') || normalized.includes('map') || normalized.includes('dict')) {
    return 'object';
  }

  if (normalized.includes('bool') || normalized.includes('switch')) {
    return 'boolean';
  }

  if (
    normalized.includes('int') ||
    normalized.includes('decimal') ||
    normalized.includes('double') ||
    normalized.includes('float') ||
    normalized.includes('money') ||
    normalized.includes('number')
  ) {
    return 'number';
  }

  if (normalized.includes('datetime') || normalized.includes('time')) {
    return 'datetime';
  }

  if (normalized.includes('date')) {
    return 'date';
  }

  return 'string';
}

export function isStructuredVariable(variable: MicroflowVariable): boolean {
  const valueType = normalizeVariableValueType(variable.valueType);
  return valueType === 'array' || valueType === 'object' || valueType === 'json';
}

export function normalizeVariableFields(variable: MicroflowVariable): MicroflowDomainField[] {
  return (variable.fields ?? []).map(cloneMicroflowField);
}

export function cloneMicroflowField(field: MicroflowDomainField): MicroflowDomainField {
  return {
    dataType: field.dataType || 'string',
    displayHelpers: field.displayHelpers ?? [],
    fieldCode: field.fieldCode ?? '',
    fieldName: field.fieldName || field.fieldCode || '字段',
    queryHelpers: field.queryHelpers ?? [],
    readOnly: Boolean(field.readOnly),
    required: Boolean(field.required),
    expression: field.expression ? cloneExpression(field.expression) : null,
    visible: field.visible !== false,
    writable: field.writable !== false,
    writeHelpers: field.writeHelpers ?? []
  };
}

function mergePreviewVariableSchema(variable: MicroflowVariable, schema: MicroflowVariable | undefined): MicroflowVariable {
  if (!schema) {
    return variable;
  }

  const variableFields = variable.fields ?? [];
  const schemaFields = schema.fields ?? [];
  return {
    ...variable,
    fields: variableFields.length > 0 ? variableFields : schemaFields.map(cloneMicroflowField),
    schemaObjectCode: variable.schemaObjectCode ?? schema.schemaObjectCode ?? null,
    sourceNodeId: variable.sourceNodeId ?? schema.sourceNodeId ?? null,
    valueType: variable.valueType || schema.valueType,
    variableName: variable.variableName || schema.variableName
  };
}

export function createMicroflowVariableField(index: number): MicroflowDomainField {
  return {
    dataType: 'string',
    displayHelpers: [],
    fieldCode: `field_${index + 1}`,
    fieldName: '字段',
    queryHelpers: [],
    readOnly: false,
    required: false,
    expression: null,
    visible: true,
    writable: true,
    writeHelpers: []
  };
}

function cloneExpression(expression: MicroflowValueExpression): MicroflowValueExpression {
  return JSON.parse(JSON.stringify(expression)) as MicroflowValueExpression;
}

export function copyFieldsFromDomainObject(domainObject: MicroflowDomainObject | null | undefined): MicroflowDomainField[] {
  return (domainObject?.fields ?? []).map(cloneMicroflowField);
}

export function findDomainObjectByCode(
  domainObjects: MicroflowDomainObject[],
  objectCode: string | null | undefined
): MicroflowDomainObject | null {
  const normalized = String(objectCode ?? '').trim();
  if (!normalized) {
    return null;
  }

  return domainObjects.find((item) => item.objectCode === normalized) ?? null;
}

export function createInitialVariableValues(variables: MicroflowVariable[]): Record<string, MicroflowVariableInputValue> {
  return Object.fromEntries(
    variables
      .map((variable) => [variable.variableCode.trim(), createInitialVariableValue(variable)] as const)
      .filter(([key]) => key.length > 0)
  );
}

export function createInitialVariableValue(variable: MicroflowVariable): MicroflowVariableInputValue {
  const valueType = normalizeVariableValueType(variable.valueType);
  if (valueType === 'array') {
    return createInitialArrayValue(variable);
  }

  if (valueType === 'object') {
    return createInitialObjectValue(variable);
  }

  if (valueType === 'json') {
    return createJsonInputValue(variable.defaultValue);
  }

  return createScalarInputValue(valueType, variable.defaultValue);
}

export function createEmptyRow(fields: MicroflowDomainField[]): MicroflowObjectInputValue {
  return Object.fromEntries(
    fields
      .filter((field) => field.fieldCode.trim())
      .map((field) => [field.fieldCode, createScalarInputValue(normalizeVariableValueType(field.dataType), null)] as const)
  );
}

export function validateVariableValues(
  variables: MicroflowVariable[],
  values: Record<string, MicroflowVariableInputValue>
): MicroflowVariableValidationResult {
  const errors: Record<string, string> = {};
  for (const variable of variables) {
    const variableCode = variable.variableCode.trim();
    if (!variableCode) {
      continue;
    }

    const valueType = normalizeVariableValueType(variable.valueType);
    const fields = normalizeVariableFields(variable);
    const value = values[variableCode] ?? createInitialVariableValue(variable);
    if (valueType === 'array') {
      const rows = Array.isArray(value) ? value : [];
      rows.forEach((row, rowIndex) => validateObjectValue(variableCode, fields, row, errors, rowIndex));
      continue;
    }

    if (valueType === 'object') {
      validateObjectValue(variableCode, fields, isObjectInputValue(value) ? value : {}, errors);
      continue;
    }

    validateScalarValue(variableCode, null, valueType, value, errors);
  }

  return { errors, valid: Object.keys(errors).length === 0 };
}

export function serializeVariableValues(
  variables: MicroflowVariable[],
  values: Record<string, MicroflowVariableInputValue>
): Record<string, unknown> {
  const result: Record<string, unknown> = {};
  for (const variable of variables) {
    const variableCode = variable.variableCode.trim();
    if (!variableCode) {
      continue;
    }

    const valueType = normalizeVariableValueType(variable.valueType);
    const fields = normalizeVariableFields(variable);
    const value = values[variableCode] ?? createInitialVariableValue(variable);
    if (valueType === 'array') {
      result[variableCode] = Array.isArray(value)
        ? value.map((row) => serializeObjectValue(fields, row))
        : [];
      continue;
    }

    if (valueType === 'object') {
      result[variableCode] = serializeObjectValue(fields, isObjectInputValue(value) ? value : {});
      continue;
    }

    result[variableCode] = serializeScalarValue(valueType, value);
  }

  return result;
}

export function createErrorKey(variableCode: string, fieldCode?: string | null, rowIndex?: number): string {
  if (!fieldCode) {
    return variableCode;
  }

  return rowIndex === undefined
    ? `${variableCode}.${fieldCode}`
    : `${variableCode}.${rowIndex}.${fieldCode}`;
}

function createInitialArrayValue(variable: MicroflowVariable): MicroflowObjectInputValue[] {
  const fields = normalizeVariableFields(variable);
  if (!Array.isArray(variable.defaultValue)) {
    return [];
  }

  return variable.defaultValue
    .filter(isPlainRecord)
    .map((row) => createObjectInputValue(fields, row));
}

function createInitialObjectValue(variable: MicroflowVariable): MicroflowObjectInputValue {
  return createObjectInputValue(
    normalizeVariableFields(variable),
    isPlainRecord(variable.defaultValue) ? variable.defaultValue : {}
  );
}

function createObjectInputValue(
  fields: MicroflowDomainField[],
  source: Record<string, unknown>
): MicroflowObjectInputValue {
  if (fields.length === 0) {
    return {};
  }

  return Object.fromEntries(
    fields
      .filter((field) => field.fieldCode.trim())
      .map((field) => [
        field.fieldCode,
        createScalarInputValue(normalizeVariableValueType(field.dataType), source[field.fieldCode])
      ] as const)
  );
}

function createScalarInputValue(valueType: MicroflowVariableValueType, value: unknown): MicroflowFieldInputValue {
  if (valueType === 'boolean') {
    return parseBoolean(value);
  }

  if (value === undefined || value === null) {
    return '';
  }

  if (valueType === 'json') {
    return createJsonInputValue(value);
  }

  return String(value);
}

function validateObjectValue(
  variableCode: string,
  fields: MicroflowDomainField[],
  value: MicroflowObjectInputValue,
  errors: Record<string, string>,
  rowIndex?: number
) {
  for (const field of fields) {
    const fieldCode = field.fieldCode.trim();
    if (!fieldCode) {
      continue;
    }

    validateScalarValue(
      variableCode,
      fieldCode,
      normalizeVariableValueType(field.dataType),
      value[fieldCode],
      errors,
      rowIndex,
      field.required
    );
  }
}

function validateScalarValue(
  variableCode: string,
  fieldCode: string | null,
  valueType: MicroflowVariableValueType,
  value: unknown,
  errors: Record<string, string>,
  rowIndex?: number,
  required = false
) {
  const key = createErrorKey(variableCode, fieldCode, rowIndex);
  const text = typeof value === 'boolean' ? String(value) : String(value ?? '').trim();
  if (required && !text) {
    errors[key] = '必填';
    return;
  }

  if (!text) {
    return;
  }

  if (valueType === 'number' && !Number.isFinite(Number(text))) {
    errors[key] = '必须是数字';
    return;
  }

  if ((valueType === 'date' || valueType === 'datetime') && Number.isNaN(Date.parse(text))) {
    errors[key] = '日期格式不正确';
    return;
  }

  if (valueType === 'json') {
    try {
      JSON.parse(text);
    } catch {
      errors[key] = 'JSON 格式不正确';
    }
  }
}

function serializeObjectValue(fields: MicroflowDomainField[], value: MicroflowObjectInputValue): Record<string, unknown> {
  if (fields.length === 0) {
    return {};
  }

  return Object.fromEntries(
    fields
      .filter((field) => field.fieldCode.trim())
      .map((field) => [
        field.fieldCode,
        serializeScalarValue(normalizeVariableValueType(field.dataType), value[field.fieldCode])
      ] as const)
  );
}

function serializeScalarValue(valueType: MicroflowVariableValueType, value: unknown): unknown {
  if (valueType === 'boolean') {
    return parseBoolean(value);
  }

  const text = String(value ?? '').trim();
  if (valueType === 'number') {
    return text ? Number(text) : null;
  }

  if (valueType === 'date' || valueType === 'datetime') {
    return text || null;
  }

  if (valueType === 'json') {
    return text ? parseJsonInputValue(text) : null;
  }

  return typeof value === 'string' ? value : text;
}

function createJsonInputValue(value: unknown): string {
  if (value === undefined || value === null || value === '') {
    return '';
  }

  return typeof value === 'string' ? value : JSON.stringify(value, null, 2);
}

function parseJsonInputValue(value: string): unknown {
  try {
    return JSON.parse(value);
  } catch {
    return value;
  }
}

function isObjectInputValue(value: MicroflowVariableInputValue): value is MicroflowObjectInputValue {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function isPlainRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function parseBoolean(value: unknown): boolean {
  if (typeof value === 'boolean') {
    return value;
  }

  const normalized = String(value ?? '').trim().toLowerCase();
  return normalized === 'true' || normalized === '1' || normalized === 'yes' || normalized === 'on';
}
