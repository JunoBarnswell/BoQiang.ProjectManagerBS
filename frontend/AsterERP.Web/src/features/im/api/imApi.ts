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

export const imApiPaths = {
  binding: '/im/account-binding/me',
  conversations: '/im/conversations',
  directory: (keyword?: string) => `/im/directory${keyword?.trim() ? `?keyword=${encodeURIComponent(keyword.trim())}` : ''}`,
  directConversation: '/im/conversations/direct',
  messages: (conversationId: string) => `/im/conversations/${encodeURIComponent(conversationId)}/messages`,
  markRead: (conversationId: string) => `/im/conversations/${encodeURIComponent(conversationId)}/read`,
  unreadSummary: '/im/unread-summary',
  userSearch: (keyword: string) => `/im/users/search?keyword=${encodeURIComponent(keyword)}&take=20`
};

export type {
  ImAccountBinding,
  ImApiAdapter,
  ImConversation,
  ImDirectory,
  ImMessage,
  ImMessagePage,
  ImSendMessageRequest,
  ImUnreadSummary,
  ImUserSearchItem
};
