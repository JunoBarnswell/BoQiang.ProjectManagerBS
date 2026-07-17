import type { ApiEnvelope } from '../../../core/http/apiEnvelope';
import { httpClient } from '../../../core/http/httpClient';
import type {
  FlowiseFeedbackDto,
  FlowiseChatMessageDto,
  FlowiseLeadDto,
  FlowisePredictionListQuery,
  FlowisePredictionRequest,
  FlowisePredictionResponse,
  FlowisePredictionStreamEvent
} from '../types/prediction.types';
import type { GridPageResult } from '../types/shared.types';

import { buildFlowiseQuery } from './queryString';

const basePath = '/ai/flowise';

type StreamHandler = (event: FlowisePredictionStreamEvent) => void;

export const predictionsApi = {
  messages: {
    list: (query: FlowisePredictionListQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<FlowiseChatMessageDto>>> =>
      httpClient.get<GridPageResult<FlowiseChatMessageDto>>(`${basePath}/prediction/messages${buildFlowiseQuery(query)}`, undefined, signal)
  },
  leads: {
    list: (query: FlowisePredictionListQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<FlowiseLeadDto>>> =>
      httpClient.get<GridPageResult<FlowiseLeadDto>>(`${basePath}/prediction/leads${buildFlowiseQuery(query)}`, undefined, signal)
  },
  lead: (resourceId: string, contactJson: string): Promise<ApiEnvelope<FlowiseLeadDto>> =>
    httpClient.post<FlowiseLeadDto, { contactJson: string; resourceId: string }>(`${basePath}/prediction/lead`, { contactJson, resourceId }),
  clear: (resourceId: string, chatId?: string | null): Promise<ApiEnvelope<boolean>> =>
    httpClient.post<boolean, { chatId?: string | null; resourceId: string }>(`${basePath}/prediction/messages/clear`, { chatId, resourceId }),
  abort: (resourceId: string, chatId?: string | null): Promise<ApiEnvelope<boolean>> =>
    httpClient.post<boolean, { chatId?: string | null; resourceId: string }>(`${basePath}/prediction/messages/abort`, { chatId, resourceId }),
  predict: (request: FlowisePredictionRequest, signal?: AbortSignal): Promise<ApiEnvelope<FlowisePredictionResponse>> =>
    httpClient.post<FlowisePredictionResponse, FlowisePredictionRequest>(`${basePath}/prediction`, request, undefined, signal),
  stream: (request: FlowisePredictionRequest, onEvent: StreamHandler, signal?: AbortSignal): Promise<void> =>
    httpClient.streamSse<FlowisePredictionStreamEvent, FlowisePredictionRequest>({
      body: request,
      method: 'POST',
      onEvent,
      path: `${basePath}/prediction/stream`,
      signal
    }),
  feedback: (messageId: string, rating: 'up' | 'down', reason?: string): Promise<ApiEnvelope<FlowiseFeedbackDto>> =>
    httpClient.post<FlowiseFeedbackDto, { messageId: string; rating: 'up' | 'down'; reason?: string }>(`${basePath}/prediction/feedback`, {
      messageId,
      rating,
      reason
    })
};
