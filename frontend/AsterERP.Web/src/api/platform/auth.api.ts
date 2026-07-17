import type { ApiEnvelope } from '../../core/http/apiEnvelope';
import { httpClient } from '../../core/http/httpClient';

import type { ApplicationLoginBootstrapDto, ApplicationLoginRequest, ApplicationLoginResponseDto, CurrentUserDto, CurrentWorkspaceDto, InitialAdminPasswordRecoveryRequest, LoginRequest, LoginResponseDto, SessionResponseDto, SwitchPlatformRequest, SwitchWorkspaceRequest, SwitchWorkspaceResponseDto, WorkspaceDto } from './auth.types';

export function login(request: LoginRequest): Promise<ApiEnvelope<LoginResponseDto>> {
  return httpClient.post<LoginResponseDto, LoginRequest>('/auth/login', request);
}

export function recoverInitialAdminPassword(request: InitialAdminPasswordRecoveryRequest): Promise<ApiEnvelope<boolean>> {
  return httpClient.post<boolean, InitialAdminPasswordRecoveryRequest>('/auth/initial-admin-password-recovery', request, { auth: false, workspace: false });
}

export function getApplicationLoginBootstrap(tenantId: string, appCode: string): Promise<ApiEnvelope<ApplicationLoginBootstrapDto>> {
  return httpClient.get<ApplicationLoginBootstrapDto>(
    `/application-auth/tenants/${encodeURIComponent(tenantId)}/apps/${encodeURIComponent(appCode)}/bootstrap`
  );
}

export function applicationLogin(
  tenantId: string,
  appCode: string,
  request: ApplicationLoginRequest
): Promise<ApiEnvelope<ApplicationLoginResponseDto>> {
  return httpClient.post<ApplicationLoginResponseDto, ApplicationLoginRequest>(
    `/application-auth/tenants/${encodeURIComponent(tenantId)}/apps/${encodeURIComponent(appCode)}/login`,
    request,
    120_000
  );
}

export function getSession(): Promise<ApiEnvelope<SessionResponseDto>> {
  return httpClient.get<SessionResponseDto>('/auth/me');
}

export function getWorkspaces(): Promise<ApiEnvelope<WorkspaceDto[]>> {
  return httpClient.get<WorkspaceDto[]>('/auth/workspaces');
}

export function switchWorkspace(request: SwitchWorkspaceRequest): Promise<ApiEnvelope<SwitchWorkspaceResponseDto>> {
  return httpClient.post<SwitchWorkspaceResponseDto, SwitchWorkspaceRequest>('/auth/switch-workspace', request);
}

export function switchPlatformWorkspace(request: SwitchPlatformRequest = { target: 'application-center' }): Promise<ApiEnvelope<SwitchWorkspaceResponseDto>> {
  return httpClient.post<SwitchWorkspaceResponseDto, SwitchPlatformRequest>('/auth/switch-platform', request);
}

export function getCurrentWorkspace(): Promise<ApiEnvelope<CurrentWorkspaceDto | null>> {
  return httpClient.get<CurrentWorkspaceDto | null>('/auth/current-workspace');
}

export type {
  CurrentWorkspaceDto,
  CurrentUserDto,
  InitialAdminPasswordRecoveryRequest,
  ApplicationLoginBootstrapDto,
  ApplicationLoginRequest,
  ApplicationLoginResponseDto,
  LoginRequest,
  LoginResponseDto,
  SessionResponseDto,
  SwitchPlatformRequest,
  SwitchWorkspaceRequest,
  SwitchWorkspaceResponseDto,
  WorkspaceDto
};
