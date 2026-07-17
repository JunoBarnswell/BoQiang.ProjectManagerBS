import type { DesignerConversionStep } from '../expression/expressionTypes';

import type { DesignerValueKind, TypedValue } from './TypedValue';

export interface ResourceRef {
  conversionPipeline: DesignerConversionStep[];
  displayName: string;
  expectedType: DesignerValueKind;
  fallback?: TypedValue;
  resourceId: string;
  resourceType: string;
  valueType: DesignerValueKind;
}
