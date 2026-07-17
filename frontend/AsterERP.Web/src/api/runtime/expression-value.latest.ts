export type ExpressionDataType = 'array' | 'boolean' | 'date' | 'json' | 'number' | 'object' | 'string';

export type ExpressionKind =
  | 'literal'
  | 'resourceRef'
  | 'functionCall'
  | 'conversion'
  | 'condition'
  | 'logic'
  | 'object'
  | 'array'
  | 'template'
  | 'defaultValue';

export interface ExpressionConversionStep {
  from: ExpressionDataType;
  name: string;
  to: ExpressionDataType;
}

export interface ExpressionValue {
  version: 'latest';
  kind: ExpressionKind;
  dataType: ExpressionDataType;
  value?: unknown;
  resourceId?: string;
  functionId?: string;
  args?: ExpressionValue[];
  input?: ExpressionValue;
  when?: ExpressionValue;
  then?: ExpressionValue;
  otherwise?: ExpressionValue;
  operator?: 'and' | 'or' | 'not';
  properties?: Record<string, ExpressionValue>;
  items?: ExpressionValue[];
  pipeline?: ExpressionConversionStep[];
  fallback?: unknown;
  dependencies?: string[];
  canonicalHash?: string;
}

export const expressionValueVersion = 'latest' as const;

