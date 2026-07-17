import { useEffect, type ReactNode } from 'react';

import { subscribeAuthExpired } from '../core/http/authEvents';
import { I18nProvider } from '../core/i18n/I18nProvider';
import { QueryProvider } from '../core/query/QueryProvider';
import { ResponsiveProvider } from '../core/responsive/ResponsiveProvider';
import { useAuthStore, useThemeStore } from '../core/state';
import { UiPreferenceRoot } from '../core/ui-preferences/UiPreferenceRoot';
import { FeedbackProvider } from '../shared/feedback/FeedbackProvider';

interface AppProvidersProps {
  children: ReactNode;
}

export function AppProviders({ children }: AppProvidersProps) {
  return (
    <QueryProvider>
      <ResponsiveProvider>
        <I18nProvider>
          <FeedbackProvider>
            <StoreBootstrap>
              <UiPreferenceRoot>{children}</UiPreferenceRoot>
            </StoreBootstrap>
          </FeedbackProvider>
        </I18nProvider>
      </ResponsiveProvider>
    </QueryProvider>
  );
}

function StoreBootstrap({ children }: { children: ReactNode }) {
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);
  const theme = useThemeStore((state) => state.theme);
  const logout = useAuthStore((state) => state.logout);
  const refreshSession = useAuthStore((state) => state.refreshSession);

  useEffect(() => {
    document.documentElement.dataset.theme = theme;
  }, [theme]);

  useEffect(() => {
    if (!isAuthenticated) {
      void refreshSession();
    }
  }, [isAuthenticated, refreshSession]);

  useEffect(() => subscribeAuthExpired(logout), [logout]);

  return <>{children}</>;
}
