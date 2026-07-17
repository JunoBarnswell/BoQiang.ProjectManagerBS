export type DesignerValueType = 'array' | 'boolean' | 'date' | 'json' | 'number' | 'object' | 'string';

export interface DesignerConversionStep {
  from: DesignerValueType;
  name: string;
  to: DesignerValueType;
}

export function resourceIdFor(source: string, path: string, modelCode?: string | null): string {
  return [source, modelCode ?? '', path || '*'].map((value) => value.trim()).filter(Boolean).join(':');
}

/** Converts the pre-ResourceRef double-colon format to the canonical identity format. */
export function normalizeResourceId(resourceId: string, resourceType?: string | null): string {
  const raw = resourceId.trim();
  if (!raw.includes('::')) return raw;
  const parts = raw.split('::').map((part) => part.trim()).filter(Boolean);
  const source = resourceType?.trim() || parts.shift() || '';
  if (parts[0] === source) parts.shift();
  const path = parts.pop() || '*';
  return resourceIdFor(source, path, parts.length > 0 ? parts.join(':') : null);
}

export function resourceTypeForId(resourceId: string): string {
  return normalizeResourceId(resourceId).split(':', 1)[0]?.trim() ?? '';
}

export type DesignerVariableSource =
  | 'api'
  | 'component'
  | 'constant'
  | 'currentRow'
  | 'form'
  | 'microflow'
  | 'page'
  | 'system'
  | 'tableRow'
  | 'variables'
  | 'workflow'
  | string;

export interface DesignerExpressionHelper {
  args: Record<string, unknown>;
  name: string;
}

export interface DesignerVariableExpression {
  graph?: import('./expressionGraph').ExpressionGraph;
  conversionPipeline?: DesignerConversionStep[];
  expectedType?: DesignerValueType | null;
  fallback?: unknown;
  /** Editor-only one-time import metadata. It is never emitted by the resource catalog or persisted AST. */
  helpers?: DesignerExpressionHelper[];
  invalidReason?: string | null;
  modelCode?: string | null;
  path?: string | null;
  rawPathPreview?: string | null;
  resourceId?: string;
  resourceType?: DesignerVariableSource;
  source?: DesignerVariableSource;
  value?: unknown;
}

export interface DesignerExpressionOption {
  description?: string;
  fullPath?: string;
  groupId: string;
  groupName: string;
  id: string;
  label: string;
  modelCode?: string | null;
  owner?: string;
  path: string;
  source: DesignerVariableSource;
  sourceName?: string;
  valueType: DesignerValueType;
  writable: boolean;
}

export interface BindingDocument {
  apiBindings?: unknown;
  elements?: unknown;
  pageMicroflows?: unknown;
  pageParameters?: unknown;
  runtimeContext?: Record<string, unknown>;
  variables?: unknown;
  workflowBindings?: unknown;
}
