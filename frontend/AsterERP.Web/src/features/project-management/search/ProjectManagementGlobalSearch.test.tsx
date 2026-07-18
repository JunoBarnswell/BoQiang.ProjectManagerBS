// @vitest-environment jsdom

import { act, cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { ProjectManagementGlobalSearch } from './ProjectManagementGlobalSearch';

const navigate = vi.hoisted(() => vi.fn());

vi.mock('@tanstack/react-query', () => ({
  useQuery: (options: { queryKey?: readonly unknown[] }) => {
    if (options.queryKey?.includes('search-index-status')) {
      return {
        data: { data: { status: 'Rebuilding', mode: 'rebuild', appliedSequenceNo: 4, targetSequenceNo: 7, documentCount: 12, failureCount: 0, lastError: null, operationId: 'op-1', startedTime: null, completedTime: null, updatedTime: '2026-07-19T00:00:00.000Z' } },
        error: null,
        isError: false,
        isLoading: false,
      };
    }
    return {
      data: {
        data: {
          projects: [item('project', 'Alpha 项目')],
          tasks: [item('task', 'Alpha 任务')],
          milestones: [],
          labels: [],
          members: [],
          comments: [],
        },
      },
      error: null,
      isError: false,
      isLoading: false,
    };
  },
}));
vi.mock('react-router-dom', () => ({ useNavigate: () => navigate }));
vi.mock('../../../api/project-management/projectManagement.api', () => ({
  getProjectManagementSearchIndexStatus: vi.fn(),
  searchProjectManagement: vi.fn(),
}));
vi.mock('../../../features/project-management/state/projectManagementWorkspaceScope', () => ({
  useProjectManagementWorkspaceScope: () => ({ isAvailable: true, tenantId: 'tenant-a', appCode: 'SYSTEM' }),
}));

function item(resultType: 'project' | 'task', title: string) {
  return { id: `${resultType}-1`, projectId: 'project-1', resultType, summary: `${title}摘要`, targetRoute: `/projects/project-1/${resultType === 'task' ? 'tasks?taskId=task-1' : 'overview'}`, title, updatedTime: '2026-07-19T00:00:00.000Z' };
}

describe('ProjectManagementGlobalSearch', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    navigate.mockReset();
  });

  afterEach(() => {
    cleanup();
    vi.useRealTimers();
  });

  it('opens with Ctrl+F, debounces input, groups results, and shows index lag', () => {
    render(<ProjectManagementGlobalSearch />);
    fireEvent.keyDown(window, { key: 'f', ctrlKey: true });

    const input = screen.getByLabelText('全局搜索关键字');
    fireEvent.change(input, { target: { value: 'alpha' } });
    expect(screen.queryByText('Alpha 项目')).toBeNull();
    act(() => vi.advanceTimersByTime(300));

    expect(screen.getByRole('dialog')).toBeTruthy();
    expect(screen.getByText('项目（1）')).toBeTruthy();
    expect(screen.getByText('任务（1）')).toBeTruthy();
    expect(screen.getByText('全文索引：重建中 · 待处理 3 条变更')).toBeTruthy();
  });

  it('moves with arrows, opens the selected authorized target, and closes with Escape', () => {
    render(<ProjectManagementGlobalSearch />);
    fireEvent.click(screen.getByRole('button', { name: '打开全局搜索' }));
    const input = screen.getByLabelText('全局搜索关键字');
    fireEvent.change(input, { target: { value: 'alpha' } });
    act(() => vi.advanceTimersByTime(300));

    fireEvent.keyDown(input, { key: 'ArrowDown' });
    fireEvent.keyDown(input, { key: 'Enter' });
    expect(navigate).toHaveBeenCalledWith('/platform/project-management/projects/project-1/tasks?taskId=task-1');
    expect(screen.queryByRole('dialog')).toBeNull();

    fireEvent.click(screen.getByRole('button', { name: '打开全局搜索' }));
    fireEvent.keyDown(screen.getByLabelText('全局搜索关键字'), { key: 'Escape' });
    expect(screen.queryByRole('dialog')).toBeNull();
  });
});
