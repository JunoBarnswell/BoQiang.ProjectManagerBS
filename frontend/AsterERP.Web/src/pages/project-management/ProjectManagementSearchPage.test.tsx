// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { ProjectManagementSearchPage } from './ProjectManagementSearchPage';

const queryCalls = vi.hoisted(() => [] as Array<{ queryKey?: readonly unknown[]; enabled?: boolean }>);

vi.mock('@tanstack/react-query', () => ({
  useQuery: (options: { queryKey?: readonly unknown[]; enabled?: boolean }) => {
    queryCalls.push(options);
    return {
      data: {
        data: {
          projects: [item('project', '项目结果')],
          tasks: [item('task', '任务结果')],
          milestones: [item('milestone', '里程碑结果')],
          labels: [item('label', '标签结果')],
          members: [item('member', '成员结果')],
          comments: [item('comment', '评论结果')],
        },
      },
      error: null,
      isError: false,
      isLoading: false,
      refetch: vi.fn(),
    };
  },
}));

vi.mock('react-router-dom', () => ({ useNavigate: () => vi.fn() }));
vi.mock('../../api/project-management/projectManagement.api', () => ({ searchProjectManagement: vi.fn() }));
vi.mock('../../features/project-management/state/projectManagementWorkspaceScope', () => ({
  useProjectManagementWorkspaceScope: () => ({ isAvailable: true, tenantId: 'tenant-a', appCode: 'SYSTEM' }),
}));
vi.mock('../../shared/responsive/ResponsivePage', () => ({
  ResponsivePage: ({ children, toolbar }: { children: React.ReactNode; toolbar?: React.ReactNode }) => <main>{toolbar}{children}</main>,
}));
vi.mock('../../shared/status/Page403', () => ({ Page403: () => <p>403</p> }));
vi.mock('../../shared/status/PageError', () => ({ PageError: ({ description }: { description?: React.ReactNode }) => <p>{description}</p> }));
vi.mock('../../shared/status/PageLoading', () => ({ PageLoading: () => <p>loading</p> }));

function item(resultType: 'project' | 'task' | 'milestone' | 'label' | 'member' | 'comment', title: string) {
  return { id: `${resultType}-1`, projectId: 'project-1', resultType, summary: `${title}摘要`, targetRoute: '/projects/project-1/overview', title, updatedTime: '2026-07-19T00:00:00.000Z' };
}

describe('ProjectManagementSearchPage', () => {
  afterEach(cleanup);

  beforeEach(() => {
    queryCalls.length = 0;
  });

  it('renders all structured-search result groups and enables the authorized workspace query', () => {
    render(<ProjectManagementSearchPage />);
    fireEvent.change(screen.getByLabelText('搜索关键字'), { target: { value: '结果' } });
    fireEvent.click(screen.getByRole('button', { name: '搜索' }));

    for (const title of ['项目结果', '任务结果', '里程碑结果', '标签结果', '成员结果', '评论结果']) {
      expect(screen.getByText(title)).toBeTruthy();
    }
    expect(queryCalls.length).toBeGreaterThanOrEqual(2);
    const authorizedSearchCall = queryCalls.find((call) => call.enabled && call.queryKey?.includes('search'));
    expect(authorizedSearchCall?.enabled).toBe(true);
    expect(authorizedSearchCall?.queryKey).toContain('search');
  });
});
