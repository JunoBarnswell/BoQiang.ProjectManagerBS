import { create } from 'zustand';

import {
  defaultUiPreferences,
  normalizeUiFontFamilyKey,
  normalizeUiPreferences,
  normalizeUiScalePercent,
  parseStoredUiPreferences,
  serializeUiPreferences,
  uiPreferenceStorageKey,
  type UiFontFamilyKey,
  type UiPreferences,
  type UiScalePercent
} from '../ui-preferences/uiPreferenceOptions';

import type { UiPreferenceStoreState } from './types';

function readStoredUiPreferences(): UiPreferences {
  if (typeof window === 'undefined') {
    return defaultUiPreferences;
  }

  try {
    return parseStoredUiPreferences(window.localStorage.getItem(uiPreferenceStorageKey));
  } catch {
    return defaultUiPreferences;
  }
}

function persistUiPreferences(preferences: UiPreferences): void {
  if (typeof window === 'undefined') {
    return;
  }

  try {
    window.localStorage.setItem(uiPreferenceStorageKey, serializeUiPreferences(preferences));
  } catch {
    // localStorage can be unavailable in hardened browser contexts.
  }
}

export const useUiPreferenceStore = create<UiPreferenceStoreState>((set) => {
  const initialPreferences = readStoredUiPreferences();

  return {
    fontFamilyKey: initialPreferences.fontFamilyKey,
    preferences: initialPreferences,
    resetPreferences: () => {
      persistUiPreferences(defaultUiPreferences);
      set({
        fontFamilyKey: defaultUiPreferences.fontFamilyKey,
        preferences: defaultUiPreferences,
        scalePercent: defaultUiPreferences.scalePercent
      });
    },
    scalePercent: initialPreferences.scalePercent,
    setFontFamilyKey: (fontFamilyKey: UiFontFamilyKey) => {
      set((state) => {
        const nextPreferences = normalizeUiPreferences({
          ...state.preferences,
          fontFamilyKey: normalizeUiFontFamilyKey(fontFamilyKey)
        });
        persistUiPreferences(nextPreferences);

        return {
          fontFamilyKey: nextPreferences.fontFamilyKey,
          preferences: nextPreferences,
          scalePercent: nextPreferences.scalePercent
        };
      });
    },
    setPreferences: (preferences: UiPreferences) => {
      const nextPreferences = normalizeUiPreferences(preferences);
      persistUiPreferences(nextPreferences);
      set({
        fontFamilyKey: nextPreferences.fontFamilyKey,
        preferences: nextPreferences,
        scalePercent: nextPreferences.scalePercent
      });
    },
    setScalePercent: (scalePercent: UiScalePercent) => {
      set((state) => {
        const nextPreferences = normalizeUiPreferences({
          ...state.preferences,
          scalePercent: normalizeUiScalePercent(scalePercent)
        });
        persistUiPreferences(nextPreferences);

        return {
          fontFamilyKey: nextPreferences.fontFamilyKey,
          preferences: nextPreferences,
          scalePercent: nextPreferences.scalePercent
        };
      });
    }
  };
});
