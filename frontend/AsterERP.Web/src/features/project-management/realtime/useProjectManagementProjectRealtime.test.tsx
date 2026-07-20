// @vitest-environment jsdom

import { act, renderHook } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

type HubHandler = (...args: unknown[]) => void;

interface FakeHubConnection {
  handlers: Record<string, HubHandler>;
  invoke: ReturnType<typeof vi.fn>;
  off: ReturnType<typeof vi.fn>;
  on: ReturnType<typeof vi.fn>;
  onclose: ReturnType<typeof vi.fn>;
  onreconnected: ReturnType<typeof vi.fn>;
  onreconnecting: ReturnType<typeof vi.fn>;
  readonly state: string;
  start: ReturnType<typeof vi.fn>;
  stop: ReturnType<typeof vi.fn>;
}

const state = vi.hoisted(() => ({
  connection: null as unknown as FakeHubConnection,
  connectionState: 'Disconnected',
  invalidateQueries: vi.fn().mockResolvedValue(undefined),
  queryClient: { invalidateQueries: vi.fn(), setQueriesData: vi.fn() },
}));

state.queryClient.invalidateQueries = state.invalidateQueries;
vi.mock('@tanstack/react-query', () => ({ useQueryClient: () => state.queryClient }));
vi.mock('../../../core/http/tokenStorage', () => ({ getAccessToken: () => 'token' }));
vi.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: class {
    withUrl() { return this; }
    withAutomaticReconnect() { return this; }
    configureLogging() { return this; }
    build() {
      const handlers: Record<string, HubHandler> = {};
      state.connection = {
        handlers,
        get state() { return state.connectionState; },
        invoke: vi.fn(() => Promise.resolve()),
        off: vi.fn(),
        on: vi.fn((name: string, callback: HubHandler) => { handlers[name] = callback; }),
        onclose: vi.fn((callback: () => void) => { handlers.close = callback; }),
        onreconnected: vi.fn((callback: () => void) => { handlers.reconnected = callback; }),
        onreconnecting: vi.fn((callback: () => void) => { handlers.reconnecting = callback; }),
        start: vi.fn(() => { state.connectionState = 'Connected'; return Promise.resolve(); }),
        stop: vi.fn(() => Promise.resolve()),
      };
      return state.connection;
    }
  },
  HubConnectionState: { Connected: 'Connected', Connecting: 'Connecting', Disconnected: 'Disconnected', Reconnecting: 'Reconnecting' },
  LogLevel: { Warning: 2 },
}));

import { useProjectManagementProjectRealtime } from './useProjectManagementProjectRealtime';

describe('useProjectManagementProjectRealtime', () => {
  it('does not resubscribe when only onAccessRevoked identity changes', async () => {
    vi.useFakeTimers();
    const scope = { tenantId: 'tenant-a', appCode: 'SYSTEM', isAvailable: true };
    const firstRevoked = vi.fn();
    const { rerender, unmount } = renderHook(
      ({ onAccessRevoked }) => useProjectManagementProjectRealtime({
        enabled: true,
        onAccessRevoked,
        projectId: 'project-a',
        signalRUrl: '/hubs/system-notification',
        scope,
      }),
      { initialProps: { onAccessRevoked: firstRevoked } },
    );

    await act(async () => { await vi.runAllTimersAsync(); });
    expect(state.invalidateQueries).toHaveBeenCalled();
    state.invalidateQueries.mockClear();

    const secondRevoked = vi.fn();
    rerender({ onAccessRevoked: secondRevoked });
    await act(async () => { await vi.advanceTimersByTimeAsync(200); });
    expect(state.invalidateQueries).not.toHaveBeenCalled();

    unmount();
    vi.useRealTimers();
  });

  it('batches structural invalidation events and still reconciles after reconnect', async () => {
    vi.useFakeTimers();
    const scope = { tenantId: 'tenant-a', appCode: 'SYSTEM', isAvailable: true };
    const { unmount } = renderHook(() => useProjectManagementProjectRealtime({
      enabled: true,
      projectId: 'project-a',
      signalRUrl: '/hubs/system-notification',
      scope,
    }));

    await act(async () => { await vi.runAllTimersAsync(); });
    state.invalidateQueries.mockClear();

    await act(async () => {
      state.connection.handlers.ProjectManagementInvalidated({
        aggregateId: 'task-a',
        aggregateType: 'Task',
        aggregateVersion: 2,
        changedFields: ['status'],
        eventType: 'updated',
        patch: { status: 'InProgress' },
        projectId: 'project-a',
        projectSequence: 2,
      });
      state.connection.handlers.ProjectManagementInvalidated({
        aggregateId: 'task-a',
        aggregateType: 'Task',
        aggregateVersion: 3,
        changedFields: ['status'],
        eventType: 'updated',
        patch: { status: 'Done' },
        projectId: 'project-a',
        projectSequence: 3,
      });
      await vi.advanceTimersByTimeAsync(120);
    });

    expect(state.invalidateQueries).toHaveBeenCalledTimes(5);
    state.invalidateQueries.mockClear();

    await act(async () => {
      await state.connection.handlers.reconnecting();
      await state.connection.handlers.reconnected();
      await vi.advanceTimersByTimeAsync(120);
    });
    expect(state.invalidateQueries).toHaveBeenCalledTimes(5);

    unmount();
    vi.useRealTimers();
  });
});
