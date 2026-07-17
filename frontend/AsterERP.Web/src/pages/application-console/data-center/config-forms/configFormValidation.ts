import type { ApplicationDataCenterObjectUpsertRequest } from '../../../../api/application-data-center/applicationDataCenter.types';

import type { ConfigFieldError, ConfigFieldSchema, ConfigFormSchema } from './configFormTypes';
import { parseJsonObject, resolveConfigValue } from './configJsonCodec';

export function validateConfigForm(
  form: ApplicationDataCenterObjectUpsertRequest,
  schema: ConfigFormSchema | null
): ConfigFieldError[] {
  const errors: ConfigFieldError[] = [];

  if (!form.objectCode.trim()) {
    errors.push({ field: 'objectCode', message: '对象编码不能为空' });
  }

  if (!form.objectName.trim()) {
    errors.push({ field: 'objectName', message: '对象名称不能为空' });
  }

  if (!form.objectType.trim()) {
    errors.push({ field: 'objectType', message: '对象类型不能为空' });
  }

  const configParse = parseJsonObject(form.configJson);
  if (configParse.error) {
    errors.push({ field: 'configJson', message: `公开配置 JSON 格式不正确：${configParse.error}` });
  }

  if (form.secretConfigJson?.trim()) {
    const secretParse = parseJsonObject(form.secretConfigJson);
    if (secretParse.error) {
      errors.push({ field: 'secretConfigJson', message: `凭据 JSON 格式不正确：${secretParse.error}` });
    }
  }

  if (!schema) {
    return errors;
  }

  for (const field of schema.sections.flatMap((section) => section.fields)) {
    const value = resolveConfigValue(form, field);
    if (!isFieldVisible(field, form)) {
      continue;
    }

    if (field.required && isEmpty(value)) {
      errors.push({ field: field.name, message: `${field.label}不能为空` });
      continue;
    }

    for (const rule of field.validation ?? []) {
      if (rule.type === 'url' && !isEmpty(value) && !isUrl(String(value))) {
        errors.push({ field: field.name, message: rule.message ?? `${field.label}必须是合法 URL` });
      }

      if (rule.type === 'numberRange' && !isEmpty(value)) {
        const numberValue = Number(value);
        if (Number.isNaN(numberValue) || (rule.min !== undefined && numberValue < rule.min) || (rule.max !== undefined && numberValue > rule.max)) {
          errors.push({ field: field.name, message: rule.message ?? `${field.label}超出允许范围` });
        }
      }

      if (rule.type === 'jsonObject' && !isEmpty(value)) {
        const parsed = parseJsonObject(String(value));
        if (parsed.error) {
          errors.push({ field: field.name, message: rule.message ?? `${field.label}必须是合法 JSON 对象` });
        }
      }
    }
  }

  return errors;
}

export function firstValidationMessage(errors: ConfigFieldError[]): string | null {
  return errors[0]?.message ?? null;
}

function isFieldVisible(field: ConfigFieldSchema, form: ApplicationDataCenterObjectUpsertRequest): boolean {
  if (!field.visibleWhen) {
    return true;
  }

  const value = parseJsonObject(form.configJson).value[field.visibleWhen.field];
  if (field.visibleWhen.equals !== undefined) {
    return value === field.visibleWhen.equals;
  }

  if (field.visibleWhen.notEquals !== undefined) {
    return value !== field.visibleWhen.notEquals;
  }

  if (field.visibleWhen.includes) {
    return field.visibleWhen.includes.includes(value);
  }

  return true;
}

function isEmpty(value: unknown): boolean {
  if (value === null || value === undefined) {
    return true;
  }

  if (typeof value === 'string') {
    return value.trim().length === 0;
  }

  if (Array.isArray(value)) {
    return value.length === 0;
  }

  return false;
}

function isUrl(value: string): boolean {
  try {
    const parsed = new URL(value);
    return parsed.protocol === 'http:' || parsed.protocol === 'https:';
  } catch {
    return false;
  }
}
