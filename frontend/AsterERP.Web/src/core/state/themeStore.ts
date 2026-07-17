import { create } from 'zustand';

import { appEnv, type ThemeMode } from '../config/env';

import type { ThemeStoreState } from './types';

function normalizeTheme(theme: string | null | undefined): ThemeMode {
  return theme === 'dark' || theme === 'brand' || theme === 'kingdee' || theme === 'yonyou' ? theme : 'light';
}

function loadInitialTheme(): ThemeMode {
  const storedTheme = localStorage.getItem('astererp.theme');
  return normalizeTheme(storedTheme ?? appEnv.defaultTheme);
}

export const useThemeStore = create<ThemeStoreState>((set) => ({
  setTheme: (theme) => {
    const nextTheme = normalizeTheme(theme);
    set({ theme: nextTheme });
    localStorage.setItem('astererp.theme', nextTheme);
    document.documentElement.dataset.theme = nextTheme;
  },
  theme: loadInitialTheme()
}));

