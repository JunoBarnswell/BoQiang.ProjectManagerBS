import type { ApiEnvelope } from '../../../core/http/apiEnvelope';
import { httpClient } from '../../../core/http/httpClient';
import type {
  FlowiseWebhookListenerRegistrationDto,
  FlowiseWebhookStreamEvent,
  FlowiseWebhookTriggerRequest,
  FlowiseWebhookTriggerResponse
} from '../types/webhookListener.types';

type StreamHandler = (event: FlowiseWebhookStreamEvent) => void;

const basePath = '/v1';

export const webhookListenerApi = {
  register: (chatflowId: string): Promise<ApiEnvelope<FlowiseWebhookListenerRegistrationDto>> =>
    httpClient.post<FlowiseWebhookListenerRegistrationDto, Record<string, never>>(`${basePath}/webhook-listener/${chatflowId}`, {}),
  stream: (chatflowId: string, listenerId: string, onEvent: StreamHandler, signal?: AbortSignal): Promise<void> =>
    httpClient.streamSse<FlowiseWebhookStreamEvent>({
      method: 'GET',
      onEvent,
      path: `${basePath}/webhook-listener/${chatflowId}/stream/${listenerId}`,
      signal
    }),
  trigger: (
    chatflowId: string,
    request: FlowiseWebhookTriggerRequest,
    webhookSecret?: string | null
  ): Promise<ApiEnvelope<FlowiseWebhookTriggerResponse>> => {
    const headers = webhookSecret ? { 'x-flowise-webhook-secret': webhookSecret } : undefined;
    return httpClient.post<FlowiseWebhookTriggerResponse, FlowiseWebhookTriggerRequest>(`${basePath}/webhook/${chatflowId}`, request, { headers });
  },
  unregister: (chatflowId: string, listenerId: string): Promise<ApiEnvelope<boolean>> =>
    httpClient.delete<boolean>(`${basePath}/webhook-listener/${chatflowId}/${listenerId}`)
};
