import { ThemeProvider, createTheme } from '@mui/material/styles';
import { useMemo, type ReactNode } from 'react';

import { useThemeStore } from '../../../core/state/themeStore';

import { projectManagementTokens as tokens } from './tokens';

export const projectManagementTheme = createProjectManagementTheme(false);

function createProjectManagementTheme(dark: boolean) {
  const colors = dark
    ? {
        ...tokens.colors,
        background: '#18191d',
        surface: '#202126',
        surfaceMuted: '#292a30',
        border: '#383a43',
        text: '#f2f3f5',
        muted: '#a4a7b0',
      }
    : tokens.colors;
  return createTheme({
    palette: { mode: dark ? 'dark' : 'light', primary: { main: colors.accent }, background: { default: colors.background, paper: colors.surface }, text: { primary: colors.text, secondary: colors.muted }, divider: colors.border, success: { main: colors.success }, warning: { main: colors.warning }, error: { main: colors.danger } },
    typography: { fontFamily: tokens.typography.ui, button: { textTransform: 'none', fontWeight: 600, fontSize: '0.8125rem' }, body2: { fontSize: '0.8125rem', lineHeight: 1.45 } },
    shape: { borderRadius: tokens.radius.medium },
    components: { MuiButton: { defaultProps: { disableElevation: true }, styleOverrides: { root: { borderRadius: tokens.radius.medium, minHeight: 34 } } }, MuiIconButton: { styleOverrides: { root: { borderRadius: tokens.radius.medium, padding: 7 } } }, MuiChip: { styleOverrides: { root: { borderRadius: tokens.radius.pill, height: 26, fontSize: '0.75rem' } } }, MuiPaper: { styleOverrides: { root: { backgroundImage: 'none' } } }, MuiDialog: { styleOverrides: { paper: { border: `1px solid ${colors.border}`, borderRadius: tokens.radius.large, boxShadow: dark ? '0 22px 70px rgba(0, 0, 0, .45)' : '0 22px 70px rgba(24, 25, 32, .18)' } } }, MuiTooltip: { styleOverrides: { tooltip: { fontSize: '0.75rem', borderRadius: tokens.radius.small } } } },
  });
}

export function ProjectManagementThemeProvider({ children }: { children: ReactNode }) {
  const themeMode = useThemeStore(state => state.theme);
  const theme = useMemo(() => createProjectManagementTheme(themeMode === 'dark'), [themeMode]);
  return <ThemeProvider theme={theme}>{children}</ThemeProvider>;
}
