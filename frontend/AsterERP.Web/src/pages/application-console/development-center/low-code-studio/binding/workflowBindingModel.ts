import type { DesignerDocument, DesignerDocumentNode } from '../document/DesignerDocument';

import type { WorkflowBindingChangeResult, WorkflowBindingDraft, WorkflowPageContext, WorkflowSyncStatus } from './workflowBindingTypes';

export function readWorkflowBindingDraft(document: DesignerDocument, page: WorkflowPageContext): WorkflowBindingDraft {
  const binding = document.workflowBindings.find((item) => item.type === 'workflow') ?? null;
  const config = asRecord(binding?.config);
  const pageCode = text(page.pageCode) || text(document.runtimeContext.pageCode) || 'designer_page';
  const keyField = text(page.keyField) || 'id';
  return {
    appCode: text(config.appCode) || text(page.appCode) || text(document.runtimeContext.appCode),
    businessKeyExpression: text(config.businessKeyExpression) || `{{row.${keyField}}}`,
    businessType: text(config.businessType) || text(page.businessType) || pageCode,
    enabled: config.enabled !== false,
    menuCode: text(config.menuCode) || text(page.menuCode) || `${pageCode}-menu`,
    processDefinitionId: text(config.processDefinitionId) || text(binding?.targetId),
    processDefinitionKey: text(config.processDefinitionKey),
    processDefinitionName: text(config.processDefinitionName) || text(binding?.name),
    processDefinitionVersion: numberOrNull(config.processDefinitionVersion),
    syncStatus: syncStatus(config.syncStatus) ?? 'pendingPublish',
    tenantId: text(config.tenantId) || text(page.tenantId) || text(document.runtimeContext.tenantId),
    titleTemplate: text(config.titleTemplate) || `${page.pageName || pageCode}审批`
  };
}

export function applyWorkflowBindingDraft(document: DesignerDocument, page: WorkflowPageContext, draft: WorkflowBindingDraft): WorkflowBindingChangeResult {
  const errors = validateWorkflowBindingDraft(draft);
  if (errors.length > 0) return { document, errors };
  const existing = document.workflowBindings.find((item) => item.type === 'workflow');
  const pageCode = text(page.pageCode) || text(document.runtimeContext.pageCode) || 'designer_page';
  const binding = {
    ...(existing ?? {}),
    config: { ...asRecord(existing?.config), appCode: draft.appCode || null, businessKeyExpression: draft.businessKeyExpression, businessType: draft.businessType, enabled: draft.enabled, menuCode: draft.menuCode, processDefinitionId: draft.processDefinitionId || null, processDefinitionKey: draft.processDefinitionKey || null, processDefinitionName: draft.processDefinitionName || null, processDefinitionVersion: draft.processDefinitionVersion, syncStatus: 'pendingPublish', tenantId: draft.tenantId || null, titleTemplate: draft.titleTemplate },
    id: text(existing?.id) || `${pageCode}_workflow_binding`,
    name: draft.processDefinitionName || draft.processDefinitionKey || '页面审批流',
    targetId: draft.processDefinitionId || null,
    type: 'workflow'
  };
  const index = document.workflowBindings.findIndex((item) => item.type === 'workflow');
  const workflowBindings = index < 0 ? [...document.workflowBindings, binding] : document.workflowBindings.map((item, itemIndex) => itemIndex === index ? binding : item);
  return { document: { ...document, workflowBindings, elements: upsertWorkflowAction(document.elements, draft) }, errors: [] };
}

export function validateWorkflowBindingDraft(draft: WorkflowBindingDraft): string[] {
  if (!draft.enabled) return [];
  const errors: string[] = [];
  if (!draft.processDefinitionId) errors.push('请选择审批流定义。');
  if (!draft.processDefinitionKey) errors.push('审批流定义 Key 不能为空。');
  if (!draft.menuCode) errors.push('菜单编码不能为空。');
  if (!draft.businessType) errors.push('业务类型不能为空。');
  if (!draft.businessKeyExpression) errors.push('业务 Key 表达式不能为空。');
  if (!draft.titleTemplate) errors.push('标题模板不能为空。');
  return errors;
}

function upsertWorkflowAction(elements: Record<string, DesignerDocumentNode>, draft: WorkflowBindingDraft): Record<string, DesignerDocumentNode> {
  const action = Object.values(elements).find((element) => element.type === 'workflow.actions');
  if (!action) return elements;
  const events = action.events.some((event) => event.type === 'workflow.start') ? action.events : [...action.events, { type: 'workflow.start', config: { enabled: draft.enabled, bindingType: 'workflow' } }];
  return { ...elements, [action.id]: { ...action, events } };
}
function text(value: unknown): string { return typeof value === 'string' ? value.trim() : ''; }
function numberOrNull(value: unknown): number | null { return typeof value === 'number' && Number.isInteger(value) ? value : null; }
function syncStatus(value: unknown): WorkflowSyncStatus | null { return value === 'pendingPublish' || value === 'published' || value === 'synced' || value === 'syncFailed' ? value : null; }
function asRecord(value: unknown): Record<string, unknown> { return value && typeof value === 'object' && !Array.isArray(value) ? value as Record<string, unknown> : {}; }
