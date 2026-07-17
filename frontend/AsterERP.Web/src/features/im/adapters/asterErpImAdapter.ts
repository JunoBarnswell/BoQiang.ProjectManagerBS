import { httpClient } from '@/core/http/httpClient';

import { imApiPaths } from '../api/imApi';
import type {
  ImAccountBinding,
  ImApiAdapter,
  ImConversation,
  ImDirectory,
  ImMessage,
  ImMessagePage,
  ImSendMessageRequest,
  ImUnreadSummary,
  ImUserSearchItem
} from '../types/imTypes';

export const asterErpImAdapter: ImApiAdapter = {
  async createDirectConversation(targetUserId) {
    const response = await httpClient.post<ImConversation, { targetUserId: string }>(imApiPaths.directConversation, { targetUserId });
    return response.data;
  },
  async getBinding() {
    const response = await httpClient.get<ImAccountBinding>(imApiPaths.binding);
    return response.data;
  },
  async getConversations(signal) {
    const response = await httpClient.get<ImConversation[]>(imApiPaths.conversations, { signal });
    return response.data;
  },
  async getDirectory(keyword, signal) {
    const response = await httpClient.get<ImDirectory>(imApiPaths.directory(keyword), { signal });
    return response.data;
  },
  async getMessages(conversationId, cursor, signal) {
    const suffix = cursor ? `?cursor=${encodeURIComponent(cursor)}&take=50` : '?take=50';
    const response = await httpClient.get<ImMessagePage>(`${imApiPaths.messages(conversationId)}${suffix}`, { signal });
    return response.data;
  },
  async getUnreadSummary(signal) {
    const response = await httpClient.get<ImUnreadSummary>(imApiPaths.unreadSummary, { signal });
    return response.data;
  },
  async markRead(conversationId) {
    const response = await httpClient.post<ImUnreadSummary, Record<string, never>>(imApiPaths.markRead(conversationId), {});
    return response.data;
  },
  async searchUsers(keyword, signal) {
    const response = await httpClient.get<ImUserSearchItem[]>(imApiPaths.userSearch(keyword), { signal });
    return response.data;
  },
  async sendMessage(conversationId, request: ImSendMessageRequest) {
    const response = await httpClient.post<ImMessage, ImSendMessageRequest>(imApiPaths.messages(conversationId), request);
    return response.data;
  }
};
