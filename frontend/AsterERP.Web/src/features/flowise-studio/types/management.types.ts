export interface FlowiseLoginActivityDto {
  createdTime: string;
  id: string;
  ipAddress?: string | null;
  status: string;
  userAgent?: string | null;
  userName: string;
}

export interface FlowiseAuditLogDto {
  createdTime: string;
  detailJson: string;
  eventType: string;
  id: string;
  resourceId?: string | null;
  resourceType: string;
}

export interface FlowiseRoleDto {
  description?: string | null;
  id: string;
  name: string;
  permissions: string[];
  status: string;
}

export interface FlowiseUserDto {
  email?: string | null;
  id: string;
  name: string;
  roles: string[];
  status: string;
  workspaceIds: string[];
}

export interface FlowiseSsoConfigDto {
  enabled: boolean;
  id: string;
  provider: string;
  settingsJson: string;
}
