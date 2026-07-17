import type { LucideIcon } from 'lucide-react';

import type { GridPageResult } from '../../api/shared.types';
import type { OperationLogListItemDto } from '../../api/system/operation-logs.api';
import type { MenuListItemDto, MenuTreeNodeDto } from '../../api/system/system.types';
import type { WorkflowTaskListItemDto, WorkflowTaskSummaryDto } from '../../api/workflow/workflows.api';
import { normalizeWorkflowMenuPath } from '../../app/navigation/workflowMenuDisplay';
import type { ApiEnvelope } from '../../core/http/apiEnvelope';
import { flowiseI18nKeys } from '../../features/flowise-studio/i18n/flowiseI18nKeys';

export const pageSize = 1;
export const listSize = 5;

export type DashboardQueryResult = {
  data?: unknown;
  error?: Error | null;
  isError?: boolean;
  isLoading?: boolean;
};

export interface DashboardMetric {
  desc: string;
  icon: LucideIcon;
  isLoading?: boolean;
  noAccess?: boolean;
  tone: 'amber' | 'blue' | 'emerald' | 'rose' | 'teal';
  title: string;
  value: number | string;
}

export interface ShortcutItem {
  desc: string;
  icon: LucideIcon;
  path: string;
  tone: string;
  title: string;
}

export interface HomeMenuEntry {
  desc: string;
  path: string;
  title: string;
}

type FlowiseMenuMeta = {
  descKey: string;
  titleKey: string;
};

const flowiseMenuMetaByPath: Record<string, FlowiseMenuMeta> = {
  '/flowise/account': {
    descKey: 'flowise.descriptions.account',
    titleKey: flowiseI18nKeys.pages.account
  },
  '/flowise/api-keys': {
    descKey: 'flowise.native.apiKeys.description',
    titleKey: flowiseI18nKeys.pages.apiKeys
  },
  '/flowise/assistants': {
    descKey: 'flowise.native.assistants.description',
    titleKey: flowiseI18nKeys.pages.assistants
  },
  '/flowise/chatflows': {
    descKey: 'flowise.descriptions.chatflows',
    titleKey: flowiseI18nKeys.pages.chatflows
  },
  '/flowise/credentials': {
    descKey: 'flowise.native.credentials.description',
    titleKey: flowiseI18nKeys.pages.credentials
  },
  '/flowise/datasets': {
    descKey: 'flowise.native.datasets.description',
    titleKey: flowiseI18nKeys.pages.datasets
  },
  '/flowise/document-stores': {
    descKey: 'flowise.native.documentStores.description',
    titleKey: flowiseI18nKeys.pages.documentStores
  },
  '/flowise/evaluations': {
    descKey: 'flowise.native.evaluations.description',
    titleKey: flowiseI18nKeys.pages.evaluations
  },
  '/flowise/evaluators': {
    descKey: 'flowise.native.evaluators.description',
    titleKey: flowiseI18nKeys.pages.evaluators
  },
  '/flowise/executions': {
    descKey: 'flowise.executions.description',
    titleKey: flowiseI18nKeys.pages.executions
  },
  '/flowise/login-activity': {
    descKey: 'flowise.native.loginActivity.description',
    titleKey: flowiseI18nKeys.pages.loginActivity
  },
  '/flowise/logs': {
    descKey: 'flowise.native.logs.description',
    titleKey: flowiseI18nKeys.pages.logs
  },
  '/flowise/marketplaces': {
    descKey: 'flowise.native.marketplaces.description',
    titleKey: flowiseI18nKeys.pages.marketplaces
  },
  '/flowise/sso-config': {
    descKey: 'flowise.native.ssoConfig.description',
    titleKey: flowiseI18nKeys.pages.ssoConfig
  },
  '/flowise/tools': {
    descKey: 'flowise.native.tools.description',
    titleKey: flowiseI18nKeys.pages.tools
  },
  '/flowise/variables': {
    descKey: 'flowise.native.variables.description',
    titleKey: flowiseI18nKeys.pages.variables
  },
  '/flowise/workflows': {
    descKey: 'flowise.descriptions.workflows',
    titleKey: flowiseI18nKeys.pages.workflows
  }
};

export function resolveFlowiseMenuMeta(path: string): FlowiseMenuMeta | null {
  return flowiseMenuMetaByPath[path] ?? null;
}

export function getScopedQueryKey(baseKey: readonly unknown[], systemKey: string): readonly unknown[] {
  return [...baseKey, 'system', systemKey] as const;
}

