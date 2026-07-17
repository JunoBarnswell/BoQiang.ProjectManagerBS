import type { ApplicationMappingCacheColumn, ApplicationMappingCacheParameter } from '../../../../api/application-data-center/applicationDataCenter.types';

export const MAPPING_CACHE_PARAMETER_TYPES = ['string', 'number', 'boolean', 'date', 'json'] as const;
export type MappingCacheParameterType = (typeof MAPPING_CACHE_PARAMETER_TYPES)[number];

export interface MappingCacheParameterValidationErrors {
  name?: string;
  columnResourceId?: string;
  dataType?: string;
  defaultValue?: string;
}

export function inferMappingCacheParameterType(dataType: string): MappingCacheParameterType {
  const value = dataType.trim().toLowerCase();
  if (value.includes('json')) return 'json';
  if (value.includes('bool') || value === 'bit') return 'boolean';
  if (value.includes('date') || value.includes('time') || value.includes('timestamp')) return 'date';
  if (value.includes('int') || value.includes('number') || value.includes('numeric') || value.includes('decimal') || value.includes('double') || value.includes('real') || value.includes('float') || value.includes('money')) return 'number';
  return 'string';
}

export function getMappingCacheParameterTypes(column: ApplicationMappingCacheColumn): readonly MappingCacheParameterType[] {
  return [inferMappingCacheParameterType(column.dataType)];
}

export function formatMappingCacheParameterValue(type: MappingCacheParameterType, value: unknown): string {
  if (value === null || value === undefined) return '';
  if (type === 'boolean') return value === true || value === 'true' ? 'true' : 'false';
  if (type === 'date') {
    const date = new Date(String(value));
    return Number.isNaN(date.getTime()) ? String(value) : date.toISOString().slice(0, 10);
  }
  if (type === 'json') return typeof value === 'string' ? value : JSON.stringify(value, null, 2);
  return String(value);
}

export function parseMappingCacheParameterValue(type: MappingCacheParameterType, raw: string): { value?: unknown; error?: string } {
  if (type === 'string') return { value: raw };
  if (type === 'number') {
    const value = Number(raw);
    return raw.trim() !== '' && Number.isFinite(value) ? { value } : { error: 'Enter a valid number.' };
  }
  if (type === 'boolean') return raw === 'true' || raw === 'false' ? { value: raw === 'true' } : { error: 'Choose true or false.' };
  if (type === 'date') {
    const value = new Date(raw);
    return raw.trim() !== '' && !Number.isNaN(value.getTime()) ? { value: value.toISOString() } : { error: 'Enter a valid date.' };
  }
  try {
    return raw.trim() !== '' ? { value: JSON.parse(raw) } : { error: 'Enter valid JSON.' };
  } catch {
    return { error: 'Enter valid JSON.' };
  }
}

export function validateMappingCacheParameterDraft(
  draft: Pick<ApplicationMappingCacheParameter, 'name' | 'columnResourceId' | 'dataType'>,
  existing: readonly ApplicationMappingCacheParameter[],
  editingResourceId?: string,
): MappingCacheParameterValidationErrors {
  const errors: MappingCacheParameterValidationErrors = {};
  if (!draft.name.trim()) errors.name = 'Parameter name is required.';
  else if (existing.some((item) => item.resourceId !== editingResourceId && item.name.trim().toLowerCase() === draft.name.trim().toLowerCase())) errors.name = 'Parameter names must be unique.';
  if (!draft.columnResourceId) errors.columnResourceId = 'Select a bound column.';
  if (!MAPPING_CACHE_PARAMETER_TYPES.includes(draft.dataType as MappingCacheParameterType)) errors.dataType = 'Select a supported type.';
  return errors;
}

