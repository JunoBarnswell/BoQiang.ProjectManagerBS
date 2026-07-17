import { evaluateExpressionValue, isCanonicalExpressionValue } from '../../api/runtime/expressionValue';

export interface RuntimeExpressionScope {
  api?: Record<string, unknown>;
  component?: Record<string, unknown>;
  currentRow?: Record<string, unknown> | null;
  form?: Record<string, unknown>;
  microflow?: Record<string, unknown>;
  model?: Record<string, unknown>;
  page?: Record<string, unknown>;
  system?: Record<string, unknown>;
  tableRow?: Record<string, unknown> | null;
  variables?: Record<string, unknown>;
  workflow?: Record<string, unknown>;
}

/**
 * The runtime accepts literal values and the versioned ExpressionValue AST.
 * Historical source/path/helpers objects and {{...}} templates are migration
 * input only and deliberately fail closed here.
 */
export function resolveRuntimeValue(value: unknown, scope: RuntimeExpressionScope): unknown {
  if (isCanonicalExpressionValue(value)) {
    return evaluateExpressionValue(value, (resourceId) => resolveResource(resourceId, scope));
  }

  if (typeof value === 'string' && value.includes('{{')) {
    throw new Error('Legacy {{...}} runtime templates are not supported. Migrate this value to ExpressionValue.');
  }
  if (isLegacyExpression(value)) {
    throw new Error('Legacy source/path/helpers runtime expressions are not supported. Migrate this value to ExpressionValue.');
  }

  return value;
}

function resolveResource(resourceId: string, scope: RuntimeExpressionScope): unknown {
  const [scopeName, ...pathSegments] = resourceId.split(':');
  if (!scopeName || pathSegments.length === 0) {
    throw new Error(`Expression resourceId is invalid: ${resourceId}`);
  }

  const root = sourceRoot(scopeName, scope);
  const path = pathSegments.join(':');
  if (path === '*') return root;
  const resolved = readRuntimePath(root, path);
  if (!resolved.found) throw new Error(`Expression resource is not registered: ${resourceId}`);
  return resolved.value;
}

function sourceRoot(source: string, scope: RuntimeExpressionScope): unknown {
  switch (source) {
    case 'api': return scope.api;
    case 'component': return scope.component;
    case 'currentRow':
    case 'tableRow': return scope.currentRow ?? scope.tableRow;
    case 'form': return scope.form;
    case 'model': return scope.model;
    case 'microflow': return scope.microflow;
    case 'page': return scope.page;
    case 'system': return scope.system;
    case 'variables': return scope.variables;
    case 'workflow': return scope.workflow;
    default: throw new Error(`Expression resource scope is not registered: ${source}`);
  }
}

function readRuntimePath(source: unknown, path: string): { found: boolean; value: unknown } {
  let current = source;
  for (const segment of path.split('.').filter(Boolean)) {
    if (!current || typeof current !== 'object' || !Object.prototype.hasOwnProperty.call(current, segment)) {
      return { found: false, value: undefined };
    }
    current = (current as Record<string, unknown>)[segment];
  }
  return { found: true, value: current };
}

function isLegacyExpression(value: unknown): boolean {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return false;
  const record = value as Record<string, unknown>;
  return 'source' in record || 'path' in record || 'helpers' in record || 'graph' in record || record.kind === 'expression';
}
