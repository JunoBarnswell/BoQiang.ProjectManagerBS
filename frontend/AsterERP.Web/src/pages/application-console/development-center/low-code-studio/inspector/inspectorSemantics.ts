import type { StableResourceReference } from '../binding/bindingTypes';
import type { DesignerDocumentNode } from '../document/DesignerDocument';

import type { InspectorCondition, InspectorPropertyDescriptor } from './contract/InspectorPropertyDescriptor';

export interface InspectorValidationResult {
  readonly valid: boolean;
  readonly message?: string;
}

export function readInspectorPath(source: unknown, path: string): unknown {
  return path.split('.').filter(Boolean).reduce<unknown>((current, segment) => {
    if (!current || typeof current !== 'object' || Array.isArray(current)) return undefined;
    return (current as Record<string, unknown>)[segment];
  }, source);
}

export function matchesInspectorCondition(node: DesignerDocumentNode, condition?: InspectorCondition): boolean {
  if (!condition) return true;
  const current = readInspectorPath(node, condition.path);
  switch (condition.operator) {
    case 'equals': return valuesEqual(current, condition.value);
    case 'notEquals': return !valuesEqual(current, condition.value);
    case 'exists': return current !== undefined && current !== null;
    case 'truthy': return Boolean(current);
    case 'falsy': return !current;
  }
}

export function validateInspectorValue(value: unknown, descriptor: Pick<InspectorPropertyDescriptor, 'valueType' | 'validation' | 'options'>): InspectorValidationResult {
  const validation = descriptor.validation ?? { valueType: descriptor.valueType };
  if (value === undefined || value === null) {
    return validation.required ? { valid: false, message: 'value is required' } : { valid: true };
  }
  if (!matchesValueType(value, descriptor.valueType)) return { valid: false, message: `value must be ${descriptor.valueType}` };
  if (typeof value === 'number') {
    if (!Number.isFinite(value)) return { valid: false, message: 'value must be finite' };
    if (validation.integer && !Number.isInteger(value)) return { valid: false, message: 'value must be an integer' };
    if (validation.min !== undefined && value < validation.min) return { valid: false, message: `value must be at least ${validation.min}` };
    if (validation.max !== undefined && value > validation.max) return { valid: false, message: `value must be at most ${validation.max}` };
  }
  if (typeof value === 'string') {
    if (validation.minLength !== undefined && value.length < validation.minLength) return { valid: false, message: `value must contain at least ${validation.minLength} characters` };
    if (validation.maxLength !== undefined && value.length > validation.maxLength) return { valid: false, message: `value must contain at most ${validation.maxLength} characters` };
    if (validation.pattern) {
      try {
        if (!new RegExp(validation.pattern).test(value)) return { valid: false, message: 'value does not match the required pattern' };
      } catch {
        return { valid: false, message: 'validation pattern is invalid' };
      }
    }
  }
  if (descriptor.options && typeof value === 'string' && !descriptor.options.some((option) => option.value === value)) return { valid: false, message: 'value is not an available option' };
  return { valid: true };
}

export function isAcceptedInspectorSource(source: string | undefined, descriptor: Pick<InspectorPropertyDescriptor, 'bindable' | 'acceptedSources' | 'bindingPolicy'>): boolean {
  const policy = descriptor.bindingPolicy ?? { enabled: descriptor.bindable, acceptedSources: descriptor.acceptedSources };
  if (!policy.enabled) return false;
  if (!source) return false;
  const normalizedSource = normalizeSource(source);
  return policy.acceptedSources.some((candidate) => normalizeSource(candidate) === normalizedSource);
}

export function inspectorResourceSource(resource: Pick<StableResourceReference, 'resourceType' | 'source'>): string {
  return resource.resourceType || resource.source;
}

export function areInspectorBatchDescriptorsCompatible(left: InspectorPropertyDescriptor, right: InspectorPropertyDescriptor): boolean {
  return left.semanticId === right.semanticId
    && left.valueType === right.valueType
    && left.editor === right.editor
    && left.unit === right.unit
    && valuesEqual(left.validation, right.validation)
    && left.bindingPolicy.enabled === right.bindingPolicy.enabled
    && valuesEqual(left.bindingPolicy.acceptedSources, right.bindingPolicy.acceptedSources);
}

export function valuesEqual(left: unknown, right: unknown): boolean {
  if (Object.is(left, right)) return true;
  if (Array.isArray(left) && Array.isArray(right)) return left.length === right.length && left.every((item, index) => valuesEqual(item, right[index]));
  if (isRecord(left) && isRecord(right)) {
    const leftKeys = Object.keys(left);
    const rightKeys = Object.keys(right);
    return leftKeys.length === rightKeys.length && leftKeys.every((key) => Object.prototype.hasOwnProperty.call(right, key) && valuesEqual(left[key], right[key]));
  }
  return false;
}

function matchesValueType(value: unknown, valueType: InspectorPropertyDescriptor['valueType']): boolean {
  if (valueType === 'array') return Array.isArray(value);
  if (valueType === 'object') return isRecord(value);
  if (valueType === 'json') return isRecord(value) || Array.isArray(value);
  if (valueType === 'number') return typeof value === 'number';
  if (valueType === 'boolean') return typeof value === 'boolean';
  return typeof value === 'string';
}

function normalizeSource(source: string): string {
  const normalized = source.trim().toLowerCase();
  return normalized === 'variable' ? 'variables' : normalized;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === 'object' && !Array.isArray(value);
}
