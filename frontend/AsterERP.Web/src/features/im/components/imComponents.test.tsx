// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { useImStore } from '../state/imStore';
import type { ImApiAdapter, ImConversation, ImCurrentUser } from '../types/imTypes';

import { ImConversationList } from './ImConversationList';
import { ImMessageComposer } from './ImMessageComposer';
import { ImMiniConversation } from './ImMiniConversation';
import { ImProvider, useImContext } from './ImProvider';

describe('IM components', () => {
  beforeEach(() => {
    useImStore.setState({
      activeConversationId: undefined,
      connectionStatus: 'disconnected',
      conversations: [],
      drawerOpen: false,
      messagesByConversation: {},
      presenceByUserId: {},
      unreadSummary: { conversationUnreadCounts: {}, totalUnread: 0 }
    });
  });

  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  it('throws a clear error when components are used outside ImProvider', () => {
    function ContextProbe() {
      useImContext();
      return null;
    }

    expect(() => render(<ContextProbe />)).toThrow('IM components must be rendered inside ImProvider.');
  });

  it('uses permissive default UI permissions unless caller overrides them', () => {
    function PermissionProbe() {
      const { permissions } = useImContext();
      return <span>{permissions.canCreateConversation && permissions.canSearchUsers && permissions.canSendMessage && permissions.canView ? 'enabled' : 'disabled'}</span>;
    }

    render(
      <ImProvider adapter={createAdapter()} currentUser={currentUserWithoutRealtime} signalRUrl="/hubs/system-notification">
        <PermissionProbe />
      </ImProvider>
    );

    expect(screen.getByText('enabled')).toBeTruthy();
  });

  it('renders selected conversation, unread badge, and last message preview', () => {
    const onSelect = vi.fn();
    const conversation = createConversation('conversation-1', 3);

    render(<ImConversationList activeConversationId="conversation-1" conversations={[conversation]} onSelect={onSelect} />);

    const button = screen.getByRole('button');
    expect(screen.getByText('User B')).toBeTruthy();
    expect(screen.getByText('3')).toBeTruthy();
    expect(screen.getByText('last message')).toBeTruthy();
    expect(button.className).toContain('bg-blue-50');

    fireEvent.click(button);
    expect(onSelect).toHaveBeenCalledWith(conversation);
  });

  it('creates and opens a direct conversation in ImMiniConversation', async () => {
    const adapter = createAdapter({
      createDirectConversation: vi.fn().mockResolvedValue(createConversation('conversation-1', 0)),
      getMessages: vi.fn().mockResolvedValue({ hasMore: false, items: [] })
    });

    render(
      <ImProvider adapter={adapter} currentUser={currentUserWithoutRealtime} signalRUrl="/hubs/system-notification">
        <ImMiniConversation targetUserId="user-b" />
      </ImProvider>
    );

    await waitFor(() => expect(adapter.createDirectConversation).toHaveBeenCalledWith('user-b'));
    expect(await screen.findByText('User B')).toBeTruthy();
  });

  it('blocks empty send, sends with Enter, and keeps Shift+Enter as newline behavior', async () => {
    const onSend = vi.fn().mockResolvedValue(undefined);

    render(<ImMessageComposer onSend={onSend} />);

    const textarea = screen.getByPlaceholderText('输入消息');
    const button = screen.getByRole('button', { name: /发送/ }) as HTMLButtonElement;
    expect(button.disabled).toBe(true);

    fireEvent.change(textarea, { target: { value: '  hello  ' } });
    expect(button.disabled).toBe(false);

    fireEvent.keyDown(textarea, { key: 'Enter', shiftKey: true });
    expect(onSend).not.toHaveBeenCalled();

    fireEvent.keyDown(textarea, { key: 'Enter' });
    await waitFor(() => expect(onSend).toHaveBeenCalledWith('hello'));
  });
});

const currentUserWithoutRealtime: ImCurrentUser = {
  displayName: 'User A',
  tenantId: null,
  userId: 'user-a',
  userName: 'user-a'
};

function createAdapter(overrides: Partial<ImApiAdapter> = {}): ImApiAdapter {
  return {
    createDirectConversation: vi.fn().mockResolvedValue(createConversation('conversation-1', 0)),
    getBinding: vi.fn().mockResolvedValue({
      boundAt: '2026-07-05T00:00:00.000Z',
      displayName: 'User A',
      imAccountId: 'astererp.tenant-a.user-a',
      status: 'Active',
      tenantId: 'tenant-a',
      userId: 'user-a'
    }),
    getConversations: vi.fn().mockResolvedValue([]),
    getDirectory: vi.fn().mockResolvedValue({ departments: [] }),
    getMessages: vi.fn().mockResolvedValue({ hasMore: false, items: [] }),
    getUnreadSummary: vi.fn().mockResolvedValue({ conversationUnreadCounts: {}, totalUnread: 0 }),
    markRead: vi.fn().mockResolvedValue({ conversationUnreadCounts: {}, totalUnread: 0 }),
    searchUsers: vi.fn().mockResolvedValue([]),
    sendMessage: vi.fn(),
    ...overrides
  };
}

function createConversation(id: string, unreadCount: number): ImConversation {
  return {
    conversationKey: `direct:tenant-a:${id}`,
    createdTime: '2026-07-05T00:00:00.000Z',
    id,
    lastMessagePreview: 'last message',
    peerDisplayName: 'User B',
    peerUserId: 'user-b',
    tenantId: 'tenant-a',
    unreadCount
  };
}
