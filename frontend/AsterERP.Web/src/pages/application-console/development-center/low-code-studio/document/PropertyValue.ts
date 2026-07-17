import type { ExpressionValue } from '../../../../../api/runtime/expression-value.latest';
import { expressionValueFromGraph, isCanonicalExpressionValue } from '../../../../../api/runtime/expressionValue';
import type { DesignerValueType, DesignerVariableExpression } from '../expression/expressionTypes';

import type { ResourceRef } from './ResourceRef';

/** Literal values remain ordinary JSON values; reference forms are recognized before this branch. */
export type LiteralValue = unknown;

/** Values accepted in props/layout/style after the one-time historical migration. */
export type PropertyValue = LiteralValue | ResourceRef | ExpressionValue;

export function isResourceRef(value: unknown): value is ResourceRef {
  return isRecord(value)
    && !isCanonicalExpressionValue(value)
    && typeof value.resourceId === 'string'
    && value.resourceId.trim().length > 0;
}

export function isExpressionValue(value: unknown): value is ExpressionValue {
  return isCanonicalExpressionValue(value);
}

/** Converts the Inspector's transient graph to the only persisted expression contract. */
export function toExpressionValue(expression: DesignerVariableExpression, expectedType: DesignerValueType): ExpressionValue | null {
  if (!expression.graph) return null;
  return expressionValueFromGraph(expression.graph, expectedType, expression.fallback);
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === 'object' && !Array.isArray(value);
}
