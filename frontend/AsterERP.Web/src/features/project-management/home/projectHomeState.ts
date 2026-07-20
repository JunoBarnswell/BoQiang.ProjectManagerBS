import type {
  ProjectManagementHomeCollection,
  ProjectManagementHomeFilterGroup,
  ProjectManagementHomeHealth,
  ProjectManagementHomeQuery,
  ProjectManagementHomeView,
} from '../../../api/project-management/projectManagement.types';

export type ProjectHomeDensity = 'compact' | 'default' | 'comfortable';
export type ProjectHomeInsightTab = 'health' | 'leads';

export interface ProjectHomeUrlState {
  collection: ProjectManagementHomeCollection;
  view: ProjectManagementHomeView;
  keyword: string;
  health?: ProjectManagementHomeHealth;
  priority?: string;
  leadUserId?: string;
  status?: string;
  targetDateFrom?: string;
  targetDateTo?: string;
  sortBy: string;
  sortDirection: 'asc' | 'desc';
  density: ProjectHomeDensity;
  insights: boolean;
  insightsTab: ProjectHomeInsightTab;
  columns: string[];
  filter: ProjectManagementHomeFilterGroup;
  projectIds?: string;
}

export const defaultProjectHomeColumns = ['name', 'health', 'priority', 'lead', 'targetDate', 'issues', 'status'];
const allowedFilterFields = new Set(['health', 'priority', 'lead', 'members', 'status', 'startDate', 'targetDate', 'labels', 'issuesCount', 'updated', 'created', 'projectKey', 'workspace', 'archived']);
const allowedFilterOperators = new Set(['is', 'isNot', 'contains', 'notContains', 'in', 'notIn', 'before', 'beforeOrOn', 'after', 'afterOrOn', 'equals', 'notEquals', 'greaterThan', 'greaterOrEqual', 'lessThan', 'lessOrEqual', 'between', 'today', 'thisWeek', 'overdue', 'isEmpty', 'isNotEmpty']);

export function parseProjectHomeFilter(value?: string | null): ProjectManagementHomeFilterGroup {
  if (!value) return { conjunction: 'and', rules: [] };
  try {
    const parsed = JSON.parse(value) as { conjunction?: string; rules?: unknown };
    const rules = Array.isArray(parsed?.rules) ? parsed.rules : [];
    return {
      conjunction: 'and',
      rules: rules.slice(0, 40).flatMap(item => {
        if (!item || typeof item !== 'object') return [];
        const candidate = item as { field?: unknown; operator?: unknown; values?: unknown };
        const field = typeof candidate.field === 'string' ? candidate.field.trim() : '';
        const operator = typeof candidate.operator === 'string' ? candidate.operator.trim() : '';
        const values = Array.isArray(candidate.values) ? candidate.values.filter((entry): entry is string => typeof entry === 'string').map(entry => entry.trim()).filter(Boolean).slice(0, 50) : [];
        return allowedFilterFields.has(field) && allowedFilterOperators.has(operator) && values.length > 0 ? [{ field, operator, values }] : [];
      }),
    };
  } catch {
    return { conjunction: 'and', rules: [] };
  }
}

export function serializeProjectHomeFilter(filter: ProjectManagementHomeFilterGroup): string | undefined {
  const rules = filter.rules.filter(rule => allowedFilterFields.has(rule.field) && allowedFilterOperators.has(rule.operator) && rule.values.length > 0);
  return rules.length > 0 ? JSON.stringify({ conjunction: 'and', rules }) : undefined;
}

export function parseProjectHomeUrlState(params: URLSearchParams): ProjectHomeUrlState {
  const collection = params.get('collection');
  const density = params.get('density');
  const insightsTab = params.get('insightsTab');
  const columns = params.get('columns');
  const view = params.get('view');
  return {
    collection: collection === 'favorites' || collection === 'recent' ? collection : 'all',
    view: view || 'all',
    keyword: params.get('keyword') ?? params.get('search') ?? '',
    health: (params.get('health') as ProjectManagementHomeHealth | null) || undefined,
    priority: params.get('priority') || undefined,
    leadUserId: params.get('leadUserId') || undefined,
    status: params.get('status') || undefined,
    targetDateFrom: params.get('targetDateFrom') || undefined,
    targetDateTo: params.get('targetDateTo') || undefined,
    sortBy: params.get('sort') || params.get('sortBy') || 'updated',
    sortDirection: params.get('order') === 'asc' || params.get('sortDirection') === 'asc' ? 'asc' : 'desc',
    density: density === 'compact' || density === 'comfortable' ? density : 'default',
    insights: params.get('insights') !== 'false',
    insightsTab: insightsTab === 'leads' ? 'leads' : 'health',
    columns: columns ? columns.split(',').filter(column => defaultProjectHomeColumns.includes(column)) : defaultProjectHomeColumns,
    filter: parseProjectHomeFilter(params.get('filter')),
    projectIds: params.get('projectIds') || undefined,
  };
}

function normalizeFilter(state: ProjectHomeUrlState): ProjectManagementHomeFilterGroup {
  const rules = [...state.filter.rules];
  if (state.health) rules.push({ field: 'health', operator: 'is', values: [state.health] });
  if (state.priority) rules.push({ field: 'priority', operator: 'is', values: [state.priority] });
  if (state.status) rules.push({ field: 'status', operator: 'is', values: [state.status] });
  if (state.leadUserId) rules.push({ field: 'lead', operator: 'is', values: [state.leadUserId] });
  // Preset views are interpreted by the HOME query service so owner/member
  // visibility and risk semantics stay identical to the permission boundary.
  return { conjunction: 'and', rules };
}

export function projectHomeQuery(state: ProjectHomeUrlState, _userId?: string, projectIds?: string): ProjectManagementHomeQuery {
  const filter = normalizeFilter(state);
  return {
    collection: state.collection,
    view: state.view,
    keyword: state.keyword || undefined,
    health: state.health,
    priority: state.priority,
    leadUserId: state.leadUserId,
    status: state.status,
    targetDateFrom: state.targetDateFrom,
    targetDateTo: state.targetDateTo,
    includeArchived: state.view === 'archived',
    sortBy: state.sortBy,
    sortDirection: state.sortDirection,
    pageIndex: 1,
    pageSize: 100,
    filter: serializeProjectHomeFilter(filter),
    columns: state.columns.join(','),
    density: state.density,
    insights: state.insights,
    insightsTab: state.insightsTab,
    projectIds: projectIds ?? state.projectIds,
  };
}

export function updateProjectHomeParam(params: URLSearchParams, name: string, value?: string): URLSearchParams {
  const next = new URLSearchParams(params);
  if (value) next.set(name, value); else next.delete(name);
  return next;
}

export function densityRowHeight(density: ProjectHomeDensity): number {
  return density === 'compact' ? 42 : density === 'comfortable' ? 56 : 48;
}
