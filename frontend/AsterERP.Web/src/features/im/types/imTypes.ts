export interface ImAccountBinding {
  tenantId: string;
  userId: string;
  imAccountId: string;
  displayName: string;
  status: string;
  boundAt: string;
}

export interface ImApiAdapter {
  getBinding(): Promise<ImAccountBinding>;
  getDirectory(keyword?: string, signal?: AbortSignal): Promise<ImDirectory>;
  searchUsers(keyword: string, signal?: AbortSignal): Promise<ImUserSearchItem[]>;
  getConversations(signal?: AbortSignal): Promise<ImConversation[]>;
  createDirectConversation(targetUserId: string): Promise<ImConversation>;
  getMessages(conversationId: string, cursor?: string, signal?: AbortSignal): Promise<ImMessagePage>;
  sendMessage(conversationId: string, request: ImSendMessageRequest): Promise<ImMessage>;
  markRead(conversationId: string): Promise<ImUnreadSummary>;
  getUnreadSummary(signal?: AbortSignal): Promise<ImUnreadSummary>;
}

export interface ImConversation {
  id: string;
  tenantId: string;
  conversationKey: string;
  conversationType?: 'Direct' | 'Group' | string;
  title?: string | null;
  status?: 'Active' | 'Archived' | string;
  peerUserId: string;
  peerDisplayName: string;
  lastMessageId?: string | null;
  lastMessagePreview?: string | null;
  lastMessageAt?: string | null;
  unreadCount: number;
  participantCount?: number;
  createdTime: string;
}

export interface ImDirectory {
  departments: ImDirectoryDepartment[];
}

export interface ImDirectoryDepartment {
  children: ImDirectoryDepartment[];
  deptCode: string;
  deptId: string;
  deptName: string;
  parentId?: string | null;
  sortOrder: number;
  users: ImDirectoryUser[];
}

export interface ImDirectoryUser {
  conversationId?: string | null;
  deptId: string;
  deptName?: string | null;
  displayName: string;
  employmentId: string;
  employmentName: string;
  imAccountId: string;
  isOnline: boolean;
  isPrimaryEmployment: boolean;
  lastMessageAt?: string | null;
  lastMessagePreview?: string | null;
  lastSeenTime?: string | null;
  positionId: string;
  positionName?: string | null;
  unreadCount: number;
  userId: string;
  userName: string;
}

export interface ImCurrentUser {
  userId: string;
  userName: string;
  displayName: string;
  tenantId?: string | null;
  appCode?: string | null;
}

export interface ImMessage {
  id: string;
  conversationId: string;
  senderUserId: string;
  receiverUserId: string;
  messageType: string;
  content: string;
  status: 'sending' | 'sent' | 'failed' | string;
  clientMessageId?: string | null;
  sourceAppCode?: string | null;
  sentAt: string;
}

export interface ImMessagePage {
  items: ImMessage[];
  nextCursor?: string | null;
  hasMore: boolean;
}

export interface ImPermissions {
  canView: boolean;
  canSearchUsers: boolean;
  canCreateConversation: boolean;
  canSendMessage: boolean;
}

export interface ImProviderConfig {
  adapter?: ImApiAdapter;
  currentUser?: ImCurrentUser;
  permissions?: Partial<ImPermissions>;
  signalRUrl?: string;
}

export interface ImProviderProps extends ImProviderConfig {
  children: React.ReactNode;
  config?: ImProviderConfig;
}

export interface ImSendMessageRequest {
  content: string;
  clientMessageId?: string | null;
  messageType?: string | null;
  sourceAppCode?: string | null;
}

export interface ImUnreadSummary {
  totalUnread: number;
  conversationUnreadCounts: Record<string, number>;
}

export interface ImPresenceChanged {
  appCode: string;
  isOnline: boolean;
  lastSeenTime?: string | null;
  tenantId: string;
  userId: string;
}

export interface ImUserSearchItem {
  userId: string;
  userName: string;
  displayName: string;
  deptId?: string | null;
  positionId?: string | null;
  imAccountId: string;
}
