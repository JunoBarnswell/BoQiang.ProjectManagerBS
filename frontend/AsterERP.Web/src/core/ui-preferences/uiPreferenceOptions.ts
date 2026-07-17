export const uiPreferenceStorageKey = 'astererp.ui.preferences';

export const uiScaleOptions = [80, 90, 100, 110, 120, 130, 140, 150] as const;

export type UiScalePercent = (typeof uiScaleOptions)[number];

export type UiFontFamilyKey = 'system' | 'yahei' | 'notoSansSc' | 'songti';

export interface UiPreferences {
  fontFamilyKey: UiFontFamilyKey;
  scalePercent: UiScalePercent;
}

export interface UiFontFamilyOption {
  cssValue: string;
  key: UiFontFamilyKey;
  labelKey: string;
}

export const defaultUiPreferences: UiPreferences = {
  fontFamilyKey: 'system',
  scalePercent: 100
};

export const uiFontFamilyOptions: UiFontFamilyOption[] = [
  {
    cssValue:
      '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, "Noto Sans", sans-serif, "Apple Color Emoji", "Segoe UI Emoji", "Segoe UI Symbol", "Noto Color Emoji"',
    key: 'system',
    labelKey: 'display.font.system'
  },
  {
    cssValue:
      '"Microsoft YaHei UI", "Microsoft YaHei", "PingFang SC", "Hiragino Sans GB", Arial, sans-serif',
    key: 'yahei',
    labelKey: 'display.font.yahei'
  },
  {
    cssValue:
      '"Noto Sans SC", "Source Han Sans SC", "Microsoft YaHei", Arial, sans-serif',
    key: 'notoSansSc',
    labelKey: 'display.font.notoSansSc'
  },
  {
    cssValue:
      'SimSun, "Songti SC", "Noto Serif SC", "Source Han Serif SC", serif',
    key: 'songti',
    labelKey: 'display.font.songti'
  }
];

const uiScaleOptionSet = new Set<number>(uiScaleOptions);
const uiFontFamilyOptionSet = new Set<UiFontFamilyKey>(uiFontFamilyOptions.map((option) => option.key));

export function normalizeUiScalePercent(value: unknown): UiScalePercent {
  const numericValue = typeof value === 'string' && value.trim() !== '' ? Number(value) : value;
  return typeof numericValue === 'number' && uiScaleOptionSet.has(numericValue)
    ? (numericValue as UiScalePercent)
    : defaultUiPreferences.scalePercent;
}

export function normalizeUiFontFamilyKey(value: unknown): UiFontFamilyKey {
  return typeof value === 'string' && uiFontFamilyOptionSet.has(value as UiFontFamilyKey)
    ? (value as UiFontFamilyKey)
    : defaultUiPreferences.fontFamilyKey;
}

export function normalizeUiPreferences(value: unknown): UiPreferences {
  if (!value || typeof value !== 'object') {
    return defaultUiPreferences;
  }

  const candidate = value as Partial<Record<keyof UiPreferences, unknown>>;
  return {
    fontFamilyKey: normalizeUiFontFamilyKey(candidate.fontFamilyKey),
    scalePercent: normalizeUiScalePercent(candidate.scalePercent)
  };
}

export function parseStoredUiPreferences(value: string | null | undefined): UiPreferences {
  if (!value) {
    return defaultUiPreferences;
  }

  try {
    return normalizeUiPreferences(JSON.parse(value));
  } catch {
    return defaultUiPreferences;
  }
}

export function serializeUiPreferences(preferences: UiPreferences): string {
  return JSON.stringify(normalizeUiPreferences(preferences));
}

export function getUiScaleRatio(scalePercent: UiScalePercent): number {
  return scalePercent / 100;
}

export function resolveUiFontFamilyCssValue(fontFamilyKey: UiFontFamilyKey): string {
  return uiFontFamilyOptions.find((option) => option.key === fontFamilyKey)?.cssValue ?? uiFontFamilyOptions[0].cssValue;
}
