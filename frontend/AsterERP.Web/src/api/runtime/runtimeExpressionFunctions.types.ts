export interface RuntimeExpressionFunctionParameterDto {
  dataType: string;
  defaultValue?: unknown;
  description: string;
  label: string;
  name: string;
  required: boolean;
}

export interface RuntimeExpressionFunctionDefinitionDto {
  canonicalName: string;
  description: string;
  deterministic: boolean;
  disabledReason: string;
  examples: string[];
  functionName: string;
  label: string;
  moduleKey: string;
  moduleName: string;
  namespace: string;
  parameters: RuntimeExpressionFunctionParameterDto[];
  qualifiedName: string;
  requiresInput: boolean;
  returnType: string;
  sqlEnabled: boolean;
}

export interface RuntimeExpressionFunctionCatalogResponse {
  functions: RuntimeExpressionFunctionDefinitionDto[];
  scope: string;
}

export type RuntimeExpressionFunctionScope = 'all' | 'microflowSqlScript' | string;
