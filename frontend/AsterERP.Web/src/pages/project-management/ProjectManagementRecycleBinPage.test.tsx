// @vitest-environment jsdom

import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { ProjectManagementRecycleBinPage } from './ProjectManagementRecycleBinPage';

vi.mock('@tanstack/react-query', () => ({
  useQuery: () => ({
    data: {
      data: {
        projects: {
          total: 1,
          items: [{ id: 'project-1', projectCode: 'P-1', projectName: '已删除项目', status: 'Archived', versionNo: 2, deletedBy: 'operator', deletedByDisplayName: 'operator', affectedTaskCount: 3, canRestore: true, canPurge: false }]
        },
        tasks: {
          total: 1,
          items: [{ id: 'task-1', projectId: 'project-1', taskCode: 'T-1', title: '已删除任务', status: 'Todo', versionNo: 2, deletedBy: 'operator', deletedByDisplayName: 'operator', affectedDescendantCount: 2, canRestore: true, canPurge: false }]
        }
      }
    },
    isError: false,
    isLoading: false,
    refetch: vi.fn()
  }),
  useQueryClient: () => ({ invalidateQueries: vi.fn() })
}));

vi.mock('react-router-dom', () => ({ Link: ({ children }: { children: React.ReactNode }) => <a>{children}</a> }));
vi.mock('../../features/project-management/state/projectManagementWorkspaceScope', () => ({ useProjectManagementWorkspaceScope: () => ({ isAvailable: true, tenantId: 'tenant-a', appCode: 'SYSTEM' }) }));
vi.mock('../../features/project-management/state/projectManagementPlatformRoutes', () => ({ toProjectManagementPlatformRoute: (route: string) => route }));
vi.mock('../../core/query/useApiMutation', () => ({ useApiMutation: () => ({ isPending: false, mutate: vi.fn() }) }));
vi.mock('../../shared/feedback/useMessage', () => ({ useMessage: () => ({ error: vi.fn(), success: vi.fn() }) }));
vi.mock('../../shared/feedback/useConfirm', () => ({ useConfirm: () => vi.fn() }));
vi.mock('../../shared/auth/PermissionButton', () => ({ PermissionButton: ({ children }: { children: React.ReactNode }) => <button type="button">{children}</button> }));
vi.mock('../../shared/auth/PermissionGuard', () => ({ PermissionGuard: ({ children }: { children: React.ReactNode }) => <>{children}</> }));
vi.mock('../../shared/responsive/ResponsivePage', () => ({ ResponsivePage: ({ children }: { children: React.ReactNode }) => <main>{children}</main> }));
vi.mock('../../shared/status/Page403', () => ({ Page403: () => <p>403</p> }));
vi.mock('../../shared/status/PageError', () => ({ PageError: () => <p>error</p> }));
vi.mock('../../shared/status/PageLoading', () => ({ PageLoading: () => <p>loading</p> }));

describe('ProjectManagementRecycleBinPage', () => {
  afterEach(cleanup);

  it('separates deleted projects and tasks while showing deletion and impact details', () => {
    render(<ProjectManagementRecycleBinPage />);

    expect(screen.getByText('已删除项目（1）')).toBeTruthy();
    expect(screen.getByText('已删除任务（1）')).toBeTruthy();
    expect(screen.getAllByText('operator')).toHaveLength(2);
    expect(screen.getByText('影响任务')).toBeTruthy();
    expect(screen.getByText('影响后代')).toBeTruthy();
    expect(screen.getByText('3')).toBeTruthy();
    expect(screen.getByText('2')).toBeTruthy();
  });
});
