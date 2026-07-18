// @vitest-environment jsdom

import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { TaskActivityTimeline } from './TaskActivityTimeline';

describe('TaskActivityTimeline', () => {
  afterEach(cleanup);

  it('renders filters, detail jump and a safe placeholder for deleted associations', () => {
    render(
      <MemoryRouter>
        <TaskActivityTimeline
          canView
          isError={false}
          isLoading={false}
          onQueryChange={vi.fn()}
          query={{ pageIndex: 1, pageSize: 20 }}
          page={{
            total: 2,
            items: [
              {
                activityType: 'task.updated',
                actorUserId: 'operator',
                createdTime: '2026-07-19T00:00:00.000Z',
                id: 'activity-1',
                isTargetDeleted: false,
                projectId: 'project-a',
                summary: '更新任务',
                targetRoute: '/projects/project-a/tasks?selectedTaskId=task-a',
                traceId: 'trace-1',
              },
              {
                activityType: 'task.comment.deleted',
                actorUserId: 'operator',
                createdTime: '2026-07-18T00:00:00.000Z',
                id: 'activity-2',
                isTargetDeleted: true,
                projectId: 'project-a',
                summary: '删除评论',
                traceId: 'trace-2',
              },
            ],
          }}
        />
      </MemoryRouter>,
    );

    expect(screen.getByLabelText('活动类型筛选')).toBeTruthy();
    expect(screen.getByText('更新任务')).toBeTruthy();
    expect(screen.getByRole('link', { name: '查看任务详情' })).toBeTruthy();
    expect(screen.getByText('关联已删除')).toBeTruthy();
    expect(screen.queryAllByRole('link', { name: '查看任务详情' })).toHaveLength(1);
  });
});
