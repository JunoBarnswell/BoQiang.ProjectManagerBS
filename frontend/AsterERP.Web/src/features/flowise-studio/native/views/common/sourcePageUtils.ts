import type { FlowiseResourceDto, FlowiseResourceUpsertRequest, FlowiseStudioQuery } from '../../../types/shared.types';

export const sourcePageSizeOptions = [10, 20, 50] as const;

export function buildSourceQuery(keyword: string, status: string, pageIndex: number, pageSize: number): FlowiseStudioQuery {
  return {
    keyword: keyword.trim(),
    pageIndex,
    pageSize,
    status
  };
}

export function createResourceDraft(overrides: Partial<FlowiseResourceUpsertRequest> = {}): FlowiseResourceUpsertRequest {
  return {
    category: '',
    definitionJson: '{}',
    description: '',
    displayName: '',
    metadataJson: '{}',
    resourceKey: '',
    secretValue: '',
    status: 'Enabled',
    workspaceId: '',
    ...overrides
  };
}

export function toResourceDraft(item: FlowiseResourceDto): FlowiseResourceUpsertRequest {
  return {
    category: item.category ?? '',
    definitionJson: item.definitionJson || '{}',
    description: item.description ?? '',
    displayName: item.displayName,
    metadataJson: item.metadataJson || '{}',
    resourceKey: item.resourceKey,
    secretValue: '',
    status: item.status || 'Enabled',
    workspaceId: item.workspaceId ?? ''
  };
}

export function formatSourceDate(value?: string | null): string {
  if (!value) {
    return '-';
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

export function formatSourceDateOnly(value?: string | null): string {
  if (!value) {
    return '-';
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleDateString();
}

export function parseJsonRecord(value?: string | null): Record<string, unknown> {
  if (!value) {
    return {};
  }

  try {
    const parsed = JSON.parse(value) as unknown;
    return parsed && typeof parsed === 'object' && !Array.isArray(parsed) ? parsed as Record<string, unknown> : {};
  } catch {
    return {};
  }
}

export function readStringList(value: unknown): string[] {
  if (Array.isArray(value)) {
    return value.map((item) => String(item)).filter(Boolean);
  }

  if (typeof value === 'string' && value.trim()) {
    return value.split(/[;,]/).map((item) => item.trim()).filter(Boolean);
  }

  return [];
}

export function readNumber(value: unknown): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : 0;
}

export function splitTags(value?: string | null): string[] {
  return (value ?? '').split(';').map((item) => item.trim()).filter(Boolean);
}

export function summarizeJson(value?: string | null): string {
  const record = parseJsonRecord(value);
  const keys = Object.keys(record);
  return keys.length ? keys.slice(0, 4).join(', ') : '-';
}

export function normalizeSourceStatus(value?: string | null): string {
  return value?.trim() || 'Enabled';
}

export function getSourcePageTotalPages(total: number, pageSize: number): number {
  return Math.max(1, Math.ceil(total / pageSize));
}
