import { createContext, type ReactNode, useContext, useEffect } from 'react';

import type { ThemeMode } from '../config/env';
import { useThemeStore } from '../state';

interface ThemeContextValue {
  setTheme: (theme: ThemeMode) => void;
  theme: ThemeMode;
}

const ThemeContext = createContext<ThemeContextValue | null>(null);

export function ThemeProvider({ children }: { children: ReactNode }) {
  const theme = useThemeStore((state) => state.theme);
  const setTheme = useThemeStore((state) => state.setTheme);

  useEffect(() => {
    document.documentElement.dataset.theme = theme;
  }, [theme]);

  return <ThemeContext.Provider value={{ setTheme, theme }}>{children}</ThemeContext.Provider>;
}

export function useTheme() {
  const context = useContext(ThemeContext);

  if (!context) {
    throw new Error('useTheme must be used inside ThemeProvider');
  }

  return context;
}
