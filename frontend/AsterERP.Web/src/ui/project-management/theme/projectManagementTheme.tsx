import { ThemeProvider, createTheme } from '@mui/material/styles';
import type { ReactNode } from 'react';

import { projectManagementTokens as tokens } from './tokens';

export const projectManagementTheme = createTheme({
  palette: { mode: 'light', primary: { main: tokens.colors.accent }, background: { default: tokens.colors.background, paper: tokens.colors.background }, text: { primary: tokens.colors.text, secondary: tokens.colors.muted }, divider: tokens.colors.border, success: { main: tokens.colors.success }, warning: { main: tokens.colors.warning }, error: { main: tokens.colors.danger } },
  typography: { fontFamily: tokens.typography.ui, button: { textTransform: 'none', fontWeight: 600, fontSize: '0.8125rem' }, body2: { fontSize: '0.8125rem', lineHeight: 1.45 } },
  shape: { borderRadius: tokens.radius.medium },
  components: { MuiButton: { defaultProps: { disableElevation: true }, styleOverrides: { root: { borderRadius: tokens.radius.medium, minHeight: 34 } } }, MuiIconButton: { styleOverrides: { root: { borderRadius: tokens.radius.medium, padding: 7 } } }, MuiChip: { styleOverrides: { root: { borderRadius: tokens.radius.pill, height: 26, fontSize: '0.75rem' } } }, MuiPaper: { styleOverrides: { root: { backgroundImage: 'none' } } }, MuiDialog: { styleOverrides: { paper: { border: `1px solid ${tokens.colors.border}`, borderRadius: tokens.radius.large, boxShadow: '0 22px 70px rgba(24, 25, 32, .18)' } } }, MuiTooltip: { styleOverrides: { tooltip: { fontSize: '0.75rem', borderRadius: tokens.radius.small } } } },
});

export function ProjectManagementThemeProvider({ children }: { children: ReactNode }) { return <ThemeProvider theme={projectManagementTheme}>{children}</ThemeProvider>; }
