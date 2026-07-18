// @vitest-environment jsdom

import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { ProjectManagementOverviewPage } from './ProjectManagementOverviewPage';

const queryCalls = vi.hoisted(() => [] as Array<{ enabled?: boolean }>);
const queryResults = vi.hoisted(() => [] as Array<Record<string, unknown>>);
const permissionState = vi.hoisted(() => ({ canViewActivities: false }));

vi.mock('@tanstack/react-query', () => ({
  useQuery: (options: { enabled?: boolean }) => {
    queryCalls.push(options);
    return queryResults.shift();
  }
}));

vi.mock('react-router-dom', () => ({
  useParams: () => ({ projectId: 'project-1' })
}));

vi.mock('../../core/auth/usePermission', () => ({
  usePermission: () => ({ hasPermission: permissionState.canViewActivities })
}));

vi.mock('../../features/project-management/state/projectManagementWorkspaceScope', () => ({
  useProjectManagementWorkspaceScope: () => ({ isAvailable: true, tenantId: 'tenant-a', appCode: 'MES' })
}));

vi.mock('../../shared/responsive/ResponsivePage', () => ({
  ResponsivePage: ({ children }: { children: React.ReactNode }) => <main>{children}</main>
}));

vi.mock('../../shared/status/Page403', () => ({ Page403: () => <p>403</p> }));
vi.mock('../../shared/status/PageError', () => ({ PageError: ({ description }: { description?: React.ReactNode }) => <p>{description}</p> }));
vi.mock('../../shared/status/PageLoading', () => ({ PageLoading: () => <p>loading</p> }));

describe('ProjectManagementOverviewPage', () => {
  afterEach(cleanup);
  beforeEach(() => {
    queryCalls.length = 0;
    queryResults.length = 0;
    permissionState.canViewActivities = false;
    queryResults.push(
      {
        data: {
          data: {
            items: [{
              blockedTaskCount: 0,
              milestones: [],
              overdueTaskCount: 0,
              project: { description: null, projectName: '项目 A', status: '进行中' },
              taskCount: 2,
              taskProgressPercent: 50
            }]
          }
        },
        error: null,
        isError: false,
        isLoading: false,
        refetch: vi.fn()
      },
      {
        data: { data: [] },
        error: null,
        isError: false,
        isLoading: false,
        refetch: vi.fn()
      }
    );
  });

  it('keeps the overview available and does not enable the activity query without audit:view', () => {
    render(<ProjectManagementOverviewPage />);

    expect(screen.getByText('整体进度')).toBeTruthy();
    expect(screen.getByText('当前账号无查看项目活动的权限。')).toBeTruthy();
    expect(queryCalls).toHaveLength(2);
    expect(queryCalls[0]?.enabled).toBe(true);
    expect(queryCalls[1]?.enabled).toBe(false);
  });

  it('keeps the overview available when an authorized activity request fails', () => {
    permissionState.canViewActivities = true;
    queryResults.length = 0;
    queryResults.push(
      {
        data: {
          data: {
            items: [{
              blockedTaskCount: 0,
              milestones: [],
              overdueTaskCount: 0,
              project: { description: null, projectName: '项目 A', status: '进行中' },
              taskCount: 2,
              taskProgressPercent: 50
            }]
          }
        },
        error: null,
        isError: false,
        isLoading: false,
        refetch: vi.fn()
      },
      { data: undefined, error: new Error('activity unavailable'), isError: true, isLoading: false, refetch: vi.fn() }
    );

    render(<ProjectManagementOverviewPage />);

    expect(screen.getByText('整体进度')).toBeTruthy();
    expect(screen.getByText('项目活动暂时无法加载。')).toBeTruthy();
    expect(queryCalls[1]?.enabled).toBe(true);
  });
});
