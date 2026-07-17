import { evaluateExpressionValue } from '../api/runtime/expressionValue';
import { isExpressionValue } from '../pages/application-console/development-center/low-code-studio/document/PropertyValue';
import type { ResourceRef } from '../pages/application-console/development-center/low-code-studio/document/ResourceRef';

import { RUNTIME_CAPABILITY_CONTRACT } from './runtime-contract/RuntimeCapabilityContract';
import type { RuntimeScopeStore } from './RuntimeScopeStore';
import { RUNTIME_SCOPE_NAMES, type RuntimeResourceBinding } from './RuntimeTypes';

export interface RuntimeBindingContext {
  componentValues?: Record<string, unknown>;
  form?: Record<string, unknown>;
  page?: Record<string, unknown>;
  resources?: Record<string, unknown>;
  resourceProviders?: ReadonlyMap<string, RuntimeResourceResolver>;
  scopeIds?: Partial<Record<(typeof RUNTIME_SCOPE_NAMES)[number], string>>;
  scopes: Record<string, Record<string, unknown>>;
  scopeStore?: RuntimeScopeStore;
  variables: Record<string, unknown>;
}

export interface RuntimeResourceProviderContext {
  binding?: RuntimeResourceBinding;
  context: RuntimeBindingContext;
  provider: string;
  resourceId: string;
  signal?: AbortSignal;
}

export type RuntimeResourceResolver = (input: RuntimeResourceProviderContext) => unknown | Promise<unknown>;

export interface ResolvedBinding {
  value: unknown;
  resourceId?: string;
  source: 'constant' | 'expression' | 'resource';
}

export class BindingResolver {
  public resolve(value: unknown, context: RuntimeBindingContext): ResolvedBinding {
    // A canonical resourceRef is also an ExpressionValue AST node. Resolve the
    // resource reference first so optional form/page values can fall back to
    // their current scope instead of being rejected as an unregistered AST
    // resource during the initial render.
    if (isResourceRef(value)) {
      const resource = resolveResource(value, context);
      return {
        resourceId: value.resourceId,
        source: 'resource',
        value: applyConversionPipeline(resource.found ? resource.value : value.fallback?.value, value.conversionPipeline)
      };
    }
    if (isExpressionValue(value)) {
      return {
        source: 'expression',
        value: evaluateExpressionValue(value, (resourceId) => {
          const resolved = resolveResource({ displayName: resourceId, resourceId, resourceType: resourceId.split(':', 1)[0], valueType: value.dataType, expectedType: value.dataType, conversionPipeline: [] }, context);
          if (!resolved.found) throw new Error(`Runtime resource is not registered: ${resourceId}`);
          return resolved.value;
        })
      };
    }
    return { source: 'constant', value };
  }

  public resolveRecord(values: Record<string, unknown>, context: RuntimeBindingContext): Record<string, unknown> {
    return Object.fromEntries(Object.entries(values).map(([key, value]) => [key, this.resolveNested(value, context)]));
  }

  private resolveNested(value: unknown, context: RuntimeBindingContext): unknown {
    if (isExpressionValue(value) || isResourceRef(value)) return this.resolve(value, context).value;
    if (Array.isArray(value)) return value.map((item) => this.resolveNested(item, context));
    if (isRecord(value)) return this.resolveRecord(value, context);
    return value;
  }
}

export async function loadRuntimeResourceContext(
  bindings: readonly RuntimeResourceBinding[],
  providers: ReadonlyMap<string, RuntimeResourceResolver>,
  context: RuntimeBindingContext = { scopes: {}, variables: {} },
  signal?: AbortSignal
): Promise<Record<string, unknown>> {
  const resources: Record<string, unknown> = {};
  for (const binding of bindings) {
    if (signal?.aborted) throw new DOMException('Runtime resource loading was cancelled.', 'AbortError');
    const providerKey = binding.provider?.trim();
    if (!providerKey) continue;
    const provider = providers.get(providerKey);
    if (!provider) throw new Error(`Runtime resource provider is not registered: ${providerKey}`);
    const value = await provider({ binding, context, provider: providerKey, resourceId: binding.resourceId, signal });
    if (signal?.aborted) throw new DOMException('Runtime resource loading was cancelled.', 'AbortError');
    if (isRecord(value)) Object.assign(resources, value);
    else resources[binding.resourceId] = value;
  }
  return resources;
}

