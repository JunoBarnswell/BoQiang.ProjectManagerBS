// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { ProjectManagementOverviewPage } from './ProjectManagementOverviewPage';

const queryCalls = vi.hoisted(() => [] as Array<{ enabled?: boolean; queryKey?: readonly unknown[] }>);
const queryResults = vi.hoisted(() => ({ activities: {} as Record<string, unknown>, overview: {} as Record<string, unknown> }));
const routeState = vi.hoisted(() => ({ search: '', navigate: vi.fn(), setSearchParams: vi.fn() }));

vi.mock('@tanstack/react-query', () => ({
  QueryClient: class QueryClient {},
  useQuery: (options: { enabled?: boolean; queryKey?: readonly unknown[] }) => {
    queryCalls.push(options);
    return options.queryKey?.includes('activities') ? queryResults.activities : queryResults.overview;
  },
  useQueryClient: () => ({ invalidateQueries: vi.fn() }),
  useMutation: () => ({ mutate: vi.fn(), isPending: false })
}));

vi.mock('react-router-dom', () => ({
  useParams: () => ({ projectId: 'project-1' }),
  useNavigate: () => routeState.navigate,
  useSearchParams: () => [new URLSearchParams(routeState.search), routeState.setSearchParams]
}));

vi.mock('../../core/i18n/I18nProvider', () => ({
  useI18n: () => ({ translate: (key: string) => key })
}));
vi.mock('../../core/auth/usePermission', () => ({ usePermission: () => ({ hasPermission: true }) }));
vi.mock('../../features/project-management/state/projectManagementWorkspaceScope', () => ({ useProjectManagementWorkspaceScope: () => ({ isAvailable: true, tenantId: 'tenant-a', appCode: 'SYSTEM' }) }));
vi.mock('../../shared/status/Page403', () => ({ Page403: () => <p>403</p> }));
vi.mock('../../shared/status/PageError', () => ({ PageError: ({ description }: { description?: React.ReactNode }) => <p>{description}</p> }));
vi.mock('../../shared/status/PageLoading', () => ({ PageLoading: () => <p>loading</p> }));
vi.mock('../../shared/feedback/useMessage', () => ({ useMessage: () => ({ success: vi.fn(), error: vi.fn() }) }));
vi.mock('../../shared/feedback/useConfirm', () => ({ useConfirm: () => vi.fn() }));

function overviewQueryResult() {
  return { data: { data: { items: [{ blockedTaskCount: 0, milestoneCount: 0, memberCount: 1, milestones: [], people: [], overdueTaskCount: 0, project: { description: '说明', projectCode: 'PM-001', projectName: '项目 A', priority: 'Medium', status: 'Active', ownerDisplayName: '负责人', updatedTime: '2026-07-19T00:00:00.000Z', versionNo: 1 }, taskCount: 2, taskProgressPercent: 50 }] } }, error: null, isError: false, isLoading: false, refetch: vi.fn() };
}

function activityQueryResult() {
  return { data: { data: { total: 1, items: [{ id: 'activity-1', summary: '更新任务 A', createdTime: '2026-07-19T00:00:00.000Z' }] } }, error: null, isError: false, isLoading: false, refetch: vi.fn() };
}

describe('ProjectManagementOverviewPage', () => {
  afterEach(cleanup);

  beforeEach(() => {
    queryCalls.length = 0;
    routeState.search = '';
    routeState.navigate.mockReset();
    routeState.setSearchParams.mockReset();
    queryResults.overview = overviewQueryResult();
    queryResults.activities = activityQueryResult();
  });

  it('renders the direct-replacement overview and keeps activity query disabled on the overview tab', () => {
    render(<ProjectManagementOverviewPage />);

    expect(screen.getAllByText('项目 A').length).toBeGreaterThan(0);
    expect(screen.getByText('projectManagement.home.overview')).toBeTruthy();
    expect(queryCalls.find(query => query.queryKey?.includes('activities'))?.enabled).toBe(false);
  });

  it('loads the activity context from the URL-selected tab', () => {
    routeState.search = 'mainTab=activity&contextTab=activity&sourceView=recent';
    render(<ProjectManagementOverviewPage />);

    expect(screen.getAllByText('更新任务 A').length).toBeGreaterThan(0);
    expect(queryCalls.find(query => query.queryKey?.includes('activities'))?.enabled).toBe(true);
  });

  it('writes the main tab state through the existing route', () => {
    render(<ProjectManagementOverviewPage />);
    fireEvent.click(screen.getAllByText('projectManagement.home.activity')[0]);
    expect(routeState.setSearchParams).toHaveBeenCalled();
  });

  it('opens an inline property editor from the context panel', () => {
    render(<ProjectManagementOverviewPage />);
    fireEvent.click(screen.getByText('projectManagement.home.priority.Medium'));
    expect(screen.getByLabelText('projectManagement.home.priority')).toBeTruthy();
  });
});
