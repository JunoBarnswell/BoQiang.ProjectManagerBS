import type { GridPageResult } from './shared.types';

export interface FlowiseCustomMcpServerDto {
  authConfigJson: string;
  authType: string;
  color?: string | null;
  createdTime: string;
  errorMessage?: string | null;
  iconSrc?: string | null;
  id: string;
  name: string;
  serverUrl: string;
  status: string;
  toolCount: number;
  toolsJson: string;
  updatedTime?: string | null;
  workspaceId?: string | null;
}

export interface FlowiseCustomMcpServerUpsertRequest {
  authConfigJson?: string | null;
  authType?: string | null;
  color?: string | null;
  iconSrc?: string | null;
  name: string;
  serverUrl: string;
  status?: string | null;
  workspaceId?: string | null;
}

export interface FlowiseCustomMcpServerAuthorizeResultDto {
  errorMessage?: string | null;
  id: string;
  status: string;
  toolCount: number;
  toolsJson: string;
}

export interface FlowiseCustomMcpServerToolDto {
  annotationsJson: string;
  description?: string | null;
  iconsJson: string;
  inputSchemaJson: string;
  name: string;
}

export type FlowiseCustomMcpServerPageResult = GridPageResult<FlowiseCustomMcpServerDto>;
