// @vitest-environment jsdom

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ProjectManagementPageStateView } from './ProjectManagementPageState';

describe('ProjectManagementPageStateView', () => {
  it.each([
    ['loading', '正在加载项目管理'],
    ['empty', '还没有项目'],
    ['error', '项目管理加载失败'],
    ['forbidden', '无权访问项目管理']
  ] as const)('renders the %s state', (state, title) => {
    render(<ProjectManagementPageStateView state={state} />);
    expect(screen.getByText(title)).toBeTruthy();
  });
});
