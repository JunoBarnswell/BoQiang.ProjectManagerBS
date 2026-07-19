// @vitest-environment jsdom

import { cleanup, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { ProjectManagementSyncPage } from './ProjectManagementSyncPage';

const queryCalls = vi.hoisted(() => [] as Array<{ enabled?: boolean }>);
const permissions = vi.hoisted(() => ({ canExport: false, canImport: false }));

vi.mock('@tanstack/react-query', () => ({
  useQuery: (options: { enabled?: boolean; queryKey?: readonly unknown[] }) => {
    queryCalls.push(options);
    return {
      data: { data: options.queryKey?.includes('sync-watermark') ? { currentSequenceNo: 5, acknowledgedSequenceNo: 3, lastSeenAt: null } : [] },
      error: null,
      isError: false,
      isLoading: false,
      refetch: vi.fn()
    };
  },
  useQueryClient: () => ({ invalidateQueries: vi.fn() })
}));

vi.mock('react-router-dom', () => ({ Link: ({ children }: { children: React.ReactNode }) => <a>{children}</a> }));

vi.mock('../../api/project-management/projectManagement.api', () => ({
  acknowledgeProjectManagementSync: vi.fn(),
  getProjectManagementSyncChanges: vi.fn(),
  getProjectManagementSyncWatermark: vi.fn()
}));

vi.mock('../../core/auth/usePermission', () => ({
  usePermission: (code: string) => ({
    hasPermission: code === 'project-management:sync:export' ? permissions.canExport : permissions.canImport
  })
}));

vi.mock('../../core/query/useApiMutation', () => ({
  useApiMutation: () => ({ isPending: false, mutate: vi.fn() })
}));

vi.mock('../../features/project-management/state/projectManagementWorkspaceScope', () => ({
  useProjectManagementWorkspaceScope: () => ({ isAvailable: true, tenantId: 'tenant-a', appCode: 'SYSTEM' })
}));

vi.mock('../../features/project-management/state/projectManagementPlatformRoutes', () => ({
  toProjectManagementPlatformRoute: () => '/platform/project-management/project-data-space'
}));

vi.mock('../../shared/auth/PermissionButton', () => ({
  PermissionButton: ({ children, ...props }: React.ButtonHTMLAttributes<HTMLButtonElement>) => <button {...props}>{children}</button>
}));
vi.mock('../../shared/feedback/useMessage', () => ({ useMessage: () => ({ error: vi.fn(), success: vi.fn() }) }));
vi.mock('../../shared/responsive/ResponsivePage', () => ({ ResponsivePage: ({ children }: { children: React.ReactNode }) => <main>{children}</main> }));
vi.mock('../../shared/status/Page403', () => ({ Page403: () => <p>403</p> }));
vi.mock('../../shared/status/PageError', () => ({ PageError: ({ description }: { description?: React.ReactNode }) => <p>{description}</p> }));
vi.mock('../../shared/status/PageLoading', () => ({ PageLoading: () => <p>loading</p> }));

describe('ProjectManagementSyncPage', () => {
  beforeEach(() => {
    cleanup();
    queryCalls.length = 0;
    permissions.canExport = false;
    permissions.canImport = false;
  });

  it('enables export queries but hides import confirmations for export-only access', () => {
    permissions.canExport = true;

    render(<ProjectManagementSyncPage />);

    expect(queryCalls).toHaveLength(2);
    expect(queryCalls.every((call) => call.enabled)).toBe(true);
    expect(screen.getByText('当前水位')).toBeTruthy();
    expect(screen.queryByText('确认当前水位')).toBeNull();
  });

  it('does not enable export queries and keeps the import-only guidance available', () => {
    permissions.canImport = true;

    render(<ProjectManagementSyncPage />);

    expect(queryCalls).toHaveLength(2);
    expect(queryCalls.every((call) => call.enabled === false)).toBe(true);
    expect(screen.getByText('你拥有同步导入权限，但没有同步导出权限；请前往数据空间选择并预览同步包。')).toBeTruthy();
    expect(screen.queryByText('当前水位')).toBeNull();
  });

  it('enables export queries and import confirmations when both permissions are granted', () => {
    permissions.canExport = true;
    permissions.canImport = true;

    render(<ProjectManagementSyncPage />);

    expect(queryCalls.every((call) => call.enabled)).toBe(true);
    expect(screen.getByText('确认当前水位')).toBeTruthy();
  });

  it('defensively renders 403 without either sync permission', () => {
    render(<ProjectManagementSyncPage />);

    expect(queryCalls.every((call) => call.enabled === false)).toBe(true);
    expect(screen.getByText('403')).toBeTruthy();
  });
});
