// @vitest-environment jsdom

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { ApplicationConsoleSummaryDto } from '../../api/application-console/applicationConsole.types';
import { useWorkspaceStore } from '../../core/state/workspaceStore';

import { ApplicationConsolePageFrame } from './ApplicationConsolePageFrame';

vi.mock('../../api/application-console/applicationConsole.api', () => ({
  getApplicationConsoleSummary: () => Promise.resolve({ data: summary })
}));

const summary = {
  application: { appCode: 'MES', appName: 'MES', appType: 'Business', createdTime: '2026-01-01T00:00:00.000Z', status: 'Enabled', systemName: 'MES', tenantId: 'tenant-a', tenantName: 'Tenant A', workspaceLevel: 'application' },
  capabilityCounts: {}, databaseBinding: { canManage: false, isBound: true, isReachable: true, status: 'Ready' }, developmentShortcuts: [], draftSignals: {}, entryTree: [], metrics: [], recentAudits: [], recentDevelopmentItems: [], recentPublishes: [], versionContext: {}
} as unknown as ApplicationConsoleSummaryDto;

describe('ApplicationConsolePageFrame', () => {
  beforeEach(() => {
    useWorkspaceStore.setState({ currentWorkspace: { appCode: 'MES', appName: 'MES', defaultRoutePath: '/home', systemCode: 'MES', systemId: 'system-mes', systemName: 'MES', tenantId: 'tenant-a', tenantName: 'Tenant A', workspaceId: 'tenant-a:MES', workspaceLevel: 'application' } });
  });

  it('renders its page after the real console summary loads and does not run a second database gate', async () => {
    render(<QueryClientProvider client={new QueryClient()}><MemoryRouter><ApplicationConsolePageFrame pageKey="data-center">{() => <div>business page</div>}</ApplicationConsolePageFrame></MemoryRouter></QueryClientProvider>);

    expect(await screen.findByText('business page')).toBeTruthy();
  });
});
