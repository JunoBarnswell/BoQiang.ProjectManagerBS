// @vitest-environment jsdom

import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { ProjectManagementAuditPage } from './ProjectManagementAuditPage';

const operationsRefetch = vi.hoisted(() => vi.fn());
const clearTracking = vi.hoisted(() => vi.fn());
const queryCount = vi.hoisted(() => ({ value: 0 }));

vi.mock('@tanstack/react-query', () => ({
  useQuery: () => {
    queryCount.value += 1;
    return queryCount.value % 2 === 1
      ? { data: { data: { items: [], total: 0 } }, isError: false, isLoading: false, refetch: vi.fn() }
      : { data: { data: { items: [{ actorUserId: 'user-a', errorMessage: null, id: 'operation-1', operationType: 'maintenance.workspace-validation', startedTime: '2026-01-01T00:00:00Z', status: 'Pending', traceId: 'trace-1' }], total: 1 } }, isError: false, isLoading: false, refetch: operationsRefetch };
  },
  useQueryClient: () => ({ invalidateQueries: vi.fn() })
}));

vi.mock('react-router-dom', () => ({ Link: ({ children }: { children: React.ReactNode }) => <a>{children}</a> }));
vi.mock('../../api/project-management/projectManagement.api', () => ({
  exportProjectManagementAudit: vi.fn(),
  getProjectManagementAudit: vi.fn(),
  getProjectManagementOperations: vi.fn(),
  startProjectManagementWorkspaceValidation: vi.fn()
}));
vi.mock('../../core/query/useApiMutation', () => ({ useApiMutation: () => ({ isPending: false, mutate: vi.fn() }) }));
vi.mock('../../core/state/authStore', () => ({ useAuthStore: (selector: (state: { user: { userId: string } }) => unknown) => selector({ user: { userId: 'user-a' } }) }));
vi.mock('../../features/project-management/state/projectManagementWorkspaceScope', () => ({ useProjectManagementWorkspaceScope: () => ({ appCode: 'SYSTEM', isAvailable: true, tenantId: 'tenant-a' }) }));
vi.mock('../../features/project-management/state/projectManagementOperationTracking', () => ({
  clearProjectManagementOperationTracking: clearTracking,
  getProjectManagementOperationTrackingKey: () => 'operation-tracking-key',
  readProjectManagementOperationTracking: () => 'operation-1',
  writeProjectManagementOperationTracking: vi.fn()
}));
vi.mock('../../features/project-management/state/projectManagementPlatformRoutes', () => ({ toProjectManagementPlatformRoute: () => '/platform/project-management' }));
vi.mock('../../features/project-management/components/ProjectManagementOperationProgress', () => ({
  ProjectManagementOperationProgress: ({ onTrackingEnded }: { onTrackingEnded?: () => void }) => <button type="button" onClick={onTrackingEnded}>完成跟踪</button>
}));
vi.mock('../../shared/auth/PermissionButton', () => ({ PermissionButton: ({ children, ...props }: React.ButtonHTMLAttributes<HTMLButtonElement>) => <button {...props}>{children}</button> }));
vi.mock('../../shared/auth/PermissionGuard', () => ({ PermissionGuard: ({ children }: { children: React.ReactNode }) => <>{children}</> }));
vi.mock('../../shared/feedback/useMessage', () => ({ useMessage: () => ({ error: vi.fn(), success: vi.fn() }) }));
vi.mock('../../shared/responsive/ResponsivePage', () => ({ ResponsivePage: ({ children }: { children: React.ReactNode }) => <main>{children}</main> }));
vi.mock('../../shared/status/Page403', () => ({ Page403: () => <p>403</p> }));
vi.mock('../../shared/status/PageError', () => ({ PageError: ({ description }: { description?: React.ReactNode }) => <p>{description}</p> }));
vi.mock('../../shared/status/PageLoading', () => ({ PageLoading: () => <p>loading</p> }));

describe('ProjectManagementAuditPage', () => {
  beforeEach(() => {
    clearTracking.mockClear();
    operationsRefetch.mockClear();
    queryCount.value = 0;
  });

  afterEach(cleanup);

  it('refreshes the operation list when tracked progress reaches a terminal state', () => {
    render(<ProjectManagementAuditPage />);

    screen.getByRole('button', { name: '完成跟踪' }).click();

    expect(clearTracking).toHaveBeenCalledWith('operation-tracking-key');
    expect(operationsRefetch).toHaveBeenCalledOnce();
  });
});
