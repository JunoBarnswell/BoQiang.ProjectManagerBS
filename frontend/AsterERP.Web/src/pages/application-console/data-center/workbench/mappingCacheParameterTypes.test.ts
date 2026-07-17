import { describe, expect, it } from 'vitest';

import { formatMappingCacheParameterValue, inferMappingCacheParameterType, parseMappingCacheParameterValue, validateMappingCacheParameterDraft } from './mappingCacheParameterTypes';

describe('mapping cache parameter contract', () => {
  it('infers the canonical type from catalog column metadata', () => {
    expect(inferMappingCacheParameterType('DECIMAL(18,2)')).toBe('number');
    expect(inferMappingCacheParameterType('timestamp with time zone')).toBe('date');
    expect(inferMappingCacheParameterType('jsonb')).toBe('json');
    expect(inferMappingCacheParameterType('varchar')).toBe('string');
  });

  it('converts typed execution values and rejects invalid input', () => {
    expect(parseMappingCacheParameterValue('number', '12.50')).toEqual({ value: 12.5 });
    expect(parseMappingCacheParameterValue('boolean', 'false')).toEqual({ value: false });
    expect(parseMappingCacheParameterValue('date', '2026-07-14')).toEqual({ value: '2026-07-14T00:00:00.000Z' });
    expect(parseMappingCacheParameterValue('json', '{"active":true}')).toEqual({ value: { active: true } });
    expect(parseMappingCacheParameterValue('number', 'not-a-number').error).toBeTruthy();
    expect(parseMappingCacheParameterValue('json', '{bad').error).toBeTruthy();
  });

  it('enforces stable names and bound columns before saving', () => {
    const errors = validateMappingCacheParameterDraft(
      { name: 'Status', columnResourceId: '', dataType: 'unsupported' },
      [{ resourceId: 'p1', name: 'status', columnResourceId: 'c1', dataType: 'string', required: true }],
    );
    expect(errors.name).toBe('Parameter names must be unique.');
    expect(errors.columnResourceId).toBeTruthy();
    expect(errors.dataType).toBeTruthy();
  });

  it('restores session/default values in the editor representation', () => {
    expect(formatMappingCacheParameterValue('json', { limit: 20 })).toContain('"limit": 20');
    expect(formatMappingCacheParameterValue('date', '2026-07-14T00:00:00.000Z')).toBe('2026-07-14');
  });
});

