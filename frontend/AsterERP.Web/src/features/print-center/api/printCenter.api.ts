import type { ApiEnvelope } from '../../../core/http/apiEnvelope';
import { httpClient } from '../../../core/http/httpClient';
import type {
  PrintCustomElementDetailDto,
  PrintCustomElementListItemDto,
  PrintCustomElementUpsertRequest,
  PrintRuntimeResolveRequest,
  PrintRuntimeResolveResponse,
  PrintTargetDetailDto,
  PrintTargetOptionDto,
  PrintTemplateDetailDto,
  PrintTemplateListItemDto,
  PrintTemplateUpsertRequest
} from '../types';

function buildQueryString(params: Record<string, string | undefined | null>): string {
  const query = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (typeof value === 'string' && value.trim()) {
      query.set(key, value.trim());
    }
  });

  const text = query.toString();
  return text ? `?${text}` : '';
}

export const printCenterApi = {
  deleteCustomElement: (id: string): Promise<ApiEnvelope<boolean>> =>
    httpClient.delete<boolean>(`/system/print-center/designer/custom-elements/${encodeURIComponent(id)}`),

  deleteTemplate: (id: string): Promise<ApiEnvelope<boolean>> =>
    httpClient.delete<boolean>(`/system/print-center/templates/${encodeURIComponent(id)}`),

  getCustomElement: (id: string): Promise<ApiEnvelope<PrintCustomElementDetailDto>> =>
    httpClient.get<PrintCustomElementDetailDto>(`/system/print-center/designer/custom-elements/${encodeURIComponent(id)}`),

  getCustomElements: (signal?: AbortSignal): Promise<ApiEnvelope<PrintCustomElementListItemDto[]>> =>
    httpClient.get<PrintCustomElementListItemDto[]>('/system/print-center/designer/custom-elements', undefined, signal),

  getTarget: (menuCode: string, scene?: string, signal?: AbortSignal): Promise<ApiEnvelope<PrintTargetDetailDto>> =>
    httpClient.get<PrintTargetDetailDto>(`/system/print-center/targets/${encodeURIComponent(menuCode)}${buildQueryString({ scene })}`, undefined, signal),

  getTargets: (signal?: AbortSignal): Promise<ApiEnvelope<PrintTargetOptionDto[]>> =>
    httpClient.get<PrintTargetOptionDto[]>('/system/print-center/targets', undefined, signal),

  getTemplate: (id: string, signal?: AbortSignal): Promise<ApiEnvelope<PrintTemplateDetailDto>> =>
    httpClient.get<PrintTemplateDetailDto>(`/system/print-center/templates/${encodeURIComponent(id)}`, undefined, signal),

  getTemplateOptions: (menuCode: string, scene: string, signal?: AbortSignal): Promise<ApiEnvelope<PrintTemplateListItemDto[]>> =>
    httpClient.get<PrintTemplateListItemDto[]>(`/system/print-center/templates/options${buildQueryString({ menuCode, scene })}`, undefined, signal),

  getTemplates: (
    query: { keyword?: string; menuCode?: string; scene?: string; status?: string },
    signal?: AbortSignal
  ): Promise<ApiEnvelope<PrintTemplateListItemDto[]>> =>
    httpClient.get<PrintTemplateListItemDto[]>(`/system/print-center/templates${buildQueryString(query)}`, undefined, signal),

  publishTemplate: (id: string): Promise<ApiEnvelope<PrintTemplateDetailDto>> =>
    httpClient.post<PrintTemplateDetailDto, Record<string, never>>(`/system/print-center/templates/${encodeURIComponent(id)}/publish`, {}),

  resolveRuntime: (request: PrintRuntimeResolveRequest): Promise<ApiEnvelope<PrintRuntimeResolveResponse>> =>
    httpClient.post<PrintRuntimeResolveResponse, PrintRuntimeResolveRequest>('/system/print-center/resolve-runtime', request),

  saveCustomElement: (request: PrintCustomElementUpsertRequest): Promise<ApiEnvelope<PrintCustomElementDetailDto>> =>
    httpClient.post<PrintCustomElementDetailDto, PrintCustomElementUpsertRequest>('/system/print-center/designer/custom-elements', request),

  saveTemplate: (request: PrintTemplateUpsertRequest): Promise<ApiEnvelope<PrintTemplateDetailDto>> =>
    request.id
      ? httpClient.put<PrintTemplateDetailDto, PrintTemplateUpsertRequest>(`/system/print-center/templates/${encodeURIComponent(request.id)}`, request)
      : httpClient.post<PrintTemplateDetailDto, PrintTemplateUpsertRequest>('/system/print-center/templates', request),

  setDefaultTemplate: (id: string): Promise<ApiEnvelope<PrintTemplateDetailDto>> =>
    httpClient.post<PrintTemplateDetailDto, Record<string, never>>(`/system/print-center/templates/${encodeURIComponent(id)}/set-default`, {})
};
