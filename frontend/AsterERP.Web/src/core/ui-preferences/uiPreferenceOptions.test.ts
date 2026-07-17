import { describe, expect, it } from 'vitest';

import {
  defaultUiPreferences,
  normalizeUiFontFamilyKey,
  normalizeUiPreferences,
  normalizeUiScalePercent,
  parseStoredUiPreferences,
  resolveUiFontFamilyCssValue,
  serializeUiPreferences
} from './uiPreferenceOptions';

describe('uiPreferenceOptions', () => {
  it('uses defaults for missing or invalid stored values', () => {
    expect(parseStoredUiPreferences(null)).toEqual(defaultUiPreferences);
    expect(parseStoredUiPreferences('{')).toEqual(defaultUiPreferences);
    expect(parseStoredUiPreferences(JSON.stringify({ fontFamilyKey: 'bad', scalePercent: 200 }))).toEqual(defaultUiPreferences);
  });

  it('normalizes supported scale and font values', () => {
    expect(normalizeUiScalePercent('120')).toBe(120);
    expect(normalizeUiScalePercent(150)).toBe(150);
    expect(normalizeUiScalePercent(85)).toBe(defaultUiPreferences.scalePercent);
    expect(normalizeUiFontFamilyKey('notoSansSc')).toBe('notoSansSc');
    expect(normalizeUiFontFamilyKey('unknown')).toBe(defaultUiPreferences.fontFamilyKey);
  });

  it('serializes only the normalized public preference contract', () => {
    const serialized = serializeUiPreferences({ fontFamilyKey: 'songti', scalePercent: 130 });
    expect(JSON.parse(serialized)).toEqual({ fontFamilyKey: 'songti', scalePercent: 130 });
  });

  it('falls back to system font css for unknown values after normalization', () => {
    const preferences = normalizeUiPreferences({ fontFamilyKey: 'bad', scalePercent: 90 });
    expect(preferences).toEqual({ fontFamilyKey: 'system', scalePercent: 90 });
    expect(resolveUiFontFamilyCssValue(preferences.fontFamilyKey)).toContain('Segoe UI');
  });
});
