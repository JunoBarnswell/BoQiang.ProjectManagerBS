// @vitest-environment jsdom

import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';

import { ProjectManagementActivityTimeline } from './ProjectManagementActivityTimeline';

describe('ProjectManagementActivityTimeline', () => {
  afterEach(cleanup);

  it('renders the items from a paged activity response', () => {
    render(
      <ProjectManagementActivityTimeline
        canView
        isError={false}
        isLoading={false}
        page={{
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
        }}
        pageSize={20}
      />
    );

    expect(screen.getByText('更新任务 A')).toBeTruthy();
    expect(screen.getByText('仅展示当前项目范围内最近 20 条活动。')).toBeTruthy();
  });

  it.each([
    ['loading', { canView: true, isError: false, isLoading: true }, '项目活动加载中…'],
    ['empty', { canView: true, isError: false, isLoading: false }, '暂无活动。项目和任务发生可审计变更后会在这里显示。'],
    ['error', { canView: true, isError: true, isLoading: false }, '项目活动暂时无法加载。'],
    ['forbidden', { canView: false, isError: false, isLoading: false }, '当前账号无查看项目活动的权限。']
  ])('renders the %s activity state', (_state, props, expectedText) => {
    render(<ProjectManagementActivityTimeline {...props} page={{ total: 0, items: [] }} pageSize={20} />);

    expect(screen.getByText(expectedText)).toBeTruthy();
  });
});
