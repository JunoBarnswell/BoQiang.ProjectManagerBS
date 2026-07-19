// @vitest-environment jsdom

import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { ProjectManagementOverviewPage } from './ProjectManagementOverviewPage';

const queryCalls = vi.hoisted(() => [] as Array<{ enabled?: boolean; queryKey?: readonly unknown[] }>);
const queryResults = vi.hoisted(() => ({
  activities: {} as Record<string, unknown>,
  overview: {} as Record<string, unknown>
}));
const permissionState = vi.hoisted(() => ({ canViewActivities: false }));

vi.mock('@tanstack/react-query', () => ({
  useQuery: (options: { enabled?: boolean; queryKey?: readonly unknown[] }) => {
    queryCalls.push(options);
    return options.queryKey?.includes('activities') ? queryResults.activities : queryResults.overview;
  }
}));

vi.mock('react-router-dom', () => ({
  Link: ({ children }: { children: React.ReactNode }) => <a>{children}</a>,
  useParams: () => ({ projectId: 'project-1' })
}));

vi.mock('../../core/auth/usePermission', () => ({
  usePermission: () => ({ hasPermission: permissionState.canViewActivities })
}));

vi.mock('../../features/project-management/state/projectManagementWorkspaceScope', () => ({
  useProjectManagementWorkspaceScope: () => ({ isAvailable: true, tenantId: 'tenant-a', appCode: 'SYSTEM' })
}));

vi.mock('../../shared/responsive/ResponsivePage', () => ({
  ResponsivePage: ({ children }: { children: React.ReactNode }) => <main>{children}</main>
}));

vi.mock('../../shared/status/Page403', () => ({ Page403: () => <p>403</p> }));
vi.mock('../../shared/status/PageError', () => ({ PageError: ({ description }: { description?: React.ReactNode }) => <p>{description}</p> }));
vi.mock('../../shared/status/PageLoading', () => ({ PageLoading: () => <p>loading</p> }));

function overviewQueryResult() {
  return {
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
  };
}

function activityQueryResult(data: unknown, isError = false) {
  return {
    data,
    error: isError ? new Error('activity unavailable') : null,
    isError,
    isLoading: false,
    refetch: vi.fn()
  };
}

describe('ProjectManagementOverviewPage', () => {
  afterEach(cleanup);

  beforeEach(() => {
    queryCalls.length = 0;
    permissionState.canViewActivities = false;
    queryResults.overview = overviewQueryResult();
    queryResults.activities = activityQueryResult({ data: { total: 0, items: [] } });
  });

  it('keeps the overview available and does not enable the activity query without audit:view', () => {
    render(<ProjectManagementOverviewPage />);

    expect(screen.getByText('整体进度')).toBeTruthy();
    expect(screen.getByText('当前账号无查看项目活动的权限。')).toBeTruthy();
    expect(queryCalls).toHaveLength(4);
    expect(queryCalls[0]?.enabled).toBe(true);
    expect(queryCalls.find((query) => query.queryKey?.includes('activities'))?.enabled).toBe(false);
  });

  it('keeps the overview available when an authorized activity request fails', () => {
    permissionState.canViewActivities = true;
    queryResults.activities = activityQueryResult(undefined, true);

    render(<ProjectManagementOverviewPage />);

    expect(screen.getByText('整体进度')).toBeTruthy();
    expect(screen.getByText('项目活动暂时无法加载。')).toBeTruthy();
    expect(queryCalls.find((query) => query.queryKey?.includes('activities'))?.enabled).toBe(true);
  });

  it('renders items from the paged activity response instead of treating the response as an array', () => {
    permissionState.canViewActivities = true;
    queryResults.activities = activityQueryResult({
      data: {
        total: 21,
        items: [{
          activityType: 'task.updated',
          aggregateId: 'task-1',
          aggregateType: 'Task',
          actorUserId: 'user-a',
          createdTime: '2026-07-19T00:00:00.000Z',
          id: 'activity-1',
          projectId: 'project-1',
          summary: '更新任务 A',
          traceId: 'trace-activity-1'
        }]
      }
    });

    render(<ProjectManagementOverviewPage />);

    expect(screen.getByText('更新任务 A')).toBeTruthy();
    expect(screen.queryByText('暂无活动。项目和任务发生可审计变更后会在这里显示。')).toBeNull();
  });
});
