import { defaultExpressionFallback, normalizeValueType } from './expressionModel';
import { resourceIdFor } from './expressionTypes';
import type { BindingDocument, DesignerExpressionOption, DesignerValueType, DesignerVariableExpression } from './expressionTypes';

export function buildResourceOptions(document?: BindingDocument | null): DesignerExpressionOption[] {
  const options = [
    ...runtimeOptions(document),
    ...pageParameterOptions(document),
    ...variableOptions(document),
    ...formFieldOptions(document),
    ...microflowOptions(document),
    ...bindingOptions(document)
  ];
  return dedupeOptions(options);
}

export function resourceOptionToExpression(option: DesignerExpressionOption): DesignerVariableExpression {
  return {
    expectedType: option.valueType,
    fallback: defaultExpressionFallback(option.valueType),
    resourceId: option.id,
    resourceType: option.source
  };
}

function runtimeOptions(document?: BindingDocument | null): DesignerExpressionOption[] {
  const pageCode = String(document?.runtimeContext?.pageCode ?? '').trim();
  return [
    createOption('system', 'currentUser', 'Current user', 'json', 'runtime', 'Runtime context', false),
    createOption('currentRow', '', 'Current row', 'object', 'runtime', 'Runtime context', false),
    createOption('page', 'pageCode', pageCode ? `Page ${pageCode}` : 'Page code', 'string', 'runtime', 'Runtime context', false)
  ];
}

function pageParameterOptions(document?: BindingDocument | null): DesignerExpressionOption[] {
  const parameters = asRecords(document?.pageParameters);
  return parameters.flatMap((parameter) => {
    const code = String(parameter.code ?? '').trim();
    if (!code) return [];
    const output = parameter.direction === 'output';
    return [createOption('page', `${output ? 'outputs' : 'inputs'}.${code}`, String(parameter.name ?? code), normalizeValueType(parameter.valueType), 'page-parameters', 'Page parameters', output, String(parameter.direction ?? 'input'))];
  });
}

function variableOptions(document?: BindingDocument | null): DesignerExpressionOption[] {
  return asRecords(document?.variables).flatMap((variable) => {
    const id = String(variable.id ?? '').trim();
    if (!id) return [];
    const source = sourceFromVariable(variable.source);
    return [createOption(source, id, String(variable.name ?? id), normalizeValueType(variable.valueType), 'variables', 'Page variables', source === 'variables' || source === 'form', String(variable.source ?? ''))];
  });
}

function formFieldOptions(document?: BindingDocument | null): DesignerExpressionOption[] {
  return asRecords(document?.elements ? Object.values(document.elements) : []).flatMap((element) => {
    const dataBinding = isRecord(element.bindings) ? element.bindings.data : undefined;
    const field = dataBinding && typeof dataBinding === 'object' ? String((dataBinding as Record<string, unknown>).field ?? '').trim() : '';
    if (!field) return [];
    const elementType = typeof element.type === 'string' ? element.type : undefined;
    return [createOption('form', field, typeof element.name === 'string' ? element.name : field, inferElementValueType(elementType), 'form-fields', 'Form fields', true, `${elementType ?? 'component'} / ${String(element.id ?? field)}`)];
  });
}

function isRecord(value: unknown): value is Record<string, unknown> { return Boolean(value) && typeof value === 'object' && !Array.isArray(value); }

