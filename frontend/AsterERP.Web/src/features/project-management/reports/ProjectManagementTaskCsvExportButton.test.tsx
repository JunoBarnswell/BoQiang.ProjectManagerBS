// @vitest-environment jsdom

import { render, screen } from '@testing-library/react';
import type { ReactNode } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';

import { ProjectManagementTaskCsvExportButton } from './ProjectManagementTaskCsvExportButton';

vi.mock('../../../api/project-management/projectManagement.api', () => ({
  exportProjectManagementTasksCsv: vi.fn(),
}));

vi.mock('../../../core/query/useApiMutation', () => ({
  useApiMutation: () => ({ isPending: false, mutate: vi.fn() }),
}));

vi.mock('../../../shared/auth/PermissionButton', () => ({
  PermissionButton: ({ children, onClick }: { children: ReactNode; onClick: () => void }) => <button onClick={onClick}>{children}</button>,
}));

vi.mock('../../../shared/feedback/useMessage', () => ({
  useMessage: () => ({ error: vi.fn(), success: vi.fn() }),
}));

describe('ProjectManagementTaskCsvExportButton', () => {
  it('renders a permission-gated action for the current task filters', () => {
    render(
      <MemoryRouter initialEntries={['/project-management/projects/project-a/tasks?q=release&status=Todo']}>
        <ProjectManagementTaskCsvExportButton filter={{ labelIds: ['label-a'], matchMode: 'Any' }} projectId="project-a" />
      </MemoryRouter>,
    );

    expect(screen.getByRole('button', { name: '导出当前筛选 CSV' })).toBeTruthy();
  });
});
