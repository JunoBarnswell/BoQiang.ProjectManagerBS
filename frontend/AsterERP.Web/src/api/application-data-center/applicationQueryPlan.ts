import type { ApplicationDataCenterObjectDetail, ApplicationQueryPlanRequest } from './applicationDataCenter.types';

export function buildApplicationQueryPlanRequest(
  detail: ApplicationDataCenterObjectDetail
): ApplicationQueryPlanRequest {
  const config = parseConfig(detail.configJson);
  const plan = config.queryPlan;
  if (!isRecord(plan)) {
    throw new Error('Query dataset is not migrated to the latest Resource ID query model.');
  }
  if (hasLegacyQueryKeys(plan)) {
    throw new Error('Query dataset contains legacy object or field paths and must be migrated before execution.');
  }
  const request = plan as unknown as ApplicationQueryPlanRequest;
  if (!request.dataSourceId || !Array.isArray(request.nodes) || request.nodes.length === 0) {
    throw new Error('Query dataset is missing its latest data-source or Resource ID node definition.');
  }
  if (request.nodes.some((node) => !node.resourceId) || request.columns.some((column) => !column.fieldResourceId)) {
    throw new Error('Query dataset contains an incomplete Resource ID query model.');
  }
  return {
    ...request,
    accessMode: request.accessMode || 'readOnly',
    page: { index: Math.max(1, request.page?.index ?? 1), size: Math.max(1, request.page?.size ?? 20) },
    rowLimit: Math.max(1, request.rowLimit ?? 20),
    timeoutSeconds: Math.max(1, request.timeoutSeconds ?? 30)
  };
}

function parseConfig(value: string): Record<string, unknown> {
  try {
    const parsed = JSON.parse(value) as unknown;
    return isRecord(parsed) ? parsed : {};
  } catch {
    return {};
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value && typeof value === 'object' && !Array.isArray(value));
}

function hasLegacyQueryKeys(value: Record<string, unknown>): boolean {
  const legacyKeys = new Set(['objectName', 'fieldCode', 'parameterName', 'leftFieldCode', 'rightFieldCode']);
  const visit = (item: unknown): boolean => {
    if (Array.isArray(item)) return item.some(visit);
    if (!isRecord(item)) return false;
    return Object.entries(item).some(([key, child]) => legacyKeys.has(key) || visit(child));
  };
  return visit(value);
}