function resolveResource(binding: ResourceRef & RuntimeBindingWithConversion, context: RuntimeBindingContext): { found: boolean; value: unknown } {
  const resourceId = binding.resourceId.trim();
  if (!resourceId) throw new Error('Runtime resource binding requires a non-empty resourceId.');
  if (context.resources && Object.prototype.hasOwnProperty.call(context.resources, resourceId)) return { found: true, value: context.resources[resourceId] };
  const providerName = binding.resourceType?.trim() || (resourceId.includes(':') ? resourceId.slice(0, resourceId.indexOf(':')) : resourceId);
  const provider = context.resourceProviders?.get(providerName) ?? context.resourceProviders?.get('*');
  if (provider) {
    const value = provider({ binding, context, provider: providerName, resourceId });
    if (value instanceof Promise) throw new Error(`Runtime resource provider must be loaded before synchronous binding resolution: ${providerName}`);
    if (value !== undefined) return { found: true, value };
  }
  return { found: false, value: undefined };
}

function isResourceRef(value: unknown): value is ResourceRef & RuntimeBindingWithConversion { return Boolean(value && typeof value === 'object' && 'resourceId' in value && typeof value.resourceId === 'string'); }
function isRecord(value: unknown): value is Record<string, unknown> { return Boolean(value && typeof value === 'object' && !Array.isArray(value)); }

interface RuntimeBindingWithConversion {
  conversionPipeline?: unknown;
}

interface RuntimeConversionStep {
  from: string;
  name: string;
  to: string;
}

function applyConversionPipeline(value: unknown, pipeline: unknown): unknown {
  if (pipeline === undefined) return value;
  if (!Array.isArray(pipeline)) throw new Error('Runtime binding conversionPipeline must be an array.');

  return pipeline.reduce<unknown>((current, step, index) => {
    if (!isRecord(step) || !isNonEmptyString(step.from) || !isNonEmptyString(step.name) || !isNonEmptyString(step.to)) {
      throw new Error(`Runtime binding conversion step ${index} must declare non-empty from, name, and to fields.`);
    }
    if (!RUNTIME_CAPABILITY_CONTRACT.converters.includes(step.name)) {
      throw new Error(`Runtime binding converter is not part of the canonical capability contract: ${step.name}`);
    }
    return applyConverter({ from: step.from, name: step.name, to: step.to }, current);
  }, value);
}

function applyConverter(step: RuntimeConversionStep, value: unknown): unknown {
  switch (step.name) {
    case 'arrayToJson':
      if (!Array.isArray(value)) throw new Error('arrayToJson requires an array value.');
      return JSON.stringify(value);
    case 'booleanToString':
      if (typeof value !== 'boolean') throw new Error('booleanToString requires a boolean value.');
      return String(value);
    case 'dateToIsoString':
      return toDate(value).toISOString();
    case 'jsonToArray': {
      const parsed = parseJson(value);
      if (!Array.isArray(parsed)) throw new Error('jsonToArray requires a JSON array value.');
      return parsed;
    }
    case 'jsonToObject': {
      const parsed = parseJson(value);
      if (!isRecord(parsed)) throw new Error('jsonToObject requires a JSON object value.');
      return parsed;
    }
    case 'numberToString':
      if (typeof value !== 'number' || !Number.isFinite(value)) throw new Error('numberToString requires a finite number value.');
      return String(value);
    case 'objectToJson':
      if (!isRecord(value)) throw new Error('objectToJson requires an object value.');
      return JSON.stringify(value);
    case 'stringToBoolean': {
      if (typeof value !== 'string') throw new Error('stringToBoolean requires a string value.');
      const normalized = value.trim().toLowerCase();
      if (normalized === 'true' || normalized === '1') return true;
      if (normalized === 'false' || normalized === '0') return false;
      throw new Error('stringToBoolean requires true, false, 1, or 0.');
    }
    case 'stringToDate':
      if (typeof value !== 'string' || !value.trim()) throw new Error('stringToDate requires a non-empty string value.');
      return toDate(value);
    case 'stringToNumber': {
      if (typeof value !== 'string' || !value.trim()) throw new Error('stringToNumber requires a non-empty string value.');
      const parsed = Number(value);
      if (!Number.isFinite(parsed)) throw new Error('stringToNumber requires a finite numeric string.');
      return parsed;
    }
    default:
      throw new Error(`Runtime binding converter is not implemented: ${step.name}`);
  }
}

function parseJson(value: unknown): unknown {
  if (typeof value !== 'string') return value;
  try {
    return JSON.parse(value) as unknown;
  } catch {
    throw new Error('Runtime binding JSON conversion received invalid JSON.');
  }
}

function toDate(value: unknown): Date {
  const date = value instanceof Date ? new Date(value.getTime()) : typeof value === 'string' ? new Date(value) : null;
  if (!date || Number.isNaN(date.getTime())) throw new Error('Runtime binding date conversion received an invalid date.');
  return date;
}

function isNonEmptyString(value: unknown): value is string { return typeof value === 'string' && value.trim().length > 0; }
