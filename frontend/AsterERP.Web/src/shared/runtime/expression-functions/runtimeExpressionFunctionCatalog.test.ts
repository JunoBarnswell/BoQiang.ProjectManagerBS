import { describe, expect, it } from 'vitest';

import type { RuntimeExpressionFunctionCatalogResponse, RuntimeExpressionFunctionDefinitionDto } from '../../../api/runtime/runtimeExpressionFunctions.types';

import {
  buildRuntimeExpressionFunctionInsertText,
  buildRuntimeExpressionFunctionSignature,
  buildRuntimeExpressionFunctionSnippet,
  findRuntimeExpressionFunction,
  groupRuntimeExpressionFunctions,
  listRuntimeExpressionFunctionNamespaces
} from './runtimeExpressionFunctionCatalog';

describe('runtimeExpressionFunctionCatalog', () => {
  it('groups and sorts functions by backend catalog metadata', () => {
    const catalog = createCatalog([
      createFunction({ functionName: 'trim', moduleKey: 'string', moduleName: '字符串', namespace: 'StringFns' }),
      createFunction({ functionName: 'clamp', moduleKey: 'number', moduleName: '数值', namespace: 'NumberFns', parameters: [
        { dataType: 'number', description: '', label: '最小值', name: 'min', required: true },
        { dataType: 'number', description: '', label: '最大值', name: 'max', required: true }
      ] }),
      createFunction({ functionName: 'toInt', moduleKey: 'number', moduleName: '数值', namespace: 'NumberFns' })
    ]);

    expect(groupRuntimeExpressionFunctions(catalog).map((group) => [group.namespace, group.functions.map((fn) => fn.functionName)])).toEqual([
      ['StringFns', ['trim']],
      ['NumberFns', ['clamp', 'toInt']]
    ]);
    expect(listRuntimeExpressionFunctionNamespaces(catalog)).toEqual(['StringFns', 'NumberFns']);
  });

  it('builds signatures, snippets, insert text, and normalized lookup', () => {
    const clamp = createFunction({
      canonicalName: 'clamp',
      functionName: 'clamp',
      namespace: 'NumberFns',
      parameters: [
        { dataType: 'number', description: '', label: '最小值', name: 'min', required: true },
        { dataType: 'number', description: '', label: '最大值', name: 'max', required: true }
      ]
    });
    const catalog = createCatalog([clamp]);

    expect(buildRuntimeExpressionFunctionSignature(clamp)).toBe('NumberFns.clamp(value, min, max)');
    expect(buildRuntimeExpressionFunctionSnippet(clamp)).toBe('NumberFns.clamp(${1:value}, ${2:min}, ${3:max})');
    expect(buildRuntimeExpressionFunctionSnippet(clamp, { includeNamespace: false })).toBe('clamp(${1:value}, ${2:min}, ${3:max})');
    expect(buildRuntimeExpressionFunctionInsertText(clamp)).toBe('NumberFns.clamp(@amount, 0, 0)');
    expect(findRuntimeExpressionFunction(catalog, 'number_fns clamp')).toBeNull();
    expect(findRuntimeExpressionFunction(catalog, 'NumberFns.clamp')).toBe(clamp);
  });
});

function createCatalog(functions: RuntimeExpressionFunctionDefinitionDto[]): RuntimeExpressionFunctionCatalogResponse {
  return {
    functions,
    scope: 'microflowSqlScript'
  };
}

function createFunction(patch: Partial<RuntimeExpressionFunctionDefinitionDto>): RuntimeExpressionFunctionDefinitionDto {
  const namespace = patch.namespace ?? 'StringFns';
  const functionName = patch.functionName ?? 'trim';
  return {
    canonicalName: patch.canonicalName ?? functionName.toLowerCase(),
    description: patch.description ?? `${functionName} description`,
    deterministic: patch.deterministic ?? true,
    disabledReason: patch.disabledReason ?? '',
    examples: patch.examples ?? [],
    functionName,
    label: patch.label ?? functionName,
    moduleKey: patch.moduleKey ?? 'string',
    moduleName: patch.moduleName ?? '字符串',
    namespace,
    parameters: patch.parameters ?? [],
    qualifiedName: patch.qualifiedName ?? `${namespace}.${functionName}`,
    requiresInput: patch.requiresInput ?? true,
    returnType: patch.returnType ?? 'string',
    sqlEnabled: patch.sqlEnabled ?? true
  };
}
