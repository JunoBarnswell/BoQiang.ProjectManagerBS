import { translateCurrentLocale } from '../../core/i18n/I18nProvider';

import type { DictOption, DictStateSnapshot } from './dictTypes';

const listeners = new Set<() => void>();
const defaultDictOptions: Record<string, DictOption[]> = {
  locale: [
    { label: 'dict.locale.zhCN', value: 'zh-CN' },
    { label: 'dict.locale.enUS', value: 'en-US' }
  ],
  sys_enabled_status: [
    { color: '#16a34a', label: 'common.enabled', value: '1' },
    { color: '#dc2626', label: 'common.disabled', value: '0' }
  ],
  theme_mode: [
    { label: 'dict.theme.light', value: 'light' },
    { label: 'dict.theme.dark', value: 'dark' },
    { label: 'dict.theme.brand', value: 'brand' }
  ]
};
const dictMemory = new Map<string, DictOption[]>(Object.entries(defaultDictOptions));

let revision = 0;

function notify() {
  revision += 1;
  for (const listener of listeners) {
    listener();
  }
}

export function getDictOptions(dictType: string): DictOption[] {
  return getStoredDictOptions(dictType).map((option) => ({
    ...option,
    label: resolveDictOptionLabel(option.label)
  }));
}

export function resolveDictOptionLabel(label: string, translate = translateCurrentLocale): string {
  const normalizedLabel = normalizeDictLabel(label);
  if (normalizedLabel.startsWith('dict.') || normalizedLabel.startsWith('common.')) {
    return translate(normalizedLabel);
  }

  return label;
}

export function getDictLabel(dictType: string, value: string): string {
  const option = getDictOptions(dictType).find((item) => item.value === value);
  return resolveDictOptionLabel(option?.label ?? value);
}

export function getDictSnapshot(dictType: string): DictStateSnapshot {
  return {
    options: getDictOptions(dictType),
    revision
  };
}

export function setDictOptions(dictType: string, options: DictOption[]): void {
  dictMemory.set(dictType, options);
  notify();
}

export function mergeDictOptions(dictType: string, options: DictOption[]): void {
  const currentOptions = getStoredDictOptions(dictType);
  const nextOptions = [...currentOptions];

  for (const option of options) {
    const existingIndex = nextOptions.findIndex((item) => item.value === option.value);
    if (existingIndex >= 0) {
      nextOptions[existingIndex] = option;
    } else {
      nextOptions.push(option);
    }
  }

  setDictOptions(dictType, nextOptions);
}

export function clearDictOptions(dictType?: string): void {
  if (dictType) {
    const defaultOptions = defaultDictOptions[dictType];
    if (defaultOptions) {
      dictMemory.set(dictType, defaultOptions);
    } else {
      dictMemory.delete(dictType);
    }
  } else {
    dictMemory.clear();
    Object.entries(defaultDictOptions).forEach(([key, options]) => dictMemory.set(key, options));
  }

  notify();
}

export function subscribeDictStore(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

function getStoredDictOptions(dictType: string): DictOption[] {
  return dictMemory.get(dictType) ?? [];
}

function normalizeDictLabel(label: string): string {
  if (label.startsWith('theme.')) {
    return `dict.${label}`;
  }

  return label;
}
