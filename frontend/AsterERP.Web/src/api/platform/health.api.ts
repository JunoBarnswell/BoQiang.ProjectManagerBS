import type { ApiEnvelope } from '../../core/http/apiEnvelope';
import { httpClient } from '../../core/http/httpClient';
import type { EchoDto, HealthDto } from '../shared.types';

export function getHealth(): Promise<ApiEnvelope<HealthDto>> {
  return httpClient.get<HealthDto>('/health');
}

export function sendEcho(message: string): Promise<ApiEnvelope<EchoDto>> {
  return httpClient.post<EchoDto, { message: string }>('/echo', { message });
}
