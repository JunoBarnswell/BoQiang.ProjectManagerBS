import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';

import { App } from './app/App';
import { AppProviders } from './app/providers';
import '@xyflow/react/dist/style.css';
import './app/App.css';
import './core/responsive/responsive.css';
import { AppErrorBoundary } from './shared/status/ErrorBoundary';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <AppProviders>
      <AppErrorBoundary>
        <App />
      </AppErrorBoundary>
    </AppProviders>
  </StrictMode>
);
