import { create } from 'zustand';

import { appEnv, type ThemeMode } from '../config/env';

import type { ThemeStoreState } from './types';

function normalizeTheme(theme: string | null | undefined): ThemeMode {
  return theme === 'dark' || theme === 'brand' || theme === 'kingdee' || theme === 'yonyou' ? theme : 'light';
}

function loadInitialTheme(): ThemeMode {
  const storedTheme = getThemeStorage()?.getItem('astererp.theme');
  return normalizeTheme(storedTheme ?? appEnv.defaultTheme);
}

export const useThemeStore = create<ThemeStoreState>((set) => ({
  setTheme: (theme) => {
    const nextTheme = normalizeTheme(theme);
    set({ theme: nextTheme });
    getThemeStorage()?.setItem('astererp.theme', nextTheme);
    if (typeof document !== 'undefined') {
      document.documentElement.dataset.theme = nextTheme;
    }
  },
  theme: loadInitialTheme()
}));

function getThemeStorage(): Storage | null {
  if (typeof window === 'undefined') return null;

  try {
    return window.localStorage;
  } catch {
    return null;
  }
}
