// @vitest-environment jsdom

import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { useWorkspaceStore } from '../../core/state/workspaceStore';

import type { ApplicationDatabaseGateResult } from './applicationDatabaseGate';
import { ApplicationDatabaseRequiredRoute } from './ApplicationDatabaseRequiredRoute';

const gateState = vi.hoisted(() => ({
  current: null as ApplicationDatabaseGateResult | null
}));

vi.mock('./applicationDatabaseGate', () => ({
  useApplicationDatabaseGate: () => gateState.current
}));

describe('ApplicationDatabaseRequiredRoute', () => {
  beforeEach(() => {
    useWorkspaceStore.setState({
      currentWorkspace: {
        appCode: 'MES',
        appName: 'MES',
        defaultRoutePath: '/home',
        systemCode: 'MES',
        systemId: 'system-mes',
        systemName: 'MES',
        tenantId: 'tenant-a',
        tenantName: 'Tenant A',
        workspaceId: 'tenant-a:MES',
        workspaceLevel: 'application'
      }
    });
  });

  it('does not render children while the binding gate is forbidden', () => {
    gateState.current = {
      error: new Error('forbidden'),
      refetch: vi.fn(),
      state: 'forbidden',
      status: null
    };

    render(
      <ApplicationDatabaseRequiredRoute>
        <div>business page</div>
      </ApplicationDatabaseRequiredRoute>
    );

    expect(screen.queryByText('business page')).toBeNull();
    expect(screen.getByText('当前账号无权访问应用数据库')).toBeTruthy();
  });

  it('renders children only after the authoritative status is ready', () => {
    gateState.current = {
      error: null,
      refetch: vi.fn(),
      state: 'ready',
      status: {
        canManage: false,
        databaseName: 'mes.db',
        displayName: 'MES',
        isBound: true,
        isReachable: true,
        message: 'ready',
        provider: 'Sqlite',
        status: 'Ready',
        updatedAt: null
      }
    };

    render(
      <ApplicationDatabaseRequiredRoute>
        <div>business page</div>
      </ApplicationDatabaseRequiredRoute>
    );

    expect(screen.getByText('business page')).toBeTruthy();
  });
});