function microflowOptions(document?: BindingDocument | null): DesignerExpressionOption[] {
  return asRecords(document?.pageMicroflows).flatMap((microflow) => {
    const alias = String(microflow.alias ?? '').trim();
    if (!alias) return [];
    const label = String(microflow.flowName ?? alias);
    const groupId = `microflow:${alias}`;
    const groupName = `Microflow · ${alias}`;
    const mappings = asRecords(microflow.outputMappings);
    const base = [
      createOption('microflow', `${alias}.data`, `${label} data`, inferMicroflowType(alias, mappings), groupId, groupName, false, String(microflow.flowCode ?? '')),
      createOption('microflow', `${alias}.loading`, `${label} loading`, 'boolean', groupId, groupName, false),
      createOption('microflow', `${alias}.error`, `${label} error`, 'string', groupId, groupName, false),
      createOption('microflow', `${alias}.trace`, `${label} trace`, 'array', groupId, groupName, false),
      createOption('microflow', `${alias}.variables`, `${label} variables`, 'object', groupId, groupName, false),
      createOption('microflow', `${alias}.version`, `${label} version`, 'number', groupId, groupName, false)
    ];
    const outputs = mappings.flatMap((mapping) => {
      const path = `${alias}.${String(mapping.outputVariable ?? 'data')}`;
      const type = normalizeValueType(mapping.valueType);
      const fields = asRecords(mapping.fields).map((field) => createOption('microflow', `${path}.${String(field.fieldCode ?? '')}`, String(field.fieldName ?? field.fieldCode ?? ''), normalizeValueType(field.valueType), groupId, groupName, false, String(microflow.flowCode ?? ''), field.modelCode == null ? null : String(field.modelCode)));
      return [createOption('microflow', path, `${String(mapping.outputVariable ?? 'data')} output`, type, groupId, groupName, false, String(microflow.flowCode ?? '')), ...fields];
    });
    return [...base, ...outputs];
  });
}

function bindingOptions(document?: BindingDocument | null): DesignerExpressionOption[] {
  return [
    ...asRecords(document?.apiBindings).map((binding) => createOption('api', String(binding.id ?? ''), String(binding.name ?? binding.id ?? ''), 'json', 'api', 'API responses', false, String(binding.type ?? ''))),
    ...asRecords(document?.workflowBindings).map((binding) => createOption('workflow', String(binding.id ?? ''), String(binding.name ?? binding.id ?? ''), 'json', 'workflow', 'Workflow values', false, String(binding.type ?? '')))
  ].filter((option) => option.path);
}

function createOption(source: string, path: string, label: string, valueType: DesignerValueType, groupId: string, groupName: string, writable: boolean, description?: string, modelCode?: string | null): DesignerExpressionOption {
  const sourceName = source.replace(/([a-z])([A-Z])/g, '$1 $2');
  return { description, fullPath: path ? `${source}.${path}` : source, groupId, groupName, id: resourceIdFor(source, path, modelCode), label, modelCode: modelCode ?? null, owner: description, path, source, sourceName, valueType, writable };
}

function dedupeOptions(options: DesignerExpressionOption[]): DesignerExpressionOption[] {
  const seen = new Set<string>();
  return options.filter((option) => {
    const key = `${option.source}:${option.modelCode ?? ''}:${option.path}`;
    if (seen.has(key)) return false;
    seen.add(key);
    return true;
  });
}

function sourceFromVariable(source: unknown): string {
  if (source === 'currentRow') return 'currentRow';
  if (source === 'formField') return 'form';
  if (source === 'workflow') return 'workflow';
  if (source === 'apiResponse') return 'api';
  if (source === 'system') return 'system';
  return 'variables';
}

function inferElementValueType(type?: string): DesignerValueType {
  if (type === 'input.number') return 'number';
  if (type === 'input.date') return 'date';
  if (type === 'select.checkbox') return 'array';
  if (type === 'media.fileUpload' || type === 'media.imageUpload' || type === 'media.signature') return 'json';
  return 'string';
}

function inferMicroflowType(alias: string, mappings: Array<Record<string, unknown>>): DesignerValueType {
  const root = mappings.find((mapping) => String(mapping.writeTo ?? '').endsWith(`${alias}.data`) || String(mapping.outputVariable ?? 'data') === 'data');
  return normalizeValueType(root?.valueType ?? 'json');
}

function asRecords(value: unknown): Array<Record<string, unknown>> {
  return Array.isArray(value) ? value.filter((item): item is Record<string, unknown> => Boolean(item) && typeof item === 'object') : [];
}
