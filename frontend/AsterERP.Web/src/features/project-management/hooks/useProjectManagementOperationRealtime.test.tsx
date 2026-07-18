// @vitest-environment jsdom

import { act, renderHook } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

type HubHandler = (...args: unknown[]) => void;

interface FakeHubConnection {
  handlers: Record<string, HubHandler>;
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
  invalidateQueries: vi.fn(),
  queryClient: { invalidateQueries: vi.fn(), setQueryData: vi.fn() },
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
        on: vi.fn((name: string, callback: HubHandler) => { handlers[name] = callback; }),
        onreconnecting: vi.fn((callback: () => void) => { handlers.reconnecting = callback; }),
        onreconnected: vi.fn((callback: () => void) => { handlers.reconnected = callback; }),
        onclose: vi.fn((callback: () => void) => { handlers.close = callback; }),
        off: vi.fn(), start: vi.fn(() => { state.connectionState = 'Connected'; return Promise.resolve(); }), stop: vi.fn(() => Promise.resolve()),
      };
      return state.connection;
    }
  },
  HubConnectionState: { Connected: 'Connected', Connecting: 'Connecting', Disconnected: 'Disconnected', Reconnecting: 'Reconnecting' },
  LogLevel: { Warning: 2 },
}));

import { useProjectManagementOperationRealtime } from './useProjectManagementOperationRealtime';

describe('useProjectManagementOperationRealtime', () => {
  it('exposes disconnect/reconnect state and invalidates the operation after reconnection', async () => {
    const scope = { tenantId: 'tenant-a', appCode: 'MES', isAvailable: true };
    const { result, unmount } = renderHook(() => useProjectManagementOperationRealtime('/hubs/system-notification', scope, 'operation-1'));
    await act(async () => undefined);
    expect(result.current.connectionState).toBe('connected');
    act(() => state.connection.handlers.reconnecting());
    expect(result.current.connectionState).toBe('reconnecting');
    act(() => state.connection.handlers.close());
    expect(result.current.connectionState).toBe('disconnected');
    await act(async () => state.connection.handlers.reconnected());
    expect(result.current.connectionState).toBe('connected');
    expect(state.invalidateQueries).toHaveBeenCalledOnce();
    unmount();
  });
});
