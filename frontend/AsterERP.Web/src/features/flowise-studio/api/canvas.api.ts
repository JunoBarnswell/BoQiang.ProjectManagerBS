import type { ApiEnvelope } from '../../../core/http/apiEnvelope';
import { httpClient } from '../../../core/http/httpClient';
import type {
  FlowiseCanvasDto,
  FlowiseCanvasUpsertRequest,
  FlowiseCanvasValidationResult
} from '../types/canvas.types';
import type { FlowiseNodeCatalogItemDto, FlowiseNodeDefinitionDto } from '../types/node.types';

const basePath = '/ai/flowise';

export const canvasApi = {
  definitions: (signal?: AbortSignal): Promise<ApiEnvelope<FlowiseNodeDefinitionDto[]>> =>
    httpClient.get<FlowiseNodeDefinitionDto[]>(`${basePath}/nodes/definitions`, undefined, signal),
  get: (resourceId: string, signal?: AbortSignal): Promise<ApiEnvelope<FlowiseCanvasDto>> =>
    httpClient.get<FlowiseCanvasDto>(`${basePath}/canvas/${resourceId}`, undefined, signal),
  nodes: (signal?: AbortSignal): Promise<ApiEnvelope<FlowiseNodeCatalogItemDto[]>> =>
    httpClient.get<FlowiseNodeCatalogItemDto[]>(`${basePath}/canvas/nodes`, undefined, signal),
  save: (request: FlowiseCanvasUpsertRequest): Promise<ApiEnvelope<FlowiseCanvasDto>> =>
    httpClient.put<FlowiseCanvasDto, FlowiseCanvasUpsertRequest>(`${basePath}/canvas`, request),
  validate: (request: FlowiseCanvasUpsertRequest): Promise<ApiEnvelope<FlowiseCanvasValidationResult>> =>
    httpClient.post<FlowiseCanvasValidationResult, FlowiseCanvasUpsertRequest>(`${basePath}/canvas/validate`, request)
};
