import type { ApplicationDataCenterObjectUpsertRequest } from '../../../../api/application-data-center/applicationDataCenter.types';

import type { ConfigFieldSchema, ConfigFormSchema, ConfigNormalizeOptions, ConfigParseResult } from './configFormTypes';

const sensitiveNamePattern = /(password|secret|secretkey|accesskey|token|apikey|clientsecret|privatekey)/i;

export function parseJsonObject(value?: string | null): ConfigParseResult {
  if (!value?.trim()) {
    return { value: {} };
  }

  try {
    const parsed = JSON.parse(value) as unknown;
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
      return { error: 'JSON 必须是对象', value: {} };
    }

    return { value: parsed as Record<string, unknown> };
  } catch (error) {
    return { error: error instanceof Error ? error.message : 'JSON 格式不正确', value: {} };
  }
}

export function stringifyJsonObject(value: Record<string, unknown>): string {
  return JSON.stringify(value);
}

export function resolveConfigValue(form: ApplicationDataCenterObjectUpsertRequest, field: ConfigFieldSchema): unknown {
  const source = parseJsonObject(field.target === 'secret' ? form.secretConfigJson : form.configJson).value;
  return source[field.name] ?? field.defaultValue ?? defaultValueForField(field);
}

export function updateConfigField(
  form: ApplicationDataCenterObjectUpsertRequest,
  field: ConfigFieldSchema,
  value: unknown
): ApplicationDataCenterObjectUpsertRequest {
  const key = field.target === 'secret' ? 'secretConfigJson' : 'configJson';
  const parsed = parseJsonObject(form[key]).value;
  const next = { ...parsed };

  if (value === undefined || value === '') {
    delete next[field.name];
  } else {
    next[field.name] = value;
  }

  const nextJson = Object.keys(next).length > 0 ? stringifyJsonObject(next) : key === 'secretConfigJson' ? '' : '{}';
  return { ...form, [key]: nextJson };
}

export function normalizeConfigRequest(
  form: ApplicationDataCenterObjectUpsertRequest,
  options: ConfigNormalizeOptions
): ApplicationDataCenterObjectUpsertRequest {
  const config = parseJsonObject(form.configJson).value;
  const trimmedSecret = form.secretConfigJson?.trim();
  let secretConfigJson: string | null = null;

  if (form.secretConfigJson === null || form.secretConfigJson === undefined) {
    secretConfigJson = null;
  } else if (trimmedSecret) {
    secretConfigJson = stringifyJsonObject(parseJsonObject(trimmedSecret).value);
  } else {
    secretConfigJson = options.isEdit ? '' : null;
  }

  return {
    ...form,
    configJson: stringifyJsonObject(config),
    endpoint: emptyToNull(form.endpoint),
    environment: emptyToNull(form.environment),
    objectCode: form.objectCode.trim(),
    objectName: form.objectName.trim(),
    objectType: form.objectType.trim(),
    ownerUserId: emptyToNull(form.ownerUserId),
    remark: emptyToNull(form.remark),
    secretConfigJson
  };
}

export function buildDefaultConfigJson(schema?: ConfigFormSchema | null): string {
  if (!schema) {
    return '{}';
  }

  const result: Record<string, unknown> = {};
  for (const field of schema.sections.flatMap((section) => section.fields)) {
    if (field.target === 'secret') {
      continue;
    }

    if (field.defaultValue !== undefined) {
      result[field.name] = field.defaultValue;
    }
  }

  return stringifyJsonObject(result);
}

export function hasExistingSecret(publicConfigJson?: string | null): boolean {
  const parsed = parseJsonObject(publicConfigJson).value;
  return parsed.hasSecret === true || parsed.masked === true;
}

export function isSensitiveField(field: ConfigFieldSchema): boolean {
  return field.sensitive === true || field.target === 'secret' || sensitiveNamePattern.test(field.name);
}

export function summarizeConfig(schema: ConfigFormSchema | null, configJson?: string | null): Array<{ label: string; value: string }> {
  if (!schema) {
    return [];
  }

  const config = parseJsonObject(configJson).value;
  return schema.sections
    .flatMap((section) => section.fields)
    .filter((field) => field.target !== 'secret' && !isSensitiveField(field))
    .map((field) => ({ label: field.label, value: formatSummaryValue(config[field.name] ?? field.defaultValue) }))
    .filter((item) => item.value !== '-')
    .slice(0, 8);
}

function defaultValueForField(field: ConfigFieldSchema): unknown {
  if (field.component === 'switch') {
    return false;
  }

  if (field.component === 'number') {
    return '';
  }

  if (field.component === 'keyValueList' || field.component === 'mappingList' || field.component === 'fieldList' || field.component === 'codeRuleSegments') {
    return [];
  }

  return '';
}

function emptyToNull(value?: string | null): string | null {
  const text = value?.trim() ?? '';
  return text.length > 0 ? text : null;
}

function formatSummaryValue(value: unknown): string {
  if (value === null || value === undefined || value === '') {
    return '-';
  }

  if (typeof value === 'boolean') {
    return value ? '是' : '否';
  }

  if (Array.isArray(value)) {
    return `${value.length} 项`;
  }

  if (typeof value === 'object') {
    return '已配置';
  }

  return String(value);
}
