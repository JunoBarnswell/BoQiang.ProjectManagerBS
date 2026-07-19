// @vitest-environment jsdom

import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { ProjectManagementDataSpacePage } from './ProjectManagementDataSpacePage';

const queryOptions = vi.hoisted(() => [] as Array<{ enabled?: boolean; queryKey?: readonly unknown[] }>);
const workspaceScope = vi.hoisted(() => ({ isAvailable: true, tenantId: 'tenant-a', appCode: 'SYSTEM' }));

vi.mock('@tanstack/react-query', () => ({
  useQuery: (options: { enabled?: boolean; queryKey?: readonly unknown[] }) => {
    queryOptions.push(options);
    return {
      data: options.queryKey?.includes('data-space-summary')
        ? { data: { tenantId: 'tenant-a', appCode: workspaceScope.appCode, databaseStatus: 'Ready', projectCount: 1, taskCount: 2, memberCount: 3, milestoneCount: 4, attachmentCount: 5 } }
        : { data: [] },
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
  applyProjectManagementSync: vi.fn(),
  createProjectManagementBackup: vi.fn(),
  exportProjectManagementSync: vi.fn(),
  getProjectManagementBackups: vi.fn(),
  getProjectManagementDataSpaceSummary: vi.fn(),
  previewProjectManagementBackupRestore: vi.fn(),
  previewProjectManagementSync: vi.fn(),
  restoreProjectManagementBackup: vi.fn()
}));

vi.mock('../../core/auth/usePermission', () => ({ usePermission: () => ({ hasPermission: true }) }));
vi.mock('../../core/query/useApiMutation', () => ({ useApiMutation: () => ({ isPending: false, mutate: vi.fn() }) }));
vi.mock('../../features/project-management/state/projectManagementWorkspaceScope', () => ({
  useProjectManagementWorkspaceScope: () => workspaceScope
}));
vi.mock('../../features/project-management/state/projectManagementPlatformRoutes', () => ({
  toProjectManagementPlatformRoute: () => '/platform/project-management'
}));
vi.mock('../../shared/auth/PermissionButton', () => ({
  PermissionButton: ({ children, ...props }: React.ButtonHTMLAttributes<HTMLButtonElement>) => <button {...props}>{children}</button>
}));
vi.mock('../../shared/auth/PermissionGuard', () => ({ PermissionGuard: ({ children }: { children: React.ReactNode }) => <>{children}</> }));
vi.mock('../../shared/feedback/useMessage', () => ({ useMessage: () => ({ error: vi.fn(), success: vi.fn() }) }));
vi.mock('../../shared/responsive/ResponsivePage', () => ({ ResponsivePage: ({ children }: { children: React.ReactNode }) => <main>{children}</main> }));
vi.mock('../../shared/status/Page403', () => ({ Page403: () => <p>403</p> }));
vi.mock('../../shared/status/PageError', () => ({ PageError: ({ description }: { description?: React.ReactNode }) => <p>{description}</p> }));
vi.mock('../../shared/status/PageLoading', () => ({ PageLoading: () => <p>loading</p> }));

describe('ProjectManagementDataSpacePage', () => {
  beforeEach(() => {
    queryOptions.length = 0;
    workspaceScope.appCode = 'SYSTEM';
  });

  afterEach(cleanup);

  it('does not expose or fetch physical backups in the SYSTEM platform workspace', () => {
    render(<ProjectManagementDataSpacePage />);

    expect(screen.getByText('平台项目管理暂不支持物理备份/恢复，待 pm_* 逻辑备份能力。')).toBeTruthy();
    expect(screen.queryByRole('button', { name: '创建备份' })).toBeNull();
    expect(screen.queryByLabelText('当前密码')).toBeNull();
    expect(queryOptions.find((options) => options.queryKey?.includes('backups'))?.enabled).toBe(false);
  });

  it('keeps the existing physical backup flow outside the SYSTEM platform workspace', () => {
    workspaceScope.appCode = 'MES';

    render(<ProjectManagementDataSpacePage />);

    expect(screen.queryByText('平台项目管理暂不支持物理备份/恢复，待 pm_* 逻辑备份能力。')).toBeNull();
    expect(screen.getByRole('button', { name: '创建备份' })).toBeTruthy();
    expect(screen.getByLabelText('当前密码')).toBeTruthy();
    expect(queryOptions.find((options) => options.queryKey?.includes('backups'))?.enabled).toBe(true);
  });
});
