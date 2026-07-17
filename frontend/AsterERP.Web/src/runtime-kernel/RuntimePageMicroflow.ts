import { executeRuntimeMicroflow } from '../api/application-data-center/applicationDataCenter.api';
import type { RuntimeArtifact, RuntimeArtifactPageMicroflow } from '../pages/application-console/development-center/low-code-studio/document/RuntimeArtifact';
import { resolveRuntimeValue, type RuntimeExpressionScope } from '../shared/runtime/runtimeExpression';

import { mergeRuntimePageMicroflowFormDefaults } from './RuntimePageMicroflowFormDefaults';

export interface RuntimePageMicroflowExecutionContext {
  formValues?: Record<string, unknown>;
  mergeVariables?: (values: Record<string, unknown>) => void;
  setFormValues?: (values: Record<string, unknown>) => void;
  scopes: Record<string, Record<string, unknown>>;
  signal?: AbortSignal;
}

export async function executeRuntimePageMicroflow(
  artifact: RuntimeArtifact,
  alias: string,
  context: RuntimePageMicroflowExecutionContext
): Promise<unknown> {
  const binding = artifact.pageMicroflows.find((item) => item.alias === alias);
  if (!binding || !binding.flowCode) throw new Error(`Unknown page microflow binding: ${alias}`);

  const scopes = context.scopes;
  const variables = scopes.variables ?? {};
  const expressionScope: RuntimeExpressionScope = {
    component: scopes.component,
    currentRow: asRecord(variables.currentRow) ?? asRecord(scopes.row) ?? asRecord(variables.row),
    form: context.formValues ?? scopes.form,
    microflow: asRecord(variables.microflows),
    page: scopes.page ?? variables,
    system: scopes.system,
    tableRow: asRecord(variables.currentRow) ?? asRecord(scopes.row) ?? asRecord(variables.row),
    variables
  };
  const inputVariables = resolveInputMappings(binding, expressionScope);
  const timeoutMs = typeof binding.timeoutMs === 'number' ? binding.timeoutMs : undefined;
  const response = await executeRuntimeMicroflow(binding.flowCode, {
    action: binding.action || 'query',
    bindingId: binding.bindingId || null,
    pageCode: typeof artifact.runtimeContext.pageCode === 'string' ? artifact.runtimeContext.pageCode : null,
    timeoutMs: timeoutMs ?? null,
    variables: { ...variables, ...inputVariables }
  }, { signal: context.signal, timeoutMs });

  const responseVariables = asRecord(response.data.variables) ?? {};
  let nextVariables = { ...variables, ...responseVariables };
  nextVariables = writePath(nextVariables, `microflows.${alias}`, {
    ...responseVariables,
    result: response.data.result
  });
  for (const mapping of binding.outputMappings ?? []) {
    const outputVariable = typeof mapping.outputVariable === 'string' ? mapping.outputVariable : '';
    const writeTo = typeof mapping.writeTo === 'string' ? mapping.writeTo : '';
    if (!outputVariable || !writeTo) continue;
    const output = responseVariables[outputVariable] ?? response.data.result;
    const value = typeof mapping.resultPath === 'string' && mapping.resultPath.trim()
      ? readPath(output, mapping.resultPath)
      : output;
    nextVariables = writePath(nextVariables, writeTo, value);
  }

  context.mergeVariables?.(nextVariables);
  if (context.formValues && context.setFormValues) {
    context.setFormValues(mergeRuntimePageMicroflowFormDefaults(artifact, context.formValues, nextVariables, { overwrite: true }));
  }
  return response.data.result;
}

function resolveInputMappings(
  binding: RuntimeArtifactPageMicroflow,
  scope: RuntimeExpressionScope
): Record<string, unknown> {
  const result: Record<string, unknown> = {};
  for (const mapping of binding.inputMappings ?? []) {
    const target = typeof mapping.targetVariable === 'string' ? mapping.targetVariable.trim() : '';
    if (!target) continue;
    try {
      if (Object.prototype.hasOwnProperty.call(mapping, 'sourceExpression')) {
        result[target] = resolveRuntimeValue(mapping.sourceExpression, scope);
        continue;
      }
      if (Object.prototype.hasOwnProperty.call(mapping, 'value')) {
        result[target] = resolveRuntimeValue(mapping.value, scope);
        continue;
      }
    } catch (error) {
      if (mapping.required === true) throw error;
      continue;
    }
    if (mapping.required === true) throw new Error(`Required page microflow input is not mapped: ${target}`);
  }
  return result;
}

function readPath(value: unknown, path: string): unknown {
  return path.split('.').filter(Boolean).reduce<unknown>((current, segment) => {
    if (Array.isArray(current)) {
      const index = Number(segment);
      return Number.isInteger(index) && index >= 0 ? current[index] : undefined;
    }
    if (!current || typeof current !== 'object') return undefined;
    return (current as Record<string, unknown>)[segment];
  }, value);
}

function writePath(values: Record<string, unknown>, path: string, value: unknown): Record<string, unknown> {
  const parts = path.split('.').filter(Boolean);
  if (parts.length === 0) return values;
  const result = { ...values };
  let cursor: Record<string, unknown> = result;
  parts.slice(0, -1).forEach((part) => {
    const next = cursor[part];
    cursor[part] = next && typeof next === 'object' && !Array.isArray(next) ? { ...(next as Record<string, unknown>) } : {};
    cursor = cursor[part] as Record<string, unknown>;
  });
  cursor[parts.at(-1)!] = value;
  return result;
}

function asRecord(value: unknown): Record<string, unknown> | undefined {
  return value && typeof value === 'object' && !Array.isArray(value) ? value as Record<string, unknown> : undefined;
}
