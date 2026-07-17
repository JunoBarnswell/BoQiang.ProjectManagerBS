import type { DesignerValueType } from './expressionTypes';

const valueTypes = new Set<DesignerValueType>(['array', 'boolean', 'date', 'json', 'number', 'object', 'string']);

export function normalizeValueType(value: unknown): DesignerValueType {
  const normalized = String(value ?? '').trim().toLowerCase();
  if (valueTypes.has(normalized as DesignerValueType)) return normalized as DesignerValueType;
  if (normalized.includes('list') || normalized.includes('collection')) return 'array';
  if (normalized.includes('bool')) return 'boolean';
  if (normalized.includes('date') || normalized.includes('time')) return 'date';
  if (normalized.includes('number') || normalized.includes('int') || normalized.includes('decimal') || normalized.includes('money')) return 'number';
  if (normalized.includes('json')) return 'json';
  if (normalized.includes('object') || normalized.includes('map') || normalized.includes('dict')) return 'object';
  return 'string';
}

export function defaultExpressionFallback(valueType: DesignerValueType): unknown {
  if (valueType === 'array') return [];
  if (valueType === 'object' || valueType === 'json') return {};
  if (valueType === 'number') return 0;
  if (valueType === 'boolean') return false;
  return '';
}
