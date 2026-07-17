import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { useEffect } from 'react';

import { getAccessToken } from '@/core/http/tokenStorage';

import { useImStore } from '../state/imStore';
import type { ImApiAdapter, ImMessage, ImPresenceChanged, ImUnreadSummary } from '../types/imTypes';

export function useImRealtimeConnection(adapter: ImApiAdapter, signalRUrl: string, enabled: boolean, currentUserId: string) {
  const receiveRealtimeMessage = useImStore((state) => state.receiveRealtimeMessage);
  const setConversations = useImStore((state) => state.setConversations);
  const setConnectionStatus = useImStore((state) => state.setConnectionStatus);
  const setUnreadSummary = useImStore((state) => state.setUnreadSummary);
  const updatePresence = useImStore((state) => state.updatePresence);

  useEffect(() => {
    if (!enabled) {
      setConnectionStatus('disconnected');
      return undefined;
    }

    let disposed = false;
    const connection = new HubConnectionBuilder()
      .withUrl(signalRUrl, { accessTokenFactory: () => getAccessToken() })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('ImMessageReceived', (message: ImMessage) => {
      const knownConversation = useImStore.getState().conversations.some((conversation) => conversation.id === message.conversationId);
      receiveRealtimeMessage(message, currentUserId);
      if (message.conversationId === useImStore.getState().activeConversationId) {
        void adapter.markRead(message.conversationId).then(setUnreadSummary);
      }

      if (!knownConversation) {
        void Promise.all([adapter.getConversations(), adapter.getUnreadSummary()]).then(([conversations, unread]) => {
          setConversations(conversations);
          setUnreadSummary(unread);
        });
      }
    });
    connection.on('ImUnreadChanged', (summary: ImUnreadSummary) => setUnreadSummary(summary));
    connection.on('ImPresenceChanged', (presence: ImPresenceChanged) => updatePresence(presence));
    connection.onreconnecting(() => setConnectionStatus('reconnecting'));
    connection.onreconnected(() => {
      setConnectionStatus('connected');
      void Promise.all([adapter.getConversations(), adapter.getUnreadSummary()]).then(([conversations, unread]) => {
        setConversations(conversations);
        setUnreadSummary(unread);
      });
    });
    connection.onclose(() => {
      if (!disposed) setConnectionStatus('disconnected');
    });

    setConnectionStatus('connecting');
    void (async () => {
      // React StrictMode can clean up and recreate effects immediately in
      // development. Yield once so a connection that is already disposed is
      // never stopped while SignalR is still negotiating.
      await Promise.resolve();
      if (disposed) return;
      try {
        await connection.start();
        if (!disposed && connection.state === HubConnectionState.Connected) {
          setConnectionStatus('connected');
        }
      } catch {
        if (!disposed) setConnectionStatus('disconnected');
      }
    })();

    return () => {
      disposed = true;
      void connection.stop();
    };
  }, [adapter, currentUserId, enabled, receiveRealtimeMessage, setConnectionStatus, setConversations, setUnreadSummary, signalRUrl, updatePresence]);
}
