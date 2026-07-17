import type { ResourceRef } from '../document/ResourceRef';
import { validateExpressionGraph } from '../expression/expressionGraph';
import type { DesignerConversionStep, DesignerValueType, DesignerVariableExpression } from '../expression/expressionTypes';

import { createConversionPipeline } from './conversionPipeline';

export type { BindingDocument, DesignerExpressionHelper, DesignerExpressionOption, DesignerValueType, DesignerVariableExpression } from '../expression/expressionTypes';
export { resourceIdFor } from '../expression/expressionTypes';

export type BindingCompatibility = 'exact' | 'safe' | 'lossy' | 'incompatible';

export interface StableResourceReference {
  id: string;
  label: string;
  path: string;
  resourceType: string;
  source: string;
  valueType: DesignerValueType;
  writable: boolean;
  expression: DesignerVariableExpression;
}

export function resourceReferenceFor(resource: StableResourceReference, expectedType: DesignerValueType): ResourceRef {
  const conversion = createConversionPipeline(resource.valueType, expectedType);
  return {
    conversionPipeline: conversion.steps,
    displayName: resource.label,
    expectedType,
    resourceId: resource.id,
    resourceType: resource.resourceType,
    valueType: resource.valueType
  };
}

export interface BindingValidation {
  compatibility: BindingCompatibility;
  conversion: BindingConversionStep[];
  valid: boolean;
  reason: string;
}

export type BindingConversionStep = DesignerConversionStep;

export interface PropertyBindingState {
  compatibility: BindingCompatibility;
  conversion: BindingConversionStep[];
  expression: DesignerVariableExpression;
  resourceId: string;
}

export function expressionValueType(expression: DesignerVariableExpression): DesignerValueType {
  return expression.graph?.root?.valueType ?? expression.expectedType ?? 'json';
}

export function validateBindingExpression(expression: DesignerVariableExpression, expectedType: DesignerValueType): BindingValidation {
  const from = expressionValueType(expression);
  const conversion = createConversionPipeline(from, expectedType);
  const graphErrors = expression.graph ? validateExpressionGraph(expression.graph, expectedType) : [];
  const pipelineMatches = !expression.conversionPipeline || samePipeline(expression.conversionPipeline, conversion.steps);
  const valid = conversion.valid && pipelineMatches && graphErrors.length === 0;
  return {
    compatibility: conversion.compatibility,
    conversion: conversion.steps,
    reason: graphErrors[0] ?? (pipelineMatches ? conversion.reason : '绑定转换链与目标类型不一致'),
    valid
  };
}

function samePipeline(left: readonly BindingConversionStep[], right: readonly BindingConversionStep[]): boolean {
  return left.length === right.length && left.every((step, index) => step.from === right[index]?.from && step.name === right[index]?.name && step.to === right[index]?.to);
}
