// @vitest-environment jsdom

import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { ProjectManagementOperationProgress } from './ProjectManagementOperationProgress';
import { HttpError } from '../../../core/http/httpError';

const mutationState = vi.hoisted(() => ({ mutate: vi.fn() }));
const queryState = vi.hoisted(() => ({ value: {
  data: { data: { completedTime: undefined, errorMessage: undefined, id: 'operation-1', impactJson: '{}', isCancellationRequested: false, operationType: 'maintenance.workspace-validation', phase: 'ValidatingTasks', progressPercent: 40, startedTime: '2026-01-01T00:00:00Z', status: 'Running', traceId: 'trace-1' } },
  isError: false, isFetching: false, isLoading: false, refetch: vi.fn()
} as any }));

vi.mock('@tanstack/react-query', () => ({
  useQuery: () => queryState.value,
  useQueryClient: () => ({ setQueryData: vi.fn() })
}));

vi.mock('../../../core/query/useApiMutation', () => ({
  useApiMutation: () => ({ isPending: false, mutate: mutationState.mutate })
}));

vi.mock('../../../shared/auth/PermissionButton', () => ({
  PermissionButton: ({ children, onClick }: { children: React.ReactNode; onClick: () => void }) => <button type="button" onClick={onClick}>{children}</button>
}));

vi.mock('../../../shared/feedback/useMessage', () => ({ useMessage: () => ({ error: vi.fn() }) }));
vi.mock('../hooks/useProjectManagementOperationRealtime', () => ({ useProjectManagementOperationRealtime: () => ({ connectionState: 'connected' }) }));
vi.mock('../state/projectManagementWorkspaceScope', () => ({ useProjectManagementWorkspaceScope: () => ({ appCode: 'MES', isAvailable: true, tenantId: 'tenant-a' }) }));

describe('ProjectManagementOperationProgress', () => {
  afterEach(() => cleanup());
  it('renders persistent phase progress and exposes cancellation for a running operation', () => {
    render(<ProjectManagementOperationProgress operationId="operation-1" />);

    expect(screen.getByText('ValidatingTasks · 40%')).toBeTruthy();
    expect(screen.getByRole('progressbar', { name: '长任务进度' }).getAttribute('value')).toBe('40');
    screen.getByRole('button', { name: '取消任务' }).click();
    expect(mutationState.mutate).toHaveBeenCalledOnce();
  });

  it('renders differentiated retry alerts for authorization, missing, and network failures', () => {
    for (const [error, expected] of [[new HttpError({ message: 'forbidden', status: 403 }), '无权查看该长任务。'], [new HttpError({ message: 'missing', status: 404 }), '长任务不存在或已失效。'], [new Error('offline'), '长任务状态暂时无法加载。']] as const) {
      queryState.value = { data: undefined, isError: true, isFetching: false, isLoading: false, error, refetch: vi.fn() };
      const { unmount } = render(<ProjectManagementOperationProgress operationId="operation-1" />);
      expect(screen.getByRole('alert').textContent).toContain(expected);
      screen.getByRole('button', { name: '重试' }).click();
      expect(queryState.value.refetch).toHaveBeenCalledOnce();
      unmount();
    }
  });

  it('marks a refetching operation busy and clears completed tracking', () => {
    const onTrackingEnded = vi.fn();
    queryState.value = { data: { data: { completedTime: '2026-01-01T00:01:00Z', errorMessage: undefined, id: 'operation-1', impactJson: '{}', isCancellationRequested: false, operationType: 'maintenance.workspace-validation', phase: 'Completed', progressPercent: 100, startedTime: '2026-01-01T00:00:00Z', status: 'Succeeded', traceId: 'trace-1' } }, isError: false, isFetching: true, isLoading: false, refetch: vi.fn() };
    render(<ProjectManagementOperationProgress operationId="operation-1" onTrackingEnded={onTrackingEnded} />);
    expect(screen.getByLabelText('长任务进度').closest('section')?.getAttribute('aria-busy')).toBe('true');
    expect(onTrackingEnded).toHaveBeenCalledOnce();
  });

  it('exposes loading as an accessible status', () => {
    queryState.value = { data: undefined, isError: false, isFetching: false, isLoading: true, refetch: vi.fn() };
    render(<ProjectManagementOperationProgress operationId="operation-1" />);
    expect(screen.getByRole('status').textContent).toContain('正在加载长任务状态');
  });

  it('ends persisted tracking when the stored operation is no longer found', () => {
    const onTrackingEnded = vi.fn();
    queryState.value = { data: undefined, isError: true, isFetching: false, isLoading: false, error: new HttpError({ message: 'missing', status: 404 }), refetch: vi.fn() };
    render(<ProjectManagementOperationProgress operationId="stale-operation" onTrackingEnded={onTrackingEnded} />);
    expect(onTrackingEnded).toHaveBeenCalledOnce();
  });
});