export function getQueryResult(results: Record<string, DashboardQueryResult>, key: string): DashboardQueryResult {
  return results[key] ?? {};
}

export function getGridTotal(response: unknown): number | null {
  const envelope = response as ApiEnvelope<GridPageResult<unknown>> | undefined;
  return envelope?.data?.total ?? null;
}

export function getGridItems<TItem>(response: unknown): TItem[] {
  const envelope = response as ApiEnvelope<GridPageResult<TItem>> | undefined;
  return envelope?.data?.items ?? [];
}

export function getEnvelopeData<TData>(response: unknown): TData | null {
  const envelope = response as ApiEnvelope<TData> | undefined;
  return envelope?.data ?? null;
}

export function countMenuNodes(nodes: MenuTreeNodeDto[]): number {
  return nodes.reduce((count, node) => count + 1 + countMenuNodes(node.children ?? []), 0);
}

export function flattenFlowiseMenus(nodes: MenuTreeNodeDto[], parentName = ''): HomeMenuEntry[] {
  return nodes.flatMap((node) => {
    const groupName = parentName || node.menuName;
    const children = flattenFlowiseMenus(node.children ?? [], groupName);
    const routePath = node.routePath?.trim();
    const pageCode = node.pageCode?.trim();
    const targetPath = normalizeWorkflowMenuPath(pageCode ? `/pages/${encodeURIComponent(pageCode)}` : routePath);
    if (!targetPath?.startsWith('/flowise/')) {
      return children;
    }

    const meta = resolveFlowiseMenuMeta(targetPath);

    return [
      {
        desc: meta ? meta.descKey : groupName,
        path: targetPath,
        title: meta ? meta.titleKey : node.menuName
      },
      ...children
    ];
  });
}

export function flattenFlowiseMenuRows(rows: MenuListItemDto[]): HomeMenuEntry[] {
  return rows
    .filter((row) => row.routePath?.startsWith('/flowise/'))
    .sort((left, right) => left.sortOrder - right.sortOrder || left.menuName.localeCompare(right.menuName))
    .map((row) => ({
      desc: resolveFlowiseMenuMeta(normalizeWorkflowMenuPath(row.routePath))?.descKey ?? row.parentMenuName ?? '',
      path: normalizeWorkflowMenuPath(row.routePath),
      title: resolveFlowiseMenuMeta(normalizeWorkflowMenuPath(row.routePath))?.titleKey ?? row.menuName
    }));
}

export function formatDateTime(value?: string | null, locale = 'zh-CN'): string {
  if (!value) {
    return '-';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString(locale, {
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    month: '2-digit'
  });
}

export function getMetricValue(total: number | null, result: DashboardQueryResult, noAccess: boolean, noAccessText: string, loadingText: string): number | string {
  if (noAccess) {
    return noAccessText;
  }

  if (result.isLoading) {
    return loadingText;
  }

  return total ?? '-';
}

export function getSummaryMetricValue(
  summary: WorkflowTaskSummaryDto | null,
  key: keyof WorkflowTaskSummaryDto,
  result: DashboardQueryResult,
  noAccess: boolean,
  noAccessText: string,
  loadingText: string
): number | string {
  if (noAccess) {
    return noAccessText;
  }

  if (result.isLoading) {
    return loadingText;
  }

  return summary?.[key] ?? '-';
}

export function formatWorkflowTaskTitle(task: WorkflowTaskListItemDto, fallbackTitle: string): string {
  return task.name || task.processName || task.taskDefinitionKey || fallbackTitle;
}

export function formatWorkflowBusiness(task: WorkflowTaskListItemDto, fallbackBusinessType: string): string {
  const businessType = task.businessType || fallbackBusinessType;
  const businessKey = task.businessKey || '-';
  return `${businessType} / ${businessKey}`;
}

export function formatOperationLogTitle(log: OperationLogListItemDto, workflowListText: string, workflowOperationText: string): string {
  const title = log.routeDisplayName || log.requestPath || '-';
  const workflowListAction = new RegExp(`AiFlowiseChatflowsController\\.Get${'Agent'}${'flows'}Async\\s*\\(AsterERP\\.Api\\)`, 'gi');
  if (workflowListAction.test(title)) {
    return workflowListText;
  }

  if (/AiFlowiseChatflowsController\.([A-Za-z]+)Agentflow([A-Za-z]*)Async\s*\(AsterERP\.Api\)/i.test(title)) {
    return workflowOperationText;
  }

  return title;
}
