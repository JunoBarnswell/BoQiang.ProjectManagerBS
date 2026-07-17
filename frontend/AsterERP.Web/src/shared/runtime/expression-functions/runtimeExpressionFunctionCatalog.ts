import type {
  RuntimeExpressionFunctionCatalogResponse,
  RuntimeExpressionFunctionDefinitionDto
} from '../../../api/runtime/runtimeExpressionFunctions.types';

export interface RuntimeExpressionFunctionGroup {
  functions: RuntimeExpressionFunctionDefinitionDto[];
  moduleKey: string;
  moduleName: string;
  namespace: string;
}

export const runtimeExpressionFunctionNamespaces = [
  'StringFns',
  'NumberFns',
  'DateFns',
  'FormatFns',
  'JsonFns',
  'ObjectFns',
  'ArrayFns',
  'RegexFns',
  'UrlFns',
  'TypeFns',
  'RbacFns'
] as const;

export function groupRuntimeExpressionFunctions(
  catalog: RuntimeExpressionFunctionCatalogResponse | null | undefined
): RuntimeExpressionFunctionGroup[] {
  const groups = new Map<string, RuntimeExpressionFunctionGroup>();
  for (const fn of catalog?.functions ?? []) {
    const key = `${fn.moduleKey}:${fn.namespace}`;
    const existing = groups.get(key);
    if (existing) {
      existing.functions.push(fn);
      continue;
    }

    groups.set(key, {
      functions: [fn],
      moduleKey: fn.moduleKey,
      moduleName: fn.moduleName,
      namespace: fn.namespace
    });
  }

  return Array.from(groups.values()).map((group) => ({
    ...group,
    functions: [...group.functions].sort((left, right) => left.functionName.localeCompare(right.functionName))
  }));
}

export function findRuntimeExpressionFunction(
  catalog: RuntimeExpressionFunctionCatalogResponse | null | undefined,
  qualifiedName: string
): RuntimeExpressionFunctionDefinitionDto | null {
  const normalized = normalizeFunctionName(qualifiedName);
  return catalog?.functions.find((fn) => normalizeFunctionName(fn.qualifiedName) === normalized) ?? null;
}

export function buildRuntimeExpressionFunctionSignature(fn: RuntimeExpressionFunctionDefinitionDto): string {
  const args = [
    ...(fn.requiresInput ? ['value'] : []),
    ...fn.parameters.map((param) => param.required ? param.name : `${param.name}?`)
  ];
  return `${fn.qualifiedName}(${args.join(', ')})`;
}

export function buildRuntimeExpressionFunctionSnippet(
  fn: RuntimeExpressionFunctionDefinitionDto,
  options: { includeNamespace?: boolean } = {}
): string {
  const args = [
    ...(fn.requiresInput ? ['${1:value}'] : []),
    ...fn.parameters.map((param, index) => `\${${index + (fn.requiresInput ? 2 : 1)}:${param.name}}`)
  ];
  const name = options.includeNamespace === false ? fn.functionName : fn.qualifiedName;
  return `${name}(${args.join(', ')})`;
}

export function buildRuntimeExpressionFunctionInsertText(fn: RuntimeExpressionFunctionDefinitionDto): string {
  const args = [
    ...(fn.requiresInput ? [defaultInputPlaceholder(fn)] : []),
    ...fn.parameters.map(defaultParameterPlaceholder)
  ];
  return `${fn.qualifiedName}(${args.join(', ')})`;
}

export function listRuntimeExpressionFunctionNamespaces(
  catalog: RuntimeExpressionFunctionCatalogResponse | null | undefined
): string[] {
  const namespaces = new Set(catalog?.functions.map((fn) => fn.namespace).filter(Boolean));
  return runtimeExpressionFunctionNamespaces.filter((namespaceName) => namespaces.has(namespaceName));
}

export function normalizeFunctionName(value: string): string {
  return value.trim().replace(/[-_\s]/g, '').toLowerCase();
}

function defaultInputPlaceholder(fn: RuntimeExpressionFunctionDefinitionDto): string {
  if (fn.namespace === 'NumberFns') {
    return '@amount';
  }

  if (fn.namespace === 'DateFns') {
    return '@date';
  }

  if (fn.namespace === 'ArrayFns') {
    return '@list';
  }

  if (fn.namespace === 'JsonFns' || fn.namespace === 'ObjectFns') {
    return '@object';
  }

  return '@value';
}

function defaultParameterPlaceholder(param: { dataType: string; name: string }): string {
  if (param.name === 'field' || param.name === 'path' || param.name === 'fields') {
    return `'${param.name}'`;
  }

  if (param.name === 'separator') {
    return "','";
  }

  if (param.dataType === 'number') {
    return '0';
  }

  if (param.dataType === 'boolean') {
    return 'true';
  }

  return `'${param.name}'`;
}
