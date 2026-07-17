import React, { type ReactNode } from 'react';

import { translateCurrentLocale } from '../../core/i18n/I18nProvider';

import { Page500 } from './Page500';

interface AppErrorBoundaryState {
  error: Error | null;
}

interface AppErrorBoundaryProps {
  children: ReactNode;
}

export class AppErrorBoundary extends React.Component<AppErrorBoundaryProps, AppErrorBoundaryState> {
  public state: AppErrorBoundaryState = {
    error: null
  };

  public static getDerivedStateFromError(error: Error): AppErrorBoundaryState {
    return { error };
  }

  public override componentDidCatch(error: Error) {
    console.error('Application error boundary caught an error:', error);
  }

  public override render() {
    if (this.state.error) {
      return (
        <div className="page-state-shell">
          <Page500 />
          <button type="button" className="primary-button" onClick={() => window.location.reload()}>
            {translateCurrentLocale('runtime.refresh')}
          </button>
        </div>
      );
    }

    return this.props.children;
  }
}
