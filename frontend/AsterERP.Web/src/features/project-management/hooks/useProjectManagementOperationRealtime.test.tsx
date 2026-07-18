// @vitest-environment jsdom

import { act, renderHook } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

const state = vi.hoisted(() => ({
  connection: null as any,
  invalidateQueries: vi.fn(),
  queryClient: null as any,
}));

state.queryClient = { invalidateQueries: state.invalidateQueries, setQueryData: vi.fn() };
vi.mock('@tanstack/react-query', () => ({ useQueryClient: () => state.queryClient }));
vi.mock('../../../core/http/tokenStorage', () => ({ getAccessToken: () => 'token' }));
vi.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: class {
    withUrl() { return this; }
    withAutomaticReconnect() { return this; }
    configureLogging() { return this; }
    build() {
      const handlers: Record<string, (...args: any[]) => void> = {};
      state.connection = {
        handlers,
        on: vi.fn((name: string, callback: (...args: any[]) => void) => { handlers[name] = callback; }),
        onreconnecting: vi.fn((callback: () => void) => { handlers.reconnecting = callback; }),
        onreconnected: vi.fn((callback: () => void) => { handlers.reconnected = callback; }),
        onclose: vi.fn((callback: () => void) => { handlers.close = callback; }),
        off: vi.fn(), start: vi.fn(() => Promise.resolve()), stop: vi.fn(() => Promise.resolve()),
      };
      return state.connection;
    }
  },
  LogLevel: { Warning: 2 },
}));

import { useProjectManagementOperationRealtime } from './useProjectManagementOperationRealtime';

describe('useProjectManagementOperationRealtime', () => {
  it('exposes disconnect/reconnect state and invalidates the operation after reconnection', async () => {
    const scope = { tenantId: 'tenant-a', appCode: 'MES', isAvailable: true };
    const { result } = renderHook(() => useProjectManagementOperationRealtime('/hubs/system-notification', scope, 'operation-1'));
    await act(async () => undefined);
    expect(result.current.connectionState).toBe('connected');
    act(() => state.connection.handlers.reconnecting());
    expect(result.current.connectionState).toBe('reconnecting');
    act(() => state.connection.handlers.close());
    expect(result.current.connectionState).toBe('disconnected');
    act(() => state.connection.handlers.reconnected());
    expect(result.current.connectionState).toBe('connected');
    expect(state.invalidateQueries).toHaveBeenCalledOnce();
  });
});
