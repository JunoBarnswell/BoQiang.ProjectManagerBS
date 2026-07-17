// @vitest-environment jsdom

import { act, cleanup, render, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { useImStore } from '../state/imStore';
import type { ImApiAdapter } from '../types/imTypes';

const signalRState = vi.hoisted(() => ({
  connections: [] as Array<{
    state: string;
    start: ReturnType<typeof vi.fn>;
    stop: ReturnType<typeof vi.fn>;
  }>
}));

vi.mock('@microsoft/signalr', () => {
  class FakeHubConnectionBuilder {
    withUrl() {
      return this;
    }

    withAutomaticReconnect() {
      return this;
    }

    configureLogging() {
      return this;
    }

    build() {
      const connection = {
        state: 'Disconnected',
        start: vi.fn(async () => {
          connection.state = 'Connected';
        }),
        stop: vi.fn(async () => {
          connection.state = 'Disconnected';
        }),
        on: vi.fn(),
        onclose: vi.fn(),
        onreconnecting: vi.fn(),
        onreconnected: vi.fn()
      };
      signalRState.connections.push(connection);
      return connection;
    }
  }

  return {
    HubConnectionBuilder: FakeHubConnectionBuilder,
    HubConnectionState: { Connected: 'Connected' },
    LogLevel: { Warning: 2 }
  };
});

import { useImRealtimeConnection } from './useImRealtimeConnection';

describe('useImRealtimeConnection', () => {
  beforeEach(() => {
    signalRState.connections.length = 0;
    useImStore.setState({ activeConversationId: undefined });
  });

  afterEach(() => {
    cleanup();
  });

  it('keeps one connection when the active conversation changes during a session', async () => {
    const adapter = {
      markRead: vi.fn().mockResolvedValue({ conversationUnreadCounts: {}, totalUnread: 0 })
    } as unknown as ImApiAdapter;

    function Probe() {
      useImRealtimeConnection(adapter, '/hubs/system-notification', true, 'user-a');
      return null;
    }

    const view = render(<Probe />);
    await waitFor(() => expect(signalRState.connections).toHaveLength(1));
    const connection = signalRState.connections[0];

    act(() => {
      useImStore.getState().setActiveConversationId('conversation-1');
    });

    await waitFor(() => expect(signalRState.connections).toHaveLength(1));
    expect(connection.start).toHaveBeenCalledTimes(1);
    expect(connection.stop).not.toHaveBeenCalled();

    view.unmount();
    await waitFor(() => expect(connection.stop).toHaveBeenCalledTimes(1));
  });
});
