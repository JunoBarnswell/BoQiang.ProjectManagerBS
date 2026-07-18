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
  build: vi.fn(),
  connection: null as unknown as FakeHubConnection,
  connectionState: 'Disconnected',
}));

vi.mock('../../../core/http/tokenStorage', () => ({ getAccessToken: () => 'token' }));
vi.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: class {
    withUrl() { return this; }
    withAutomaticReconnect() { return this; }
    configureLogging() { return this; }
    build() {
      state.build();
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

import { acquireProjectManagementHubConnection } from './projectManagementHubConnection';

describe('projectManagementHubConnection', () => {
  it('reuses one connection and rejoins referenced projects before notifying subscribers', async () => {
    const scope = { tenantId: 'tenant-a', appCode: 'SYSTEM', isAvailable: true };
    const first = acquireProjectManagementHubConnection('/hubs/system-notification', scope)!;
    const second = acquireProjectManagementHubConnection('/hubs/system-notification', scope)!;
    const joined = vi.fn();
    const leaveProject = first.subscribeProject('project-1', joined);

    await Promise.resolve();
    await Promise.resolve();

    expect(state.build).toHaveBeenCalledOnce();
    expect(state.connection.invoke).toHaveBeenCalledWith('JoinProjectManagementProject', 'project-1');
    expect(joined).toHaveBeenCalledOnce();

    await state.connection.handlers.reconnecting();
    await state.connection.handlers.reconnected();

    expect(state.connection.invoke).toHaveBeenCalledTimes(2);
    expect(joined).toHaveBeenCalledTimes(2);

    state.connectionState = 'Disconnected';
    await state.connection.handlers.close();
    const resumed = acquireProjectManagementHubConnection('/hubs/system-notification', scope)!;
    await Promise.resolve();
    await Promise.resolve();

    expect(state.connection.start).toHaveBeenCalledTimes(2);
    expect(state.connection.invoke).toHaveBeenCalledTimes(3);
    expect(joined).toHaveBeenCalledTimes(3);

    leaveProject();
    first.dispose();
    second.dispose();
    resumed.dispose();
  });
});
